namespace GaimerDesktop.Services.Chess;

/// <summary>
/// Stateless parser for UCI protocol output lines from Stockfish.
/// All methods are pure functions — no side effects, no state.
/// </summary>
public static class UciParser
{
    /// <summary>
    /// Parses an "info depth ..." line into an EngineVariation.
    /// Returns null for non-info lines or info lines without score data.
    /// </summary>
    public static EngineVariation? ParseInfoLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("info "))
            return null;

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        int? depth = null, multipv = null, cp = null, mate = null;
        string[]? pv = null;

        for (int i = 1; i < tokens.Length; i++)
        {
            switch (tokens[i])
            {
                case "depth" when i + 1 < tokens.Length:
                    if (int.TryParse(tokens[i + 1], out var d))
                        depth = d;
                    i++;
                    break;

                case "multipv" when i + 1 < tokens.Length:
                    if (int.TryParse(tokens[i + 1], out var mpv))
                        multipv = mpv;
                    i++;
                    break;

                case "score" when i + 2 < tokens.Length:
                    if (tokens[i + 1] == "cp" && int.TryParse(tokens[i + 2], out var cpVal))
                    {
                        cp = cpVal;
                        i += 2;
                    }
                    else if (tokens[i + 1] == "mate" && int.TryParse(tokens[i + 2], out var mateVal))
                    {
                        mate = mateVal;
                        i += 2;
                    }
                    break;

                case "pv" when i + 1 < tokens.Length:
                    pv = tokens[(i + 1)..];
                    i = tokens.Length; // pv is always last, consume rest
                    break;
            }
        }

        // Must have score data (cp or mate) to be a meaningful variation
        if (cp is null && mate is null)
            return null;

        return new EngineVariation(
            MultiPvIndex: multipv ?? 1,
            Move: pv?.Length > 0 ? pv[0] : "",
            CentipawnEval: cp,
            MateIn: mate,
            PrincipalVariation: pv ?? Array.Empty<string>(),
            Depth: depth ?? 0);
    }

    /// <summary>
    /// Parses a "bestmove ..." line. Returns null if the line is not a bestmove line.
    /// Returns (bestMove, ponderMove?) tuple.
    /// </summary>
    public static (string BestMove, string? PonderMove)? ParseBestMove(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("bestmove "))
            return null;

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
            return null;

        var bestMove = tokens[1];
        string? ponder = null;

        if (tokens.Length >= 4 && tokens[2] == "ponder")
            ponder = tokens[3];

        return (bestMove, ponder);
    }

    /// <summary>
    /// Extracts total nodes from the last info line that contains node count.
    /// </summary>
    public static long ParseNodes(string line)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length - 1; i++)
        {
            if (tokens[i] == "nodes" && long.TryParse(tokens[i + 1], out var nodes))
                return nodes;
        }
        return 0;
    }

    /// <summary>
    /// Extracts time in ms from an info line.
    /// </summary>
    public static int ParseTime(string line)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length - 1; i++)
        {
            if (tokens[i] == "time" && int.TryParse(tokens[i + 1], out var time))
                return time;
        }
        return 0;
    }
}
