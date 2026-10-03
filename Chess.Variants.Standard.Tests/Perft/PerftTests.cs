// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Games;
using Chess.Variants.Standard.Notation.Fen;

namespace Chess.Variants.Standard.Tests.Perft;

public sealed class PerftTests
{
    [Theory]
    [InlineData(0, 1L)]
    [InlineData(1, 20L)]
    [InlineData(2, 400L)]
    [InlineData(3, 8_902L)]
    public void InitialPosition_NormalDepthsMatchCanonicalCounts(
        int depth,
        long expectedNodes)
    {
        var game = CreatePerftGame(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR " + "w KQkq - 0 1");

        Assert.Equal(expectedNodes, CountNodes(game, depth));
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void InitialPosition_Depth4MatchesCanonicalCount()
    {
        Assert.Equal(
            197_281,
            CountNodes(
                CreatePerftGame(
                    "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/" +
                    "RNBQKBNR w KQkq - 0 1"),
                4));
    }

    [Fact(Explicit = true)]
    [Trait("Category", "Manual")]
    public void InitialPosition_Depth5MatchesCanonicalCount()
    {
        Assert.Equal(
            4_865_609,
            CountNodes(
                CreatePerftGame(
                    "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/" +
                    "RNBQKBNR w KQkq - 0 1"),
                5));
    }

    [Theory]
    [InlineData(1, 48L)]
    [InlineData(2, 2_039L)]
    public void Kiwipete_NormalDepthsMatchCanonicalCounts(
        int depth,
        long expectedNodes)
    {
        Assert.Equal(expectedNodes, CountNodes(CreateKiwipete(), depth));
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void Kiwipete_Depth3MatchesCanonicalCount()
    {
        Assert.Equal(97_862, CountNodes(CreateKiwipete(), 3));
    }

    [Fact(Explicit = true)]
    [Trait("Category", "Manual")]
    public void Kiwipete_Depth4MatchesCanonicalCount()
    {
        Assert.Equal(4_085_603, CountNodes(CreateKiwipete(), 4));
    }

    [Theory]
    [InlineData(1, 14L)]
    [InlineData(2, 191L)]
    [InlineData(3, 2_812L)]
    public void Position3_NormalDepthsMatchCanonicalCounts(
        int depth,
        long expectedNodes)
    {
        var game = CreatePerftGame("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1");

        Assert.Equal(expectedNodes, CountNodes(game, depth));
    }

    [Theory]
    [InlineData(1, 6L)]
    [InlineData(2, 264L)]
    [InlineData(3, 9_467L)]
    public void Position4_NormalDepthsMatchCanonicalCounts(
        int depth,
        long expectedNodes)
    {
        var game = CreatePerftGame(
            "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/" +
            "R2Q1RK1 w kq - 0 1");

        Assert.Equal(expectedNodes, CountNodes(game, depth));
    }

    [Theory]
    [InlineData(1, 44L)]
    [InlineData(2, 1_486L)]
    [InlineData(3, 62_379L)]
    public void Position5_NormalDepthsMatchCanonicalCounts(
        int depth,
        long expectedNodes)
    {
        var game = CreatePerftGame(
            "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/" + "RNBQK2R w KQ - 1 8");

        Assert.Equal(expectedNodes, CountNodes(game, depth));
    }

    [Theory]
    [InlineData(1, 46L)]
    [InlineData(2, 2_079L)]
    [InlineData(3, 89_890L)]
    public void Position6_NormalDepthsMatchCanonicalCounts(
        int depth,
        long expectedNodes)
    {
        var game = CreatePerftGame(
            "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/" +
            "1PP1QPPP/R4RK1 w - - 0 10");

        Assert.Equal(expectedNodes, CountNodes(game, depth));
    }

    private static long CountNodes(
        Game game,
        int depth)
    {
        if (depth == 0)
        {
            return 1;
        }

        long nodes = 0;
        var moves = TestSupport.AllMoves(game);

        foreach (var move in moves)
        {
            game.Execute(move);
            nodes += CountNodes(game, depth - 1);
            game.UndoLastMove();
        }

        return nodes;
    }

    private static Game CreateKiwipete()
    {
        return CreatePerftGame(
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/" +
            "PPPBBPPP/R3K2R w KQkq - 0 1");
    }

    private static Game CreatePerftGame(
        string fen)
    {
        var initialState = new FenCodec().Parse(fen);

        return TestSupport.CreateNonTerminatingGame(initialState);
    }
}
