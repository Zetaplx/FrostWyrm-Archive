using FrostWyrm.Archive.Serialization;

namespace FrostWyrm.Archive.Prototypes;

/// <summary>
///     The basic form of a YAML data definition. Should contain data which we want serialized and stored in YAML files.
/// </summary>
public interface IPrototype
{
    /// <summary>
    ///     A unique identifier for this particular Prototype.
    /// </summary>
    public string ID { get; init; }
}

/// <summary>
///     A special type of Prototype which may inherit data from one or more parents.<br/><br/>
/// 
///     When multiple parents define the same data, the later parent is prioritized.
/// </summary>
public interface IInheritingPrototype : IPrototype
{
    /// <summary>
    ///     The Prototype IDs for all parent Prototypes.
    /// </summary>
    public IReadOnlyList<string> Parents { get; init; }

    /// <summary>
    ///     If true, this Prototype cannot be instantiated directly and may contain incomplete data definitions. 
    ///     Implies that other Prototypes will use this as a parent.
    /// </summary>
    public bool Abstract { get; init; }
}

/// <summary>
///     Identifies a class as a Prototype to the PrototypeManager. Required for reading YAML using this Prototype type.<br/><br/>
/// 
///     All Prototype definitions should be named in the vein of "EntityPrototype". The "Prototype" portion will be omitted
///     in the YAML serialization, but is required unless a specific name is given to this attribute.
/// </summary>
/// <param name="name">
///     A name override for the class in YAML. If null, will default to the camel-case version of the type name,
///     ignoring the trailing "Prototype" (i.e. EntityPrototype => entity)
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class PrototypeAttribute(string? name = null) : Attribute
{
    /// <summary>
    ///     A name override for the class in YAML. If null, will default to the camel-case version of the type name,
    ///     ignoring the trailing "Prototype" (i.e. EntityPrototype => entity)
    /// </summary>
    public string? Name { get; }= name;
}

/// <summary>
///     Identifies the ID Data Field within a Prototype definition.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class IdDataFieldAttribute(): DataFieldAttribute("id");

/// <summary>
///     Identifies the Abstract Data Field of an inheriting Prototype definition.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class AbstractDataFieldAttribute() : DataFieldAttribute("abstract");

/// <summary>
///     Identifies the Parents Data Field of an inheriting Prototype definition.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ParentsDataFieldAttribute() : DataFieldAttribute("parents");

