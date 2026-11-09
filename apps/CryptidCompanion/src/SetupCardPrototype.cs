using FrostWyrm.Archive.Prototypes;
using FrostWyrm.Archive.Serialization;

namespace CryptidCompanion;

[Prototype("setupCard")]
public sealed class SetupCardPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; init; } = string.Empty;

    [DataField("setups", Required: true)]
    public List<PlayerSetup> Setups { get; init; } = new();
}

[DataDefinition]
public sealed class PlayerSetup
{
    [DataField("playerCount", Required: true)]
    public int PlayerCount { get; init; }

    [DataField("clues", Required: true)]
    public Dictionary<string, int> Clues { get; init; } = new();
}
