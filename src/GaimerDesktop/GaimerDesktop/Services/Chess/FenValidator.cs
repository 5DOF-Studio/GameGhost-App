namespace GaimerDesktop.Services.Chess;

public static class FenValidator
{
    private static readonly HashSet<char> ValidPieces = new("KQRBNPkqrbnp");
    private static readonly HashSet<char> ValidCastling = new("KQkq");

    public static bool IsValid(string fen, out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(fen))
        {
            errors.Add("FEN string is empty or null");
            return false;
        }

        var fields = fen.Split(' ');
        if (fields.Length != 6)
        {
            errors.Add($"FEN must have 6 fields separated by spaces, got {fields.Length}");
            return false;
        }

        ValidateBoard(fields[0], errors);
        ValidateSideToMove(fields[1], errors);
        ValidateCastlingRights(fields[2], errors);
        ValidateEnPassant(fields[3], fields[1], errors);
        ValidateHalfmoveClock(fields[4], errors);
        ValidateFullmoveNumber(fields[5], errors);

        return errors.Count == 0;
    }

    private static void ValidateBoard(string board, List<string> errors)
    {
        var ranks = board.Split('/');
        if (ranks.Length != 8)
        {
            errors.Add($"Board must have 8 ranks, got {ranks.Length}");
            return;
        }

        int whiteKings = 0, blackKings = 0;
        int whitePawns = 0, blackPawns = 0;

        for (int i = 0; i < ranks.Length; i++)
        {
            int squareCount = 0;
            foreach (char c in ranks[i])
            {
                if (char.IsDigit(c))
                {
                    squareCount += c - '0';
                }
                else if (ValidPieces.Contains(c))
                {
                    squareCount++;

                    switch (c)
                    {
                        case 'K': whiteKings++; break;
                        case 'k': blackKings++; break;
                        case 'P':
                            whitePawns++;
                            if (i == 0 || i == 7)
                                errors.Add($"White pawn on rank {(i == 0 ? 8 : 1)} (rank 1 or rank 8) is invalid");
                            break;
                        case 'p':
                            blackPawns++;
                            if (i == 0 || i == 7)
                                errors.Add($"Black pawn on rank {(i == 0 ? 8 : 1)} (rank 1 or rank 8) is invalid");
                            break;
                    }
                }
                else
                {
                    errors.Add($"Invalid character '{c}' in rank {8 - i}");
                    return;
                }
            }

            if (squareCount != 8)
            {
                errors.Add($"Rank {8 - i} must have 8 squares, got {squareCount}");
            }
        }

        if (whiteKings != 1)
            errors.Add($"Must have exactly 1 white king, found {whiteKings}");
        if (blackKings != 1)
            errors.Add($"Must have exactly 1 black king, found {blackKings}");
        if (whitePawns > 8)
            errors.Add($"White cannot have more than 8 pawns, found {whitePawns}");
        if (blackPawns > 8)
            errors.Add($"Black cannot have more than 8 pawns, found {blackPawns}");
    }

    private static void ValidateSideToMove(string side, List<string> errors)
    {
        if (side != "w" && side != "b")
            errors.Add($"Invalid side to move '{side}', must be 'w' or 'b'");
    }

    private static void ValidateCastlingRights(string castling, List<string> errors)
    {
        if (castling == "-") return;

        foreach (char c in castling)
        {
            if (!ValidCastling.Contains(c))
            {
                errors.Add($"Invalid castling character '{c}', must be from KQkq or -");
                return;
            }
        }
    }

    private static void ValidateEnPassant(string ep, string sideToMove, List<string> errors)
    {
        if (ep == "-") return;

        if (ep.Length != 2 || ep[0] < 'a' || ep[0] > 'h' || (ep[1] != '3' && ep[1] != '6'))
        {
            errors.Add($"Invalid en passant square '{ep}', must be a-h on rank 3 or 6, or '-'");
            return;
        }

        // If white to move, ep square must be on rank 6 (black just pushed); if black, rank 3
        if (sideToMove == "w" && ep[1] != '6')
            errors.Add($"En passant square '{ep}' must be on rank 6 when white to move");
        else if (sideToMove == "b" && ep[1] != '3')
            errors.Add($"En passant square '{ep}' must be on rank 3 when black to move");
    }

    private static void ValidateHalfmoveClock(string halfmove, List<string> errors)
    {
        if (!int.TryParse(halfmove, out var value) || value < 0)
            errors.Add($"Invalid halfmove clock '{halfmove}', must be a non-negative integer");
    }

    private static void ValidateFullmoveNumber(string fullmove, List<string> errors)
    {
        if (!int.TryParse(fullmove, out var value) || value < 1)
            errors.Add($"Invalid fullmove number '{fullmove}', must be a positive integer");
    }
}
