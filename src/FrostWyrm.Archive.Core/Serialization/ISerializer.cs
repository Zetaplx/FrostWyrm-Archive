using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Archive.Serialization;

public interface ISerializer<TNode, TData> where TNode : YamlNode
{
    public bool Validate(TNode node);
    public TData Deserialize(TNode node);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DefaultTypeSerializerAttribute : Attribute;
