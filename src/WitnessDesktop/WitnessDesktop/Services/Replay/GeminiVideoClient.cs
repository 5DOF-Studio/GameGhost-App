// File: src/WitnessDesktop/WitnessDesktop/Services/Replay/GeminiVideoClient.cs
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WitnessDesktop.Services.Replay;

public class GeminiVideoClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly int _pollingTimeoutSeconds;
    private readonly int _pollingIntervalMs;

    public GeminiVideoClient(HttpClient httpClient, string apiKey,
        int pollingTimeoutSeconds = 120, int pollingIntervalMs = 2000)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _pollingTimeoutSeconds = pollingTimeoutSeconds;
        _pollingIntervalMs = pollingIntervalMs;
    }

    public virtual async Task<GeminiFileMetadata> UploadVideoAsync(string filePath, CancellationToken ct)
    {
        var fileName = Path.GetFileName(filePath);

        using var content = new MultipartFormDataContent();

        // Metadata part
        var metadata = JsonSerializer.Serialize(new { file = new { display_name = fileName } });
        var metadataPart = new StringContent(metadata, Encoding.UTF8, "application/json");
        content.Add(metadataPart, "metadata");

        // File part — stream to avoid loading entire video into memory [C1]
        var fileStream = File.OpenRead(filePath);
        var filePart = new StreamContent(fileStream);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(filePart, "file", fileName);

        var response = await _httpClient.PostAsync(
            $"upload/v1beta/files?key={_apiKey}", content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var file = doc.RootElement.GetProperty("file");

        return new GeminiFileMetadata
        {
            Name = file.GetProperty("name").GetString()!,
            Uri = file.GetProperty("uri").GetString()!,
            State = file.GetProperty("state").GetString()!
        };
    }

    public virtual async Task WaitForActiveAsync(string fileName, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(_pollingTimeoutSeconds);

        while (DateTimeOffset.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var response = await _httpClient.GetAsync(
                $"v1beta/{fileName}?key={_apiKey}", ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var state = doc.RootElement.GetProperty("state").GetString();

            if (state == "ACTIVE") return;
            if (state == "FAILED") throw new InvalidOperationException($"Gemini file processing failed: {fileName}");

            await Task.Delay(_pollingIntervalMs, ct);
        }

        throw new TimeoutException($"Gemini file {fileName} did not become ACTIVE within {_pollingTimeoutSeconds}s");
    }

    public virtual async Task<string> GenerateContentAsync(string fileUri, string prompt, string model, CancellationToken ct)
    {
        var request = new
        {
            contents = new[] {
                new {
                    parts = new object[] {
                        new { file_data = new { mime_type = "video/mp4", file_uri = fileUri } },
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 65536,
                responseMimeType = "application/json"
            }
        };

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"v1beta/models/{model}:generateContent?key={_apiKey}", content, ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new GeminiRateLimitException("Gemini rate limit exceeded");

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()!;

        return RepairTruncatedJson(text);
    }

    public virtual async Task DeleteFileAsync(string fileName, CancellationToken ct)
    {
        try
        {
            await _httpClient.DeleteAsync(
                $"v1beta/{fileName}?key={_apiKey}", ct);
            // Ignore 404 — file may have auto-expired
        }
        catch { /* Best-effort cleanup */ }
    }

    public virtual async Task<int> CleanupStaleFilesAsync(TimeSpan maxAge, CancellationToken ct)
    {
        int deleted = 0;
        try
        {
            var response = await _httpClient.GetAsync(
                $"v1beta/files?key={_apiKey}", ct);
            if (!response.IsSuccessStatusCode) return 0;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("files", out var files)) return 0;

            var cutoff = DateTimeOffset.UtcNow - maxAge;
            foreach (var file in files.EnumerateArray())
            {
                if (!file.TryGetProperty("createTime", out var createTimeEl)) continue;
                if (!DateTimeOffset.TryParse(createTimeEl.GetString(), out var createTime)) continue;
                if (createTime >= cutoff) continue;

                var name = file.GetProperty("name").GetString()!;
                await DeleteFileAsync(name, ct);
                deleted++;
            }
        }
        catch { /* Best-effort */ }
        return deleted;
    }

    /// <summary>
    /// Attempts to repair truncated JSON output from Gemini (65K token ceiling).
    /// Finds the last complete JSON object and closes the array.
    /// </summary>
    public static string RepairTruncatedJson(string json)
    {
        json = json.Trim();
        try
        {
            JsonDocument.Parse(json);
            return json; // Valid — no repair needed
        }
        catch { /* Fall through to repair */ }

        // Find the last complete JSON object (ends with })
        var lastClose = json.LastIndexOf('}');
        if (lastClose < 0) return "[]";

        var repaired = json[..(lastClose + 1)];

        // Ensure array wrapper
        if (!repaired.TrimStart().StartsWith('['))
            repaired = "[" + repaired;

        if (!repaired.TrimEnd().EndsWith(']'))
            repaired += "]";

        try
        {
            JsonDocument.Parse(repaired);
            return repaired;
        }
        catch { return "[]"; }
    }
}

public record GeminiFileMetadata
{
    public required string Name { get; init; }
    public required string Uri { get; init; }
    public required string State { get; init; }
}

public class GeminiRateLimitException : Exception
{
    public GeminiRateLimitException(string message) : base(message) { }
}
