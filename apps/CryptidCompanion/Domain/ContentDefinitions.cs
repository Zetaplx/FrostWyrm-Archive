using FrostWyrm.Archive.Prototypes;
using FrostWyrm.Archive.Serialization;
using FrostWyrm.Math.Hex;

namespace CryptidCompanion.Domain;

public enum Terrain { Water, Forest, Desert, Mountain, Swamp }
public enum Animal { None, Bear, Cougar }
public enum StructureKind { Stone, Shack }
public enum StructureColor { Blue, Green, White, Black }
public enum ClueRule { TerrainPair, NearTerrain, NearAnimal, NearStructure, NearColor }

[DataDefinition]
public readonly record struct Position([property: DataField("row", Required: true)] int Row,
    [property: DataField("column", Required: true)] int Column)
{
    public override string ToString() => $"{(char)('A' + Row)}{Column + 1}";
}

[Prototype("tile")]
public sealed class TilePrototype : IPrototype
{
    [IdDataField] public string ID { get; init; } = "";
    [DataField(Required: true)] public List<CellDefinition> Cells { get; init; } = new();
}

[DataDefinition]
public sealed class CellDefinition
{
    [DataField(Required: true)] public Position Position { get; init; }
    [DataField(Required: true)] public Terrain Terrain { get; init; }
    [DataField] public Animal Animal { get; init; }
}

[DataDefinition]
public sealed class TilePlacement
{
    [DataField(Required: true)] public ProtoId<TilePrototype> Tile { get; init; } = default!;
    [DataField(Required: true)] public Position Origin { get; init; }
    [DataField] public bool Flipped { get; init; }
}

[DataDefinition]
public sealed class StructurePlacement
{
    [DataField(Required: true)] public Position Position { get; init; }
    [DataField(Required: true)] public StructureKind Kind { get; init; }
    [DataField(Required: true)] public StructureColor Color { get; init; }
}

[Prototype("board")]
public sealed class BoardPrototype : IPrototype
{
    [IdDataField] public string ID { get; init; } = "";
    [DataField] public Hex.Orientation Orientation { get; init; } = Hex.Orientation.FlatTop;
    [DataField] public int Rows { get; init; } = 9;
    [DataField] public int Columns { get; init; } = 12;
    [DataField(Required: true)] public List<TilePlacement> Tiles { get; init; } = new();
    [DataField(Required: true)] public List<StructurePlacement> Structures { get; init; } = new();
}

[Prototype("clue")]
public sealed class CluePrototype : IPrototype
{
    [IdDataField] public string ID { get; init; } = "";
    [DataField(Required: true)] public string Text { get; init; } = "";
    [DataField(Required: true)] public ClueRule Rule { get; init; }
    [DataField] public List<Terrain> Terrains { get; init; } = new();
    [DataField] public Animal Animal { get; init; }
    [DataField] public StructureKind Structure { get; init; }
    [DataField] public StructureColor Color { get; init; }
    [DataField] public int Distance { get; init; }
}

[Prototype("clueBook")]
public sealed class ClueBookPrototype : IPrototype
{
    [IdDataField] public string ID { get; init; } = "";
    [DataField(Required: true)] public string Color { get; init; } = "";
    [DataField(Required: true)] public List<ProtoId<CluePrototype>> Clues { get; init; } = new();
}
