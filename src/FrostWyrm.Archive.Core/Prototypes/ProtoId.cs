using FrostWyrm.Archive.Serialization;

using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Archive.Prototypes;

public class ProtoId
{
    public Type Type { get; } = default!;
    public string ID { get; } = default!;

    public ProtoId(Type type, string id)
    {
        Type = type;
        ID = id;
    }

    public static implicit operator ProtoId((Type type, string id) tuple) => new ProtoId(tuple.type, tuple.id);
    public static implicit operator string(ProtoId protoId) => protoId.ID;
    public void Deconstruct(out Type type, out string id)
    {
        type = Type;
        id = ID;
    }

    public bool IsValid() => !string.IsNullOrWhiteSpace(ID);

    public override bool Equals(object? obj)
        => IsValid() && obj is ProtoId protoId && protoId.Type == Type && protoId.ID == ID;
    public override int GetHashCode() => (Type, ID).GetHashCode();
}

public sealed class ProtoId<T> : ProtoId where T : IPrototype
{
    public ProtoId(string id) : base(typeof(T), id) { }

    public static implicit operator ProtoId<T>(string id) => new ProtoId<T>(id);
    public static implicit operator string(ProtoId<T> protoId) => protoId.ID;
}

[DefaultTypeSerializer]
public sealed class ProtoIdSerializer<T> : ISerializer<YamlScalarNode, ProtoId<T>> where T : IPrototype
{
    public ProtoId<T> Deserialize(YamlScalarNode node)
        => new ProtoId<T>(node.Value!);

    public bool Validate(YamlScalarNode node)
        => node.Value is not null; // Its parsing as a string, if its a string, for now, its valid.
}