using FrostWyrm.Archive.Prototypes;
using FrostWyrm.Math.Hex;

namespace CryptidCompanion.Domain;

public sealed record BoardSpace(Position Position, AxialHex Hex, Terrain Terrain, Animal Animal,
    string Tile, StructurePlacement? Structure);

public sealed class BoardState
{
    private readonly Dictionary<Position, BoardSpace> _spaces;
    public IReadOnlyCollection<BoardSpace> Spaces => _spaces.Values;
    public BoardPrototype Definition { get; }
    public BoardSpace this[Position position] => _spaces.TryGetValue(position, out var space)
        ? space : throw new ArgumentException($"{position} is outside this board.");

    public BoardState(BoardPrototype definition, IPrototypeManager prototypes)
    {
        Definition = definition;
        _spaces = new();
        foreach (var placement in definition.Tiles)
        {
            var tile = prototypes.Index(placement.Tile);
            foreach (var cell in tile.Cells)
            {
                var row = placement.Origin.Row + (placement.Flipped ? 2 - cell.Position.Row : cell.Position.Row);
                var column = placement.Origin.Column + (placement.Flipped ? 5 - cell.Position.Column : cell.Position.Column);
                var position = new Position(row, column);
                if (row < 0 || column < 0 || row >= definition.Rows || column >= definition.Columns)
                    throw new InvalidOperationException($"Board {definition.ID}: tile {tile.ID} extends outside the board at {position}.");
                if (!_spaces.TryAdd(position, new(position, HexCoordinates.FromOffset(row, column, definition.Orientation),
                        cell.Terrain, cell.Animal, tile.ID, definition.Structures.SingleOrDefault(s => s.Position == position))))
                    throw new InvalidOperationException($"Board {definition.ID}: tiles overlap at {position}.");
            }
        }
        if (_spaces.Count != definition.Rows * definition.Columns)
            throw new InvalidOperationException($"Board {definition.ID}: tiles cover {_spaces.Count} of {definition.Rows * definition.Columns} spaces.");
    }

    public IEnumerable<BoardSpace> Within(Position origin, int distance)
    {
        var hex = this[origin].Hex;
        return Spaces.Where(space => hex.DistanceTo(space.Hex) <= distance);
    }

    public IEnumerable<BoardSpace> Adjacent(Position origin)
        => Within(origin, 1).Where(space => space.Position != origin);

    public bool Near(Position position, int distance, Func<BoardSpace, bool> feature)
        => Within(position, distance).Any(feature);
}

public static class ClueEvaluator
{
    public static bool Matches(CluePrototype clue, BoardState board, Position position)
        => clue.Rule switch
        {
            ClueRule.TerrainPair => clue.Terrains.Contains(board[position].Terrain),
            ClueRule.NearTerrain => board.Near(position, clue.Distance, s => clue.Terrains.Contains(s.Terrain)),
            ClueRule.NearAnimal => board.Near(position, clue.Distance,
                s => s.Animal != Animal.None && (clue.Animal == Animal.None || clue.Animal == s.Animal)),
            ClueRule.NearStructure => board.Near(position, clue.Distance, s => s.Structure?.Kind == clue.Structure),
            ClueRule.NearColor => board.Near(position, clue.Distance, s => s.Structure?.Color == clue.Color),
            _ => throw new InvalidOperationException($"Unknown rule for clue {clue.ID}.")
        };

    public static HashSet<Position> Evaluate(CluePrototype clue, BoardState board)
        => board.Spaces.Where(s => Matches(clue, board, s.Position)).Select(s => s.Position).ToHashSet();
}
