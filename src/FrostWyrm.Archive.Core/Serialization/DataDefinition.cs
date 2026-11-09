namespace FrostWyrm.Archive.Serialization;

/// <summary>
///     Identifies a class or struct as containing data relevant to the YAML serializer.
///     Only data types with this attribute will be serialized/deserialized.
/// </summary>
/// <param name="name">An optional name override for the definition in yaml. If null, will default to the type name in camel-case. </param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class DataDefinitionAttribute(string? name = null) : Attribute
{
    /// <summary>
    ///     An optional name override for the definition in yaml. If null, will default to the type name in camel-case. 
    /// </summary>
    public string? Name { get; } = name;
}

/// <summary>
///     Marks this and any inheretors of this type as a Data Definition. Data Definitions identified in this way
///     cannot use name overrides.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ImplicitDataDefinitionForInheritorsAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
/// <summary>
///     Identifies a field or property in a Data Definition which should be serialized in YAML constructions of the definition.
/// </summary>
/// <param name="name">An optional name override for the member in yaml. If null, will default to the member name in camel-case. </param>
public class DataFieldAttribute(string? Name = null, bool Required = false, Type? CustomSerializer = null, SerializerPropagationFlags Propogate = SerializerPropagationFlags.Default) : Attribute
{
    /// <summary>
    ///     An optional name override for the member in yaml. If null, will default to the field member in camel-case. 
    /// </summary>
    public string? Name { get; } = Name;

    /// <summary>
    ///     Identifies a field that must have a definition in any YAML Prototype defining the Data Definition this is contained in.
    ///     Any field without this flag should contain a default value in the Data Definition.
    /// </summary>
    public bool Required { get; } = Required;

    // TODO: Make this do something...
    /// <summary>
    ///     When assigned and not null, data field will attempt to use the provided custom serializer.<br/><br/>
    /// </summary>
    public Type? CustomSerializer = CustomSerializer;

    /// <summary>
    ///     If true, a defined scalar serializer may be used to effect the children of the node.
    ///     I.e. if placed on a List<TDataType>, and a ISerializer<YamlScalarNode, TDataType> is used,
    ///     the serializer will be used on all elements of the list, if possible.
    /// </summary>
    public SerializerPropagationFlags Propogate = Propogate;
}

[Flags]
public enum SerializerPropagationFlags : byte
{
    None = 0,
    Sequence = 1 << 0,
    DictionaryKeys = 1 << 1,
    DictionaryValues = 1 << 2,
    DataFields = 1 << 3,
    DataFieldsOverrideExisting = 1 << 4,

    RecusivePropagation = 1 << 5,

    Default = Sequence | DictionaryValues | DataFields
}