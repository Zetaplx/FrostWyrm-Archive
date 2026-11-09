using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;

using FrostWyrm.Archive.DependencyInjection;
using FrostWyrm.Archive.Serialization;

using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Archive.Prototypes;

public interface IPrototypeManager : IManager
{
    /// <summary>
    ///     Retrieves a Prototype with a given type and ID. May be configured to search for inheritence instead of an exact type match.
    /// </summary>
    public IPrototype Index(Type type, string id, bool allowInheritanceLookup = true);
    /// <summary>
    ///     Retrieves a typed Prototype with a given ID. May be configured to search for inheritence instead of an exact type match.
    /// </summary>
    public T Index<T>(ProtoId<T> id, bool allowInheritanceLookup = true) where T : IPrototype;

    /// <summary>
    ///     Returns all prototypes of a given type which meet a given condition. If condition is null, all prototypes of that type are returned.
    /// </summary>
    public IEnumerable<T> EnumeratePrototypes<T>(Func<T, bool>? conditional = null) where T : IPrototype;

    /// <summary>
    ///     Registers all Prototypes marked with the Prototype attribute within a given assembly to the registry.
    /// </summary>
    /// <param name="assembly"></param>
    public void RegisterAssemblyPrototypes(Assembly assembly);

    /// <summary>
    ///     Loads all prototypes from a provided YAML sequence node into the registery.
    /// </summary>
    public void LoadPrototypes(YamlSequenceNode node);
    /// <summary>
    ///     Loads all prototypes from a provided TextReader into the registry.
    /// </summary>
    public void LoadPrototypes(TextReader reader);
    /// <summary>
    ///     Loads all prototypes from a provided file path into the registry. Should be replaced when the ResourceManager is completed.
    /// </summary>
    public void LoadPrototypes(string filePath);
}

public sealed class PrototypeManager : IPrototypeManager
{
    [Dependency] private readonly ISerializationManager _serialization = default!;

    private readonly Dictionary<string, Type> _prototypeTypesByName = new();
    private readonly Dictionary<Type, string> _prototypeNamesByType = new();
    private readonly PrototypeRegistry _registry = new();

    /// <inheritdoc/>
    public IPrototype Index(Type type, string id, bool allowInheritanceLookup = true)
    {
        if (!_registry.TryGetById(type, id, out var prototype, allowInheritanceLookup))
            throw new InvalidOperationException($"Unable to resolve prototype of type \"{type.FullName}\" and ID \"{id}\"");

        return prototype;
    }
    /// <inheritdoc/>
    public T Index<T>(ProtoId<T> id, bool allowInheritanceLookup = true) where T : IPrototype
        => (T)Index(typeof(T), id, allowInheritanceLookup);

    /// <inheritdoc/>
    public IEnumerable<T> EnumeratePrototypes<T>(Func<T, bool>? conditional = null) where T : IPrototype
    {
        conditional ??= _ => true;

        foreach (var (type, prototype) in _registry.GetAll())
        {
            if (typeof(T).IsAssignableFrom(type))
            {
                var t = (T)prototype;
                if (!conditional(t))
                    continue;

                yield return t;
            }
        }
    }

    /// <inheritdoc/>
    public void RegisterAssemblyPrototypes(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            var attribute = type.GetCustomAttribute<PrototypeAttribute>();
            if (attribute == null)
                continue;

            ValidatePrototype(type);

            if (type.IsAbstract)
                continue;

            var name = attribute.Name ?? GetDefaultPrototypeName(type);

            if (!_prototypeTypesByName.TryAdd(name, type))
                throw new InvalidOperationException($"Prototype name \"{name}\" already registered!");

            if (!_prototypeNamesByType.TryAdd(type, name))
                throw new InvalidOperationException($"Prototype type \"{type}\" already registered!");
        }
    }

    public void ApplyPrototypeInheritence()
    {
        // TODO: Get inhereting to actually... work.
    }

    #region Load Prototypes
    /// <inheritdoc/>
    public void LoadPrototypes(YamlSequenceNode root)
    {
        if (_prototypeNamesByType.Count <= 0)
            throw new InvalidOperationException($"PrototypeManager has yet to be initialized with any prototypes.");

        foreach (var node in root.Children)
        {
            if (node is not YamlMappingNode mapping)
                throw new InvalidOperationException($"Top-level prototype entries must be YAML mappings.");
            
            if (!mapping.Children.TryGetValue(new YamlScalarNode("type"), out var typeNode) ||
                typeNode is not YamlScalarNode typeScalar ||
                string.IsNullOrWhiteSpace(typeScalar.Value))
                throw new InvalidOperationException($"Prototype definition is missing required \"type\" field.");
            
            if (!_prototypeTypesByName.TryGetValue(typeScalar.Value, out var prototypeType))
                throw new InvalidOperationException($"Unkown prototype type \"{typeScalar.Value}\".");
            
            var prototype = _serialization.DeserializePrototype(prototypeType, mapping);

            _registry.RegisterPrototype(prototype);
        }
    }

    /// <inheritdoc/>
    public void LoadPrototypes(TextReader reader)
    {
        var yaml = new YamlStream();
        yaml.Load(reader);

        foreach (var document in yaml.Documents)
        {
            if (document.RootNode is not YamlSequenceNode root)
                throw new InvalidOperationException($"Prototype YAML documents must have a sequence root.");
            
            LoadPrototypes(root);
        }
    }

    /// <inheritdoc/>
    public void LoadPrototypes(string filePath)
    {
        using var reader = File.OpenText(filePath);
        LoadPrototypes(reader);    
    }
    #endregion
    
    /// <summary>
    ///     Validates the state of a given Prototype type, ensuring that all required fields and features are present.
    /// </summary>
    public static void ValidatePrototype(Type type)
    {
        if (!typeof(IPrototype).IsAssignableFrom(type))
            throw new InvalidOperationException($"Prototype type \"{type.FullName}\" does not implement IPrototype.");

        bool hasIdField = false;
        bool hasParentField = false;
        bool hasAbstractField = false;
        bool isInheriting = typeof(IInheritingPrototype).IsAssignableFrom(type);

        foreach (var field in type.GetFields())
        {
            if (field.IsDefined(typeof(IdDataFieldAttribute), true))
                if (hasIdField)
                    throw new InvalidOperationException($"Duplicate ID data fields in \"{type.FullName}\".");
                else
                    hasIdField = true;

            if (field.IsDefined(typeof(ParentsDataFieldAttribute), true))
                if (hasParentField)
                    throw new InvalidOperationException($"Duplicate Parents data fields in \"{type.FullName}\".");
                else
                    hasParentField = true;

            if (field.IsDefined(typeof(AbstractDataFieldAttribute), true))
                if (hasAbstractField)
                    throw new InvalidOperationException($"Duplicate Abstract data fields in \"{type.FullName}\".");
                else
                    hasAbstractField = true;
        }

        foreach (var property in type.GetProperties())
        {
            if (property.IsDefined(typeof(IdDataFieldAttribute), true))
                if (hasIdField)
                    throw new InvalidOperationException($"Duplicate ID data fields in \"{type.FullName}\".");
                else
                    hasIdField = true;

            if (property.IsDefined(typeof(ParentsDataFieldAttribute), true))
                if (hasParentField)
                    throw new InvalidOperationException($"Duplicate Parents data fields in \"{type.FullName}\".");
                else
                    hasParentField = true;

            if (property.IsDefined(typeof(AbstractDataFieldAttribute), true))
                if (hasAbstractField)
                    throw new InvalidOperationException($"Duplicate Abstract data fields in \"{type.FullName}\".");
                else
                    hasAbstractField = true;
        }

        if (!hasIdField)
            throw new InvalidOperationException($"Prototype \"{type.FullName}\" must specify an ID with the IdDataField attribute.");
        if (isInheriting)
        {
            if (!hasParentField)
                throw new InvalidOperationException($"Inheriting Prototype \"{type.FullName}\" must specify a Parent data field with the ParentsDataField attribute.");

            if (!hasAbstractField)
                throw new InvalidOperationException($"Inheriting Prototype \"{type.FullName}\" must specify an Abstract data field with the AbstractDataField attribute.");
        }
    }
    
    /// <summary>
    ///     Gets the default name of a prototype based on its type name.
    /// </summary>
    private static string GetDefaultPrototypeName(Type type)
    {
        const string suffix = "Prototype";

        var name = type.Name;
        if (name.EndsWith(suffix))
            name = name[..^suffix.Length];

        if (name.Length == 0)
            throw new InvalidOperationException($"Unable to derive prototype name from \"{type.FullName}\".");

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}

/// <summary>
///     A storage structure for Prototypes allowing for rapid querying of Prototypes by ID/type/etc.
/// </summary>
public sealed class PrototypeRegistry
{
    private readonly Dictionary<ProtoId, IPrototype> _byId = new();
    private readonly Dictionary<Type, List<IPrototype>> _byType = new();
    private readonly Dictionary<ProtoId, IReadOnlyList<string>> _prototypeParents = new();

    /// <summary>
    ///     Attempts to retrieve prototype with a given id and type from the registry. May be configured to include inheritence relations in search (on by default).
    /// </summary>
    public bool TryGetById(Type type, string id, out IPrototype prototype, bool allowInheritanceLookup = true)
    {
        prototype = default!;
        if (_byId.TryGetValue((type, id), out var proto))
        {
            prototype = proto;
            return true;
        }
        if (!allowInheritanceLookup)
            return false;

        bool found = false;
        foreach (var ((protoType, protoId), protoValue) in _byId)
        {
            if (protoId != id ||
                !type.IsAssignableFrom(protoType))
                continue;

            if (found)
                throw new InvalidOperationException($"Multiple with id \"{id}\" satisfy \"{type.FullName}\".");

            prototype = protoValue;
            found = true;
        }

        return found;
    }

    /// <summary>
    ///     Tries to retrieve a typed prototype with a given ID from the registry. May be configured to include inheritence relations in search (on by default).
    /// </summary>
    public bool TryGetById<T>(string id, out T prototype, bool allowInheritanceLookup = true) where T : IPrototype
    {
        prototype = default!;
        if (!TryGetById(typeof(T), id, out var untyped, allowInheritanceLookup))
            return false;

        prototype = (T)untyped;
        return true;
    }

    public void RegisterPrototype(IPrototype prototype)
    {
        var type = prototype.GetType();
        _byId.Add((type, prototype.ID), prototype);

        if (_byType.TryGetValue(type, out var types))
            types.Add(prototype);
        else _byType.Add(type, new() { prototype });

        if (prototype is IInheritingPrototype inheriting)
        {
            _prototypeParents.Add((type, prototype.ID), inheriting.Parents);
        }
    }

    public IEnumerable<(Type Type, IPrototype Prototype)> GetAll()
    {
        foreach (var ((type, id), prototype) in _byId)
            yield return (type, prototype);
    }

    public IEnumerable<(Type Type, IPrototype Prototype, IReadOnlyList<string> Parents)> GetPrototypeParents()
    {
        foreach (var ((type, id), list) in _prototypeParents)
        {
            if (!TryGetById(type, id, out var proto, false))
                continue; // Wha...?
            
            yield return (type, proto, list);
        }
    }

    public void Reset()
    {
        _byId.Clear();
        _prototypeParents.Clear();
        _byType.Clear();
    }
}