// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using Chess.Core.Board;
using Chess.Core.Games;
using Chess.Core.Games.Attacks;
using Chess.Core.Games.Status;
using Chess.Core.Games.Variants;
using Chess.Core.Movement;
using Chess.Core.Pieces;
using Chess.Core.Sides;
using Chess.Variants.Standard.Board;
using Chess.Variants.Standard.Board.Regions;
using Chess.Variants.Standard.Board.Topology;
using Chess.Variants.Standard.Games;
using Chess.Variants.Standard.Games.History;
using Chess.Variants.Standard.Games.Rules;
using Chess.Variants.Standard.Movement;
using Chess.Variants.Standard.Movement.Orientation;
using Chess.Variants.Standard.Pieces;
using Chess.Variants.Standard.Sides;

namespace Chess.Variants.Standard.Tests;

internal static class TestSupport
{
    public static Square Square(
        string coordinate)
    {
        if (coordinate.Length != 2 ||
            coordinate[0] is < 'a' or > 'h' ||
            coordinate[1] is < '1' or > '8')
        {
            throw new ArgumentException(
                "Use algebraic coordinates from a1 through h8.",
                nameof(coordinate));
        }

        var column = coordinate[0] - 'a';
        var row = '8' - coordinate[1];

        return BoardLayout.GetSquare(row, column);
    }

    public static Placement At(
        string coordinate,
        Side side,
        PieceDefinition definition)
    {
        return new Placement(Square(coordinate), side, definition);
    }

    public static Game CreateGame(
        params Placement[] placements)
    {
        return CreateGame(SideDefinitions.White, placements);
    }

    public static Game CreateGame(
        Side sideToMove,
        params Placement[] placements)
    {
        return CreateDefinition(
                TurnOrderDefinition.Instance,
                sideToMove,
                isStatusEvaluationEnabled: true,
                placements)
            .CreateGame();
    }

    public static Game CreateGame(
        TurnOrder turnOrder,
        params Placement[] placements)
    {
        return CreateDefinition(
                turnOrder,
                turnOrder.First,
                isStatusEvaluationEnabled: true,
                placements)
            .CreateGame();
    }

    public static Game CreateGame(
        StandardInitialState initialState)
    {
        return Variant.CreateGame(initialState);
    }

    public static Game CreateNonTerminatingGame(
        params Placement[] placements)
    {
        return CreateNonTerminatingGame(SideDefinitions.White, placements);
    }

    public static Game CreateNonTerminatingGame(
        Side sideToMove,
        params Placement[] placements)
    {
        return CreateDefinition(
                TurnOrderDefinition.Instance,
                sideToMove,
                isStatusEvaluationEnabled: false,
                placements)
            .CreateGame();
    }

    public static Game CreateNonTerminatingGame(
        TurnOrder turnOrder,
        params Placement[] placements)
    {
        return CreateDefinition(
                turnOrder,
                turnOrder.First,
                isStatusEvaluationEnabled: false,
                placements)
            .CreateGame();
    }

    public static Game CreateNonTerminatingGame(
        StandardInitialState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);

        var components = CreateRuleComponents(initialState);
        var gameStateFactory = new GameStateFactory(
            BoardTopologyFactory.Create(),
            TurnOrderDefinition.Instance,
            initialState.SideToMove,
            Orientations.Resolver,
            BoardRegions.Resolver,
            [.. initialState.Placements]);

        return CreateDefinition(
                gameStateFactory,
                components.LegalMoveGenerator,
                components.ExecutionResolver,
                new NonTerminatingStatusEvaluator())
            .CreateGame();
    }

    public static GameVariantDefinition CreateDefinition(
        TurnOrder turnOrder,
        params Placement[] placements)
    {
        return CreateDefinition(
            turnOrder,
            turnOrder.First,
            isStatusEvaluationEnabled: true,
            placements);
    }

    public static GameVariantDefinition CreateDefinition(
        Side sideToMove,
        params Placement[] placements)
    {
        return CreateDefinition(
            TurnOrderDefinition.Instance,
            sideToMove,
            isStatusEvaluationEnabled: true,
            placements);
    }

    private static GameVariantDefinition CreateDefinition(
        TurnOrder turnOrder,
        Side initialSide,
        bool isStatusEvaluationEnabled,
        params Placement[] placements)
    {
        var initialState = CreateInitialState(initialSide, placements);
        var components = CreateRuleComponents(initialState);
        var checkDetector = new CheckDetector(new PatternAttackGenerator());
        var legalGenerator = components.LegalMoveGenerator;
        var executionResolver = components.ExecutionResolver;
        var gameStateFactory = CreateGameStateFactory(
            turnOrder,
            initialSide,
            placements);

        IGameStatusEvaluator statusEvaluator = isStatusEvaluationEnabled
            ? CreateStatusEvaluator(
                gameStateFactory,
                legalGenerator,
                executionResolver,
                checkDetector,
                initialState)
            : new NonTerminatingStatusEvaluator();

        return CreateDefinition(
            gameStateFactory,
            legalGenerator,
            executionResolver,
            statusEvaluator);
    }

    public static GameVariantDefinition CreateDefinition(
        TurnOrder turnOrder,
        IGameMoveGenerator legalMoveGenerator,
        IMoveExecutionResolver executionResolver,
        IGameStatusEvaluator statusEvaluator,
        params Placement[] placements)
    {
        return CreateDefinition(
            CreateGameStateFactory(turnOrder, placements),
            legalMoveGenerator,
            executionResolver,
            statusEvaluator);
    }

    public static GameVariantDefinition CreateDefinition(
        GameStateFactory gameStateFactory,
        IGameMoveGenerator legalMoveGenerator,
        IMoveExecutionResolver executionResolver,
        IGameStatusEvaluator statusEvaluator)
    {
        return new GameVariantDefinition(
            new GameVariantId("test:standard-position"),
            "Standard test position",
            gameStateFactory,
            legalMoveGenerator,
            executionResolver,
            statusEvaluator);
    }

    public static GameStateFactory CreateGameStateFactory(
        TurnOrder turnOrder,
        params Placement[] placements)
    {
        return CreateGameStateFactory(turnOrder, turnOrder.First, placements);
    }

    public static GameStateFactory CreateGameStateFactory(
        TurnOrder turnOrder,
        Side initialSide,
        params Placement[] placements)
    {
        return new GameStateFactory(
            BoardTopologyFactory.Create(),
            turnOrder,
            initialSide,
            Orientations.Resolver,
            BoardRegions.Resolver,
            placements
                .Select(placement => new InitialPiecePlacement(
                    placement.Square,
                    placement.Side,
                    placement.Definition))
                .ToArray());
    }

    public static GameStateFactory CreateGameStateFactory(
        GameVariantDefinition definition)
    {
        return new GameStateFactory(
            definition.Topology,
            definition.TurnOrder,
            definition.InitialSide,
            Orientations.Resolver,
            BoardRegions.Resolver,
            [.. definition.InitialPlacements]);
    }

    public static IGameMoveGenerator CreateLegalMoveGenerator(
        IMoveExecutionResolver executionResolver)
    {
        var simulator = new GameMoveSimulator(executionResolver);
        var checkDetector = new CheckDetector(new PatternAttackGenerator());

        return CreateLegalMoveGenerator(
            simulator,
            checkDetector,
            new CastlingRightsEvaluator(
                new CastlingRights(true, true, true, true)),
            new StandardEnPassantTargetEvaluator(null));
    }

    private static LegalMoveGenerator CreateLegalMoveGenerator(
        GameMoveSimulator simulator,
        CheckDetector checkDetector,
        CastlingRightsEvaluator castlingRightsEvaluator,
        StandardEnPassantTargetEvaluator enPassantTargetEvaluator)
    {
        var pseudoLegalGenerator = new CastlingMoveGenerator(
            new EnPassantMoveGenerator(
                new PromotionMoveGenerator(new PseudoLegalGameMoveGenerator()),
                enPassantTargetEvaluator),
            simulator,
            checkDetector,
            castlingRightsEvaluator);

        return new LegalMoveGenerator(
            pseudoLegalGenerator,
            simulator,
            checkDetector);
    }

    public static IMoveExecutionResolver CreateExecutionResolver()
    {
        var castlingRightsEvaluator = new CastlingRightsEvaluator(
            new CastlingRights(true, true, true, true));
        var enPassantTargetEvaluator =
            new StandardEnPassantTargetEvaluator(null);

        return new CompositeMoveExecutionResolver(
            new BasicMoveExecutionResolver(),
            new PromotionMoveExecutionResolver(),
            new EnPassantMoveExecutionResolver(enPassantTargetEvaluator),
            new CastlingMoveExecutionResolver(castlingRightsEvaluator));
    }

    public static CastlingMoveExecutionResolver
        CreateCastlingMoveExecutionResolver()
    {
        return new CastlingMoveExecutionResolver(
            new CastlingRightsEvaluator(
                new CastlingRights(true, true, true, true)));
    }

    public static EnPassantMoveExecutionResolver
        CreateEnPassantMoveExecutionResolver()
    {
        return new EnPassantMoveExecutionResolver(
            new StandardEnPassantTargetEvaluator(null));
    }

    public static StatusEvaluator CreateStatusEvaluator(
        GameVariantDefinition definition)
    {
        var executionResolver = CreateExecutionResolver();
        var simulator = new GameMoveSimulator(executionResolver);
        var checkDetector = new CheckDetector(new PatternAttackGenerator());
        var castlingRightsEvaluator = new CastlingRightsEvaluator(
            InferCastlingRights(definition.InitialPlacements));
        var enPassantTargetEvaluator =
            new StandardEnPassantTargetEvaluator(null);
        var legalMoveGenerator = CreateLegalMoveGenerator(
            simulator,
            checkDetector,
            castlingRightsEvaluator,
            enPassantTargetEvaluator);

        return CreateStatusEvaluator(
            CreateGameStateFactory(definition),
            legalMoveGenerator,
            executionResolver,
            checkDetector,
            CreateInitialState(
                definition.InitialSide,
                definition
                    .InitialPlacements
                    .Select(placement => new Placement(
                        placement.Square,
                        placement.Side,
                        placement.Definition))
                    .ToArray()));
    }

    public static StatusEvaluator CreateStatusEvaluator(
        GameStateFactory gameStateFactory,
        IGameMoveGenerator legalMoveGenerator,
        IMoveExecutionResolver executionResolver,
        CheckDetector checkDetector,
        StandardInitialState? initialState = null)
    {
        var moveResolver = new GameMoveResolver(
            legalMoveGenerator,
            executionResolver);
        initialState ??= CreateInitialState(
            gameStateFactory.InitialSide,
            gameStateFactory
                .InitialPlacements
                .Select(placement => new Placement(
                    placement.Square,
                    placement.Side,
                    placement.Definition))
                .ToArray());
        var castlingRightsEvaluator = new CastlingRightsEvaluator(
            initialState.CastlingRights);
        var enPassantTargetEvaluator =
            new StandardEnPassantTargetEvaluator(initialState.EnPassantTarget);
        var positionFactsEvaluator = new StandardPositionFactsEvaluator(
            legalMoveGenerator,
            castlingRightsEvaluator,
            enPassantTargetEvaluator,
            initialState.HalfmoveClock,
            initialState.FullmoveNumber);
        var repetitionEvaluator = new StandardRepetitionEvaluator(
            positionFactsEvaluator,
            gameStateFactory.Create,
            new GameMoveExecutor(moveResolver));
        var halfmoveRuleEvaluator = new StandardHalfmoveRuleEvaluator(
            positionFactsEvaluator,
            moveResolver);

        return new StatusEvaluator(
            legalMoveGenerator,
            checkDetector,
            repetitionEvaluator,
            halfmoveRuleEvaluator);
    }

    public static StandardPositionFactsEvaluator CreatePositionFactsEvaluator(
        StandardInitialState initialState)
    {
        return CreateRuleComponents(initialState)
            .PositionFactsEvaluator;
    }

    public static StandardPositionFactsEvaluator CreatePositionFactsEvaluator(
        GameVariantDefinition definition)
    {
        return CreatePositionFactsEvaluator(CreateInitialState(definition));
    }

    public static StandardHalfmoveRuleEvaluator CreateHalfmoveRuleEvaluator(
        GameVariantDefinition definition)
    {
        var components = CreateRuleComponents(CreateInitialState(definition));
        var moveResolver = new GameMoveResolver(
            components.LegalMoveGenerator,
            components.ExecutionResolver);

        return new StandardHalfmoveRuleEvaluator(
            components.PositionFactsEvaluator,
            moveResolver);
    }

    public static StandardRepetitionEvaluator CreateRepetitionEvaluator(
        StandardInitialState initialState)
    {
        var components = CreateRuleComponents(initialState);
        var gameStateFactory = new GameStateFactory(
            BoardTopologyFactory.Create(),
            TurnOrderDefinition.Instance,
            initialState.SideToMove,
            Orientations.Resolver,
            BoardRegions.Resolver,
            [.. initialState.Placements]);
        var moveResolver = new GameMoveResolver(
            components.LegalMoveGenerator,
            components.ExecutionResolver);

        return new StandardRepetitionEvaluator(
            components.PositionFactsEvaluator,
            gameStateFactory.Create,
            new GameMoveExecutor(moveResolver));
    }

    public static StandardPositionFactsEvaluator CreatePositionFactsEvaluator(
        IGameMoveGenerator legalMoveGenerator,
        GameStateFactory gameStateFactory)
    {
        var initialState = CreateInitialState(
            gameStateFactory.InitialSide,
            gameStateFactory
                .InitialPlacements
                .Select(placement => new Placement(
                    placement.Square,
                    placement.Side,
                    placement.Definition))
                .ToArray());

        return new StandardPositionFactsEvaluator(
            legalMoveGenerator,
            new CastlingRightsEvaluator(initialState.CastlingRights),
            new StandardEnPassantTargetEvaluator(initialState.EnPassantTarget),
            initialState.HalfmoveClock,
            initialState.FullmoveNumber);
    }

    public static StandardInitialState CreateInitialState(
        Side sideToMove,
        IEnumerable<Placement> placements,
        CastlingRights? castlingRights = null,
        Square? enPassantTarget = null,
        int halfmoveClock = 0,
        int fullmoveNumber = 1)
    {
        var placementArray = placements.ToArray();
        var initialPlacements = placementArray
            .Select(placement => new InitialPiecePlacement(
                placement.Square,
                placement.Side,
                placement.Definition))
            .ToArray();

        return new StandardInitialState(
            initialPlacements,
            sideToMove,
            castlingRights ?? InferCastlingRights(initialPlacements),
            enPassantTarget,
            halfmoveClock,
            fullmoveNumber);
    }

    private static StandardInitialState CreateInitialState(
        GameVariantDefinition definition)
    {
        return CreateInitialState(
            definition.InitialSide,
            [
                .. definition.InitialPlacements.Select(placement =>
                    new Placement(
                        placement.Square,
                        placement.Side,
                        placement.Definition))
            ]);
    }

    private static RuleComponents CreateRuleComponents(
        StandardInitialState initialState)
    {
        var castlingRightsEvaluator = new CastlingRightsEvaluator(
            initialState.CastlingRights);
        var enPassantTargetEvaluator =
            new StandardEnPassantTargetEvaluator(initialState.EnPassantTarget);
        var executionResolver = new CompositeMoveExecutionResolver(
            new BasicMoveExecutionResolver(),
            new PromotionMoveExecutionResolver(),
            new EnPassantMoveExecutionResolver(enPassantTargetEvaluator),
            new CastlingMoveExecutionResolver(castlingRightsEvaluator));
        var simulator = new GameMoveSimulator(executionResolver);
        var checkDetector = new CheckDetector(new PatternAttackGenerator());
        var legalMoveGenerator = CreateLegalMoveGenerator(
            simulator,
            checkDetector,
            castlingRightsEvaluator,
            enPassantTargetEvaluator);
        var positionFactsEvaluator = new StandardPositionFactsEvaluator(
            legalMoveGenerator,
            castlingRightsEvaluator,
            enPassantTargetEvaluator,
            initialState.HalfmoveClock,
            initialState.FullmoveNumber);

        return new RuleComponents(
            legalMoveGenerator,
            executionResolver,
            positionFactsEvaluator);
    }

    private static CastlingRights InferCastlingRights(
        IEnumerable<InitialPiecePlacement> placements)
    {
        var placementArray = placements.ToArray();

        return new CastlingRights(
            HasPiece(
                placementArray,
                "e1",
                SideDefinitions.White,
                PieceDefinitions.King) &&
            HasPiece(
                placementArray,
                "h1",
                SideDefinitions.White,
                PieceDefinitions.Rook),
            HasPiece(
                placementArray,
                "e1",
                SideDefinitions.White,
                PieceDefinitions.King) &&
            HasPiece(
                placementArray,
                "a1",
                SideDefinitions.White,
                PieceDefinitions.Rook),
            HasPiece(
                placementArray,
                "e8",
                SideDefinitions.Black,
                PieceDefinitions.King) &&
            HasPiece(
                placementArray,
                "h8",
                SideDefinitions.Black,
                PieceDefinitions.Rook),
            HasPiece(
                placementArray,
                "e8",
                SideDefinitions.Black,
                PieceDefinitions.King) &&
            HasPiece(
                placementArray,
                "a8",
                SideDefinitions.Black,
                PieceDefinitions.Rook));
    }

    private static bool HasPiece(
        IEnumerable<InitialPiecePlacement> placements,
        string coordinate,
        Side side,
        PieceDefinition definition)
    {
        var square = Square(coordinate);

        return placements.Any(placement =>
            placement.Square == square &&
            ReferenceEquals(placement.Side, side) &&
            ReferenceEquals(placement.Definition, definition));
    }

    public static Move FindMove(
        Game game,
        string from,
        string to,
        MoveOptionId? option = null)
    {
        var expected = new Move(Square(from), Square(to), option);
        return Assert.Single(
            game.GenerateMoves(expected.From),
            move => move == expected);
    }

    public static GameMoveRecord Play(
        Game game,
        string from,
        string to,
        MoveOptionId? option = null)
    {
        return game.Execute(FindMove(game, from, to, option));
    }

    public static IReadOnlyList<Move> AllMoves(
        Game game)
    {
        return game
            .BoardState
            .GetPiecePositions(game.State.CurrentSide)
            .SelectMany(position => game.GenerateMoves(position.Square))
            .ToArray();
    }

    public static Piece PieceAt(
        Game game,
        string coordinate)
    {
        Assert.True(
            game.BoardState.TryGetPiece(Square(coordinate), out var piece));
        return piece;
    }
}

internal readonly record struct Placement(
    Square Square,
    Side Side,
    PieceDefinition Definition);

internal sealed record RuleComponents(
    IGameMoveGenerator LegalMoveGenerator,
    IMoveExecutionResolver ExecutionResolver,
    StandardPositionFactsEvaluator PositionFactsEvaluator);

internal sealed record StandardGameSnapshot(
    Side CurrentSide,
    GameMoveRecord? LastMove,
    IReadOnlyList<GameMoveRecord> History,
    IReadOnlyDictionary<Square, Piece> Pieces)
{
    public static StandardGameSnapshot Capture(
        Game game)
    {
        var pieces = game
            .BoardState
            .Topology
            .Squares
            .Where(square => game.BoardState.TryGetPiece(square, out _))
            .ToDictionary(
                square => square,
                square =>
                {
                    game.BoardState.TryGetPiece(square, out var piece);
                    return piece!;
                });

        return new StandardGameSnapshot(
            game.State.CurrentSide,
            game.State.LastMove,
            game.State.History.ToArray(),
            pieces);
    }

    public void AssertMatches(
        Game game)
    {
        Assert.Equal(CurrentSide, game.State.CurrentSide);
        Assert.Same(LastMove, game.State.LastMove);
        Assert.Equal(History.Count, game.State.History.Count);

        for (var index = 0; index < History.Count; index++)
        {
            Assert.Same(History[index], game.State.History[index]);
        }

        foreach (var square in game.BoardState.Topology.Squares)
        {
            var expectedOccupied = Pieces.TryGetValue(square, out var expected);
            var actualOccupied = game.BoardState.TryGetPiece(
                square,
                out var actual);

            Assert.Equal(expectedOccupied, actualOccupied);

            if (expectedOccupied)
            {
                Assert.Same(expected, actual);
                Assert.True(
                    game.BoardState.TryGetSquare(expected!, out var at));
                Assert.Equal(square, at);
            }
        }
    }
}

internal sealed class NonTerminatingStatusEvaluator : IGameStatusEvaluator
{
    public GameStatus Evaluate(
        GameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);

        return new GameStatus(StatusDefinitions.Active);
    }
}
