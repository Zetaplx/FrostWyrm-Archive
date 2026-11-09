using FrostWyrm.Archive.Prototypes;
using FrostWyrm.Archive.Serialization;
using CryptidCompanion.Domain;

namespace CryptidCompanion;

[Prototype("setupCard")]
public sealed class SetupCardPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; init; } = string.Empty;

    [DataField] public string Name { get; init; } = "";
    [DataField] public string Source { get; init; } = "";
    [DataField] public ProtoId<BoardPrototype>? Board { get; init; }

    [DataField("setups", Required: true)]
    public List<PlayerSetup> Setups { get; init; } = new();
}

[DataDefinition]
public sealed class PlayerSetup
{
    [DataField("playerCount", Required: true)]
    public int PlayerCount { get; init; }

    [DataField("clues")]
    public Dictionary<string, int> Clues { get; init; } = new();

    [DataField] public List<ProtoId<ClueBookPrototype>> Books { get; init; } = new();

    // Optional restrictions for a printed setup; absent entries use the book's full Normal Mode pool.
    [DataField] public Dictionary<string, List<ProtoId<CluePrototype>>> Candidates { get; init; } = new();
}
