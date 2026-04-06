using System.Text.Json;

namespace WitnessDesktop.Services;

public static class OpenAiRealtimeSessionOptions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static string BuildSessionUpdateJson(string instructions, string voice)
    {
        var payload = new
        {
            type = "session.update",
            session = new
            {
                modalities = new[] { "text", "audio" },
                instructions,
                voice,
                input_audio_format = "pcm16",
                output_audio_format = "pcm16",
                input_audio_transcription = new
                {
                    model = "whisper-1"
                },
                turn_detection = new
                {
                    type = "server_vad",
                    threshold = 0.5,
                    prefix_padding_ms = 300,
                    silence_duration_ms = 500,
                    create_response = true,
                    interrupt_response = true
                }
            }
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
