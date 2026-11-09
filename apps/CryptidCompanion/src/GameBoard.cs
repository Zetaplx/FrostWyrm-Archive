using FrostWyrm.Archive.Serialization;
using FrostWyrm.Math.Hex;

namespace CryptidCompanion;

public sealed class GameBoard
{
    
}

[DataDefinition]
public sealed class MapTile
{
    [DataField("tiles")]
    private Dictionary<Hex, SpaceData> _tiles = new();
}

[DataDefinition]
public sealed class SpaceData
{
    
}