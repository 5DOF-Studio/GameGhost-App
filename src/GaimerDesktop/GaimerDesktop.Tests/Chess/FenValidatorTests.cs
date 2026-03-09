using GaimerDesktop.Services.Chess;

namespace GaimerDesktop.Tests.Chess;

public class FenValidatorTests
{
    // ── Valid Positions ────────────────────────────────────────────────────

    [Fact]
    public void IsValid_StandardStartingPosition_ReturnsTrue()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            out var errors);

        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_MidGamePosition_ReturnsTrue()
    {
        // Sicilian Defense: 1.e4 c5 2.Nf3 d6 3.d4 cxd4 4.Nxd4
        var result = FenValidator.IsValid(
            "rnbqkbnr/pp2pppp/3p4/8/3NP3/8/PPP2PPP/RNBQKB1R b KQkq - 0 4",
            out var errors);

        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_EndgamePosition_ReturnsTrue()
    {
        // King and rook vs king
        var result = FenValidator.IsValid(
            "8/8/8/4k3/8/8/8/4K2R w K - 0 1",
            out var errors);

        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_NoCastlingRights_ReturnsTrue()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w - - 0 1",
            out var errors);

        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_EnPassantSquare_ReturnsTrue()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            out var errors);

        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    // ── Invalid: Structural ───────────────────────────────────────────────

    [Fact]
    public void IsValid_TooFewRanks_ReturnsFalse()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("8 ranks"));
    }

    [Fact]
    public void IsValid_TooManyRanks_ReturnsFalse()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("8 ranks"));
    }

    [Fact]
    public void IsValid_RankSumNot8_ReturnsFalse()
    {
        // First rank only sums to 7 (missing a square)
        var result = FenValidator.IsValid(
            "rnbqkbn/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("Rank") && e.Contains("8"));
    }

    [Fact]
    public void IsValid_InvalidPieceCharacter_ReturnsFalse()
    {
        // 'x' is not a valid piece
        var result = FenValidator.IsValid(
            "xnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("Invalid character"));
    }

    // ── Invalid: King Count ───────────────────────────────────────────────

    [Fact]
    public void IsValid_NoWhiteKing_ReturnsFalse()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQ1BNR w KQkq - 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("white king"));
    }

    [Fact]
    public void IsValid_TwoBlackKings_ReturnsFalse()
    {
        var result = FenValidator.IsValid(
            "knbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("black king"));
    }

    // ── Invalid: Pawns ────────────────────────────────────────────────────

    [Fact]
    public void IsValid_PawnOnRank1_ReturnsFalse()
    {
        // White pawn on rank 1 (black's back rank in FEN = rank 8 in chess = first FEN rank)
        var result = FenValidator.IsValid(
            "pnbqkbnr/1ppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("pawn") && (e.Contains("rank 1") || e.Contains("rank 8")));
    }

    [Fact]
    public void IsValid_PawnOnRank8_ReturnsFalse()
    {
        // White pawn on rank 8 (white's back rank = last FEN rank)
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/1PPPPPPP/PNBQKBNR w KQkq - 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("pawn") && (e.Contains("rank 1") || e.Contains("rank 8")));
    }

    [Fact]
    public void IsValid_TooManyWhitePawns_ReturnsFalse()
    {
        // 9 white pawns
        var result = FenValidator.IsValid(
            "rnbqkbnr/8/8/8/8/PPPPPPPPP/1PPPPPPP/RNBQKBNR w KQkq - 0 1",
            out var errors);

        result.Should().BeFalse();
        // Either rank sum error or pawn count error
        errors.Should().NotBeEmpty();
    }

    // ── Invalid: Side to Move ─────────────────────────────────────────────

    [Fact]
    public void IsValid_BadSideToMove_ReturnsFalse()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR x KQkq - 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("side to move"));
    }

    // ── Invalid: Castling Rights ──────────────────────────────────────────

    [Fact]
    public void IsValid_BadCastlingRights_ReturnsFalse()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w XYZ - 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("castling"));
    }

    // ── Invalid: En Passant ────────────────────────────────────────────────

    [Fact]
    public void IsValid_InvalidEnPassantSquare_ReturnsFalse()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq z9 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("en passant"));
    }

    [Fact]
    public void IsValid_EnPassantWrongRankForSide_ReturnsFalse()
    {
        // White to move but ep on rank 3 (should be rank 6)
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e3 0 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("rank 6"));
    }

    [Fact]
    public void IsValid_EnPassantCorrectRankForBlack_ReturnsTrue()
    {
        // Black to move, ep on rank 3 — valid
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            out var errors);

        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_EnPassantCorrectRankForWhite_ReturnsTrue()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/ppp1pppp/8/3pP3/8/8/PPPP1PPP/RNBQKBNR w KQkq d6 0 3",
            out var errors);

        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    // ── Invalid: Halfmove Clock ─────────────────────────────────────────────

    [Fact]
    public void IsValid_NegativeHalfmoveClock_ReturnsFalse()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - -1 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("halfmove clock"));
    }

    [Fact]
    public void IsValid_NonNumericHalfmove_ReturnsFalse()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - abc 1",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("halfmove clock"));
    }

    // ── Invalid: Fullmove Number ────────────────────────────────────────────

    [Fact]
    public void IsValid_ZeroFullmoveNumber_ReturnsFalse()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 0",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("fullmove number"));
    }

    [Fact]
    public void IsValid_NonNumericFullmove_ReturnsFalse()
    {
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 xyz",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("fullmove number"));
    }

    // ── Invalid: Edge Cases ───────────────────────────────────────────────

    [Fact]
    public void IsValid_EmptyString_ReturnsFalse()
    {
        var result = FenValidator.IsValid("", out var errors);

        result.Should().BeFalse();
        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void IsValid_Null_ReturnsFalse()
    {
        var result = FenValidator.IsValid(null!, out var errors);

        result.Should().BeFalse();
        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void IsValid_TooFewFields_ReturnsFalse()
    {
        // Only board position, no other fields
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR",
            out var errors);

        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("6 fields"));
    }

    [Fact]
    public void IsValid_PartialCastling_ReturnsTrue()
    {
        // Only kingside castling for white
        var result = FenValidator.IsValid(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w K - 0 1",
            out var errors);

        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }
}
