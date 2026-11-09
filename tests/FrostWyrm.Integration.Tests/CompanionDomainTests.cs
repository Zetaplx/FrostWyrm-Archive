using System.Text.Json;
using CryptidCompanion;
using CryptidCompanion.Domain;
using FrostWyrm.Archive.DependencyInjection;
using FrostWyrm.Math.Hex;

namespace FrostWyrm.Integration.Tests;

public sealed class CompanionDomainTests
{
    private static Dictionary<string, string> Files() => Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Resources"), "*.yml")
        .ToDictionary(p => Path.GetFileName(p)!, File.ReadAllText);
    private static ContentCatalog Catalog()
    {
        var catalog = new ContentCatalog(); catalog.Load(Files()); return catalog;
    }
    private static GameManager Game(ContentCatalog catalog)
    {
        var dependencies = new DependencyManager();
        dependencies.Register(catalog);
        dependencies.Register(catalog.Prototypes);
        var game = new GameManager(); dependencies.InjectDependencies(game); return game;
    }
    private static (ContentCatalog Catalog, GameManager Game, SessionSave Save) Start(int players = 3, int seed = 42, string human = "Alpha")
    {
        var catalog = Catalog(); var game = Game(catalog);
        game.Begin("companion-1", players, human, seed);
        return (catalog, game, JsonSerializer.Deserialize<SessionSave>(game.Export())!);
    }
    private static bool Matches(ContentCatalog catalog, GameManager game, SessionSave save, string book, Position p)
        => ClueEvaluator.Matches(catalog.Prototypes.Index<CluePrototype>(save.Assignments[book]), game.Board!, p);
    private static void PayCubes(ContentCatalog catalog, GameManager game, SessionSave save)
    {
        while (game.PendingHumanCubes > 0)
            game.PlaceHumanCube(game.Board!.Spaces.First(s => !Matches(catalog, game, save, save.Human, s.Position) && !game.OwnMarker(save.Human, s.Position)).Position);
    }
    private static void Play(ContentCatalog catalog, GameManager game, SessionSave save)
    { PayCubes(catalog, game, save); game.StartPlay(); }

    [Theory]
    [InlineData(Hex.Orientation.FlatTop)]
    [InlineData(Hex.Orientation.PointTop)]
    public void OffsetConversionRoundTripsIncludingNegativeIndices(Hex.Orientation orientation)
    {
        for (var row = -12; row <= 12; row++)
            for (var col = -12; col <= 12; col++)
                Assert.Equal((row, col), HexCoordinates.ToOffset(HexCoordinates.FromOffset(row, col, orientation), orientation));
    }

    [Fact]
    public void AxialDistanceAndNeighborsAgreeWithGraphTraversal()
    {
        var origin = new AxialHex(0, 0);
        Assert.Equal(6, origin.Neighbors().Distinct().Count());
        Assert.All(origin.Neighbors(), h => Assert.Equal(1, origin.DistanceTo(h)));
        var reached = new HashSet<AxialHex> { origin };
        for (var distance = 1; distance <= 5; distance++)
        {
            reached.UnionWith(reached.SelectMany(h => h.Neighbors()).ToArray());
            Assert.Equal(1 + 3 * distance * (distance + 1), reached.Count);
            Assert.All(reached, h => Assert.InRange(origin.DistanceTo(h), 0, distance));
        }
    }

    [Fact]
    public void ReferenceImageHasConfirmedTileOrientationsAndStructures()
    {
        var catalog = Catalog(); var definition = catalog.Prototypes.Index<BoardPrototype>("companion-board");
        var board = new BoardState(definition, catalog.Prototypes);
        Assert.Equal(108, board.Spaces.Count);
        Assert.Equal(new[] { "tile5", "tile4", "tile1", "tile6", "tile2", "tile3" }, definition.Tiles.Select(t => t.Tile.ID));
        Assert.Equal(new[] { true, false, true, true, false, true }, definition.Tiles.Select(t => t.Flipped));
        Assert.Equal(Terrain.Water, board[new(0, 0)].Terrain);
        Assert.Equal(Animal.Bear, board[new(0, 0)].Animal);
        Assert.Equal(StructureKind.Stone, board[new(0, 3)].Structure!.Kind);
        Assert.Equal(StructureKind.Shack, board[new(3, 3)].Structure!.Kind);
        Assert.Equal(StructureColor.White, board[new(0, 3)].Structure!.Color);
        Assert.Equal(StructureKind.Shack, board[new(1, 8)].Structure!.Kind);
        Assert.Equal(StructureColor.Blue, board[new(4, 7)].Structure!.Color);
        Assert.Equal(StructureColor.Green, board[new(4, 8)].Structure!.Color);
        Assert.Equal(StructureKind.Stone, board[new(8, 7)].Structure!.Kind);
        Assert.Equal(6, board.Adjacent(new(4, 5)).Count());
        Assert.Equal(2, board.Adjacent(new(0, 0)).Count());
        Assert.Throws<ArgumentException>(() => board[new(-1, 0)]);
    }

    [Fact]
    public void EveryNormalClueAgreesWithAnIndependentDistanceOracle()
    {
        var catalog = Catalog(); var board = new BoardState(catalog.Prototypes.Index<BoardPrototype>("companion-board"), catalog.Prototypes);
        var clues = catalog.Prototypes.EnumeratePrototypes<CluePrototype>().ToArray();
        Assert.Equal(23, clues.Length);
        foreach (var clue in clues)
        foreach (var cell in board.Spaces)
        {
            bool Feature(BoardSpace s) => clue.Rule switch
            {
                ClueRule.TerrainPair or ClueRule.NearTerrain => clue.Terrains.Contains(s.Terrain),
                ClueRule.NearAnimal => s.Animal != Animal.None && (clue.Animal == Animal.None || s.Animal == clue.Animal),
                ClueRule.NearStructure => s.Structure?.Kind == clue.Structure,
                ClueRule.NearColor => s.Structure?.Color == clue.Color,
                _ => false
            };
            // Graph breadth-first search rather than production axial-distance filtering.
            var frontier = new HashSet<Position> { cell.Position };
            var visited = new HashSet<Position>(frontier);
            for (var step = 0; step < clue.Distance; step++)
            {
                frontier = frontier.SelectMany(p => board.Spaces.Where(s => board[p].Hex.Neighbors().Contains(s.Hex)).Select(s => s.Position))
                    .Where(p => !visited.Contains(p)).ToHashSet();
                visited.UnionWith(frontier);
            }
            var expected = visited.Any(p => Feature(board[p]));
            Assert.Equal(expected, ClueEvaluator.Matches(clue, board, cell.Position));
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void CardAssignmentsStayFixedAcrossHumanColorsAndRandomSeeds(int players)
    {
        var catalog = Catalog();
        var setup = catalog.Cards.Single().Setups.Single(s => s.PlayerCount == players);
        foreach (var bookId in setup.Books)
        foreach (var seed in new[] { 0, 140 })
        {
            var game = Game(catalog);
            game.Begin("companion-1", players, bookId.ID, seed);
            var save = JsonSerializer.Deserialize<SessionSave>(game.Export())!;
            Assert.Equal(setup.Assignments.OrderBy(p => p.Key).Select(p => (p.Key, p.Value.ID)),
                save.Assignments.OrderBy(p => p.Key).Select(p => (p.Key, p.Value)));
            Assert.Single(game.Board!.Spaces, s => save.Assignments.Keys.All(b => Matches(catalog, game, save, b, s.Position)));
            Assert.Equal(2 * (players - 1), game.Markers.Count);
            foreach (var book in game.Opponents)
            {
                var cubes = game.Markers.Where(m => m.Book == book).ToArray();
                Assert.Equal(2, cubes.Select(m => m.Position).Distinct().Count());
                Assert.All(cubes, m => { Assert.Equal(Piece.Cube, m.Piece); Assert.False(Matches(catalog, game, save, book, m.Position)); });
            }
        }
    }

    [Fact]
    public void PlayerColorsMatchThePhysicalBooks()
    {
        var catalog = Catalog();
        foreach (var (book, color) in new Dictionary<string, string>
            { ["Alpha"] = "red", ["Beta"] = "green", ["Gamma"] = "blue", ["Delta"] = "orange", ["Epsilon"] = "black" })
            Assert.Equal(color, catalog.Prototypes.Index<ClueBookPrototype>(book).Color);
    }

    [Fact]
    public void IncompleteAndNonUniqueCardAssignmentsFailAtContentLoad()
    {
        var files = Files();
        files["companion-setups.yml"] = files["companion-setups.yml"].Replace("Gamma: near-stone", "Gamma: near-animal");
        var errors = Assert.Throws<ContentException>(() => new ContentCatalog().Load(files)).Errors;
        Assert.Contains(errors, e => e.Contains("assign exactly one distinct clue"));
        files = Files();
        files["companion-setups.yml"] = files["companion-setups.yml"].Replace("      Gamma: near-stone\n", "");
        Assert.Throws<ContentException>(() => new ContentCatalog().Load(files));
    }

    [Fact]
    public void SeedAndReloadPreserveAssignmentsOpeningAndRandomSequence()
    {
        var (catalog, game, save) = Start();
        var duplicate = Game(catalog); duplicate.Begin(save.Card, save.Players, save.Human, save.Seed);
        var second = JsonSerializer.Deserialize<SessionSave>(duplicate.Export())!;
        Assert.NotEqual(save.SessionId, second.SessionId);
        Assert.Equal(save.Markers, second.Markers);
        Assert.Equal(save.Assignments, second.Assignments);
        Assert.Equal(save.Draws, second.Draws);
        PayCubes(catalog, game, save); game.StartPlay();
        var restored = Game(catalog); restored.Restore(game.Export());
        Assert.Equal(game.Export(), restored.Export());
        var solution = game.Board!.Spaces.Single(s => save.Assignments.Keys.All(b => Matches(catalog, game, save, b, s.Position))).Position;
        game.Search(solution); restored.Search(solution);
        Assert.Equal(SessionPhase.Won, restored.Phase);
        Assert.Equal(game.Export(), restored.Export());
        Assert.Throws<InvalidOperationException>(() => restored.Search(solution));
    }

    [Fact]
    public void QuestionAnswersAreTruthfulAndANoRequiresAnExtraHumanCube()
    {
        var (catalog, game, save) = Start(); Play(catalog, game, save);
        var book = game.Opponents[0];
        var position = game.Board!.Spaces.First(s => !game.HasCube(s.Position) && !Matches(catalog, game, save, book, s.Position)).Position;
        var turn = game.Ask(book, position);
        Assert.Equal(Piece.Cube, Assert.Single(turn.Placements).Piece);
        Assert.Equal(1, game.PendingHumanCubes);
        var before = game.Export();
        Assert.Throws<InvalidOperationException>(() => game.Ask(book, new(0, 0)));
        Assert.Equal(before, game.Export());
        PayCubes(catalog, game, save);
        Assert.Equal(0, game.PendingHumanCubes);
        Assert.Throws<InvalidOperationException>(() => game.Search(position));
    }

    [Fact]
    public void AnAffirmativeQuestionPlacesOneDiscWithoutACubePenalty()
    {
        var (catalog, game, save) = Start(); Play(catalog, game, save);
        var solution = game.Board!.Spaces.Single(s => save.Assignments.Keys.All(b => Matches(catalog, game, save, b, s.Position))).Position;
        var book = game.Opponents[0];
        var turn = game.Ask(book, solution);
        Assert.Equal(Piece.Disc, Assert.Single(turn.Placements).Piece);
        Assert.Equal(0, game.PendingHumanCubes);
        var before = game.Export();
        Assert.Throws<InvalidOperationException>(() => game.Ask(book, solution));
        Assert.Equal(before, game.Export());
    }

    [Fact]
    public void SearchStopsAtFirstNoAndPenalizesOnlyDiscPlacers()
    {
        // Find a seed with a first yes followed by a no; verify hidden later responses never leak.
        for (var seed = 0; seed < 200; seed++)
        {
            var (catalog, game, save) = Start(5, seed); Play(catalog, game, save);
            var books = game.Opponents;
            var space = game.Board!.Spaces.FirstOrDefault(s => !game.HasCube(s.Position) && Matches(catalog, game, save, save.Human, s.Position) &&
                Matches(catalog, game, save, books[0], s.Position) && !Matches(catalog, game, save, books[1], s.Position));
            if (space is null) continue;
            var restored = Game(catalog);
            restored.Restore(game.Export());
            var turn = game.Search(space.Position);
            restored.Search(space.Position);
            Assert.Equal(game.Export(), restored.Export());
            Assert.False(turn.Success);
            Assert.Equal(new[] { save.Human, books[0], books[1] }, turn.Placements.Where(m => m.Position == space.Position).Select(m => m.Book));
            var penalty = Assert.Single(turn.Placements, m => m.Reason == "Failed search penalty");
            Assert.Equal(books[0], penalty.Book);
            Assert.False(Matches(catalog, game, save, penalty.Book, penalty.Position));
            Assert.Equal(1, game.PendingHumanCubes);
            return;
        }
        Assert.Fail("No representative failed search fixture found.");
    }

    [Fact]
    public void IllegalHumanCubeAndSearchLeaveSessionUnchanged()
    {
        var (catalog, game, save) = Start();
        var legalForHuman = game.Board!.Spaces.First(s => Matches(catalog, game, save, save.Human, s.Position)).Position;
        var original = game.Export();
        Assert.Throws<InvalidOperationException>(() => game.PlaceHumanCube(legalForHuman));
        Assert.Equal(original, game.Export());
        Assert.Throws<InvalidOperationException>(() => game.StartPlay());
        Play(catalog, game, save);
        var invalid = game.Board.Spaces.First(s => !Matches(catalog, game, save, save.Human, s.Position)).Position;
        var before = game.Export();
        Assert.Throws<InvalidOperationException>(() => game.Search(invalid));
        Assert.Equal(before, game.Export());
    }

    [Fact]
    public void ContentErrorsAggregateAndFailedImportKeepsUsableCatalog()
    {
        var catalog = Catalog(); var fingerprint = catalog.Fingerprint;
        var files = Files();
        files["bad.yml"] = """
            - type: clue
              id: broken
              rule: TerrainPair
            - type: clueBook
              id: missing-references
              color: blue
              clues: [does-not-exist]
            - type: clue
              id: near-animal
              text: Duplicate
              rule: NearAnimal
              distance: 1
            """;
        var exception = Assert.Throws<ContentException>(() => catalog.Load(files));
        Assert.True(exception.Errors.Count >= 3);
        Assert.Contains(exception.Errors, e => e.Contains("bad.yml"));
        Assert.Contains(exception.Errors, e => e.Contains("does-not-exist"));
        Assert.Equal(fingerprint, catalog.Fingerprint);
        Assert.Single(catalog.Cards);
    }

    [Fact]
    public void MissingBoardReferenceAndInvalidTileAreReported()
    {
        var files = Files();
        files["tiles.yml"] = files["tiles.yml"].Replace("column: 5", "column: 4");
        files["companion-setups.yml"] = files["companion-setups.yml"].Replace("board: companion-board", "board: missing-board");
        var errors = Assert.Throws<ContentException>(() => new ContentCatalog().Load(files)).Errors;
        Assert.Contains(errors, e => e.Contains("18 distinct"));
        Assert.Contains(errors, e => e.Contains("missing-board"));
    }

    [Fact]
    public void ChangedContentAndMalformedSavesAreRejectedWithoutReplacingSession()
    {
        var (catalog, game, save) = Start(); var original = game.Export();
        save.Catalog = "wrong";
        Assert.Throws<InvalidOperationException>(() => game.Restore(JsonSerializer.Serialize(save)));
        Assert.Equal(original, game.Export());
        save.Catalog = catalog.Fingerprint; save.Assignments[save.Human] = "unknown";
        Assert.Throws<InvalidOperationException>(() => game.Restore(JsonSerializer.Serialize(save)));
        Assert.Equal(original, game.Export());
    }
}
