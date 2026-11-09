using System.Collections;
using System.Globalization;
using System.Reflection;

using FrostWyrm.Archive.Prototypes;

using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Archive.Serialization;

public interface ISerializationManager : IManager
{
    /// <summary>
    ///     Deserializes a YAML Mapping Node into a Prototype definition.
    /// </summary>
    public IPrototype DeserializePrototype(Type type, YamlMappingNode node);

    /// <summary>
    ///     Deserializes a YAML node into an instantiated object.
    /// </summary>
    public object DeserializeNode(Type type, YamlNode node, Type? customSerializer = null, SerializerPropagationFlags flags = SerializerPropagationFlags.Default);

    /// <summary>
    ///     A generically typed helper to deserialize a YAML node into an instantiated object of a given type.
    /// </summary>
    public T DeserializeNode<T>(YamlNode node, Type? customSerializer = null, SerializerPropagationFlags flags = SerializerPropagationFlags.Default);

    public void RegisterAssemblySerializers(Assembly assembly);
}

public sealed class SerializationManager : ISerializationManager
{
    private Dictionary<(Type nodeType, Type dataType), Type> _defaultSerializers = new();

    #region Deserializers
    /// <inheritdoc/>
    public IPrototype DeserializePrototype(Type type, YamlMappingNode node)
    {
        // Verify everything is in order with the prototype.
        PrototypeManager.ValidatePrototype(type);

        if (type.IsAbstract)
            throw new InvalidOperationException($"Unable to deserialize abstract prototype type \"{type.FullName}\".");

        var instance = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Unable to instantiate prototype type \"{type.FullName}\".");

        var enforceRequired = true;
        if (typeof(IInheritingPrototype).IsAssignableFrom(type) &&
            node.Children.TryGetValue("abstract", out var absNode) &&
            absNode is YamlScalarNode scalar)
            enforceRequired = !bool.Parse(scalar.Value!);

        PopulateDataFields(type, instance, node, enforceRequired);

        return (IPrototype)instance;
    }

    /// <inheritdoc/>
    public object DeserializeNode(Type type, YamlNode node, Type? customSerializer = null, SerializerPropagationFlags flags = SerializerPropagationFlags.Default)
    {
        Type serializerType;
        if (customSerializer is not null && IsCompatibleSerializer(customSerializer, type, node))
            serializerType = customSerializer;
        else if (!TryGetSerializer(node.GetType(), type, out serializerType) || customSerializer is not null && flags != SerializerPropagationFlags.None)
        {
            if (node is YamlScalarNode scalar)
                return DeserializeScalar(type, scalar);

            if (node is YamlSequenceNode sequence)
                return DeserializeSequence(type, sequence, customSerializer, flags);

            if (node is YamlMappingNode mapping)
                return DeserializeMapping(type, mapping, customSerializer, flags);

            throw new NotSupportedException(
                $"Unsupported YAML node type \"{node.GetType().Name}\".");
        }

        var serializer = Activator.CreateInstance(serializerType)
            ?? throw new InvalidOperationException($"Unable to instantiate serializer of type \"{serializerType.FullName}\"");

        return DeserializeWithSerializer(type, node, serializer);
    }

    /// <inheritdoc/>
    public T DeserializeNode<T>(YamlNode node, Type? customSerializer = null, SerializerPropagationFlags flags = SerializerPropagationFlags.Default)
        => (T)DeserializeNode(typeof(T), node, customSerializer, flags);

    /// <summary>
    ///     Deserializes a YAML scalar node into a single scalar value.
    /// </summary>
    private object DeserializeScalar(Type type, YamlScalarNode node)
    {
        var value = node.Value;

        if (type == typeof(string))
            return value ?? string.Empty;

        if (type == typeof(int))
            return int.Parse(value!, CultureInfo.InvariantCulture);

        if (type == typeof(float))
            return float.Parse(value!, CultureInfo.InvariantCulture);

        if (type == typeof(double))
            return double.Parse(value!, CultureInfo.InvariantCulture);

        if (type == typeof(bool))
            return bool.Parse(value!);

        if (type.IsEnum)
            return Enum.Parse(type, value!, ignoreCase: true);

        throw new NotSupportedException($"Deserialization of type \"{type.FullName}\" is not supported.");
    }

    /// <summary>
    ///     Deserializes a YAML sequence node into an Array or IList. Currently no support for other enumerable data types.
    /// </summary>
    private object DeserializeSequence(
        Type type,
        YamlSequenceNode node,
        Type? customSerializer = null,
        SerializerPropagationFlags flags = SerializerPropagationFlags.Default)
    {
        if (!TryGetSequenceElementType(type, out var elementType))
            throw new InvalidOperationException($"Unable to resolve element type for \"{type.FullName}\"");

        if (type.IsArray)
        {
            var array = Array.CreateInstance(elementType, node.Children.Count);

            for (int i = 0; i < node.Children.Count; i++)
            {
                var value = DeserializeNode(
                    elementType, node.Children[i],
                    flags.HasFlag(SerializerPropagationFlags.Sequence) ? customSerializer : null,
                    GetNextFlags(flags));
                array.SetValue(value, i);
            }

            return array;
        }

        var result = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Unable to instantiate type \"{type.FullName}\".");

        if (result is not IList list)
            throw new InvalidOperationException(
                $"Sequence type \"{type.FullName}\" does not support indexed collection insertion.");

        foreach (var child in node.Children)
        {
            list.Add(DeserializeNode(
                elementType, child,
                flags.HasFlag(SerializerPropagationFlags.Sequence) ? customSerializer : null,
                GetNextFlags(flags)));
        }

        return result;
    }

    /// <summary>
    ///     Routes deserialization of mapping nodes to Data Definitions or Dictionaries depending on type.
    private object DeserializeMapping(Type type, YamlMappingNode node, Type? customSerializer = null, SerializerPropagationFlags flags = SerializerPropagationFlags.Default)
    {
        if (IsDataDefinition(type))
            return DeserializeDataDefinition(type, node, customSerializer, flags);

        if (TryGetDictionaryTypes(type, out var keyType, out var valueType))
            return DeserializeDictionary(type, keyType, valueType, node, customSerializer, flags);

        throw new InvalidOperationException($"Unsupported mapping node type \"${type.FullName}\".");
    }

    /// <summary>
    ///     Deserializes a mapping node into a dictionary with a specified key and value type.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private object DeserializeDictionary(
        Type type,
        Type keyType,
        Type valueType,
        YamlMappingNode node,
        Type? customSerializer = null,
        SerializerPropagationFlags flags = SerializerPropagationFlags.Default)
    {
        var result = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Unable to instantiate dictionary type \"{type.FullName}\".");

        if (result is not IDictionary dictionary)
            throw new InvalidOperationException($"Dictionary type \"{type.FullName}\" does not implement IDictionary.");

        foreach (var (keyNode, valueNode) in node.Children)
        {
            var keySerializer = flags.HasFlag(SerializerPropagationFlags.DictionaryKeys) ? customSerializer : null;
            var valueSerializer = flags.HasFlag(SerializerPropagationFlags.DictionaryValues) ? customSerializer : null;
            var key = DeserializeNode(keyType, keyNode, keySerializer, GetNextFlags(flags));
            var value = DeserializeNode(valueType, valueNode, valueSerializer, GetNextFlags(flags));
            dictionary.Add(key, value);
        }
        return dictionary;
    }

    /// <summary>
    ///     Deserializes a YAML mapping node into a Data Definition. Only populates fields and properties marked as Data Fields.
    /// </summary>
    private object DeserializeDataDefinition(
        Type type,
        YamlMappingNode node,
        Type? customSerializer = null,
        SerializerPropagationFlags flags = SerializerPropagationFlags.Default)
    {
        var instance = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Unable to instantiate data definition \"{type.FullName}\"");

        PopulateDataFields(type, instance, node, true, customSerializer, flags);

        return instance;
    }

    public object DeserializeWithSerializer(Type dataType, YamlNode node, object serializer)
    {
        var serializerInterface = typeof(ISerializer<,>).MakeGenericType(node.GetType(), dataType);

        var validate = serializerInterface.GetMethod("Validate")!;
        var deserialize = serializerInterface.GetMethod("Deserialize")!;

        if (!(bool)validate.Invoke(serializer, [node])!)
            throw new InvalidOperationException($"Invalid YAML value for type \"{dataType.FullName}\".");

        return deserialize.Invoke(serializer, [node])!;
    }
    #endregion

    /// <summary>
    ///     Populates the DataFields of an instantiated type with the values from a YAML Mapping Node.
    ///     Recursively calles <see cref="DeserializeNode"/> to allow nesting of Data Definitions and the use of custom deserializers.
    /// </summary>
    private void PopulateDataFields(
        Type type, object instance, 
        YamlMappingNode node,
         bool enforceRequired, 
         Type? customSerializer = null, 
         SerializerPropagationFlags flags = SerializerPropagationFlags.Default)
    {
        const BindingFlags bindingFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        foreach (var member in type.GetMembers(bindingFlags))
        {
            if (member is not FieldInfo && member is not PropertyInfo)
                continue; // We do not serializer/deserialize non-field/property members.

            var attribute = member.GetCustomAttribute<DataFieldAttribute>();
            if (attribute == null)
                continue; // Do not populate non-datafield members.

            // Use override name if available, otherwise convert member name to camel case.
            var yamlName = attribute.Name ?? ToCamelCase(member.Name);

            if (!node.Children.TryGetValue(new YamlScalarNode(yamlName), out var childNode))
            {
                if (attribute.Required && enforceRequired)
                    throw new InvalidOperationException($"Missing requried field \"{yamlName}\" in {type.Name}.");

                continue; // No field matching member name.
            }

            var (serializer, newFlags) =    flags.HasFlag(SerializerPropagationFlags.DataFieldsOverrideExisting) ? (customSerializer, GetNextFlags(flags)) :
                                flags.HasFlag(SerializerPropagationFlags.DataFields) && attribute.CustomSerializer is null ? (customSerializer, GetNextFlags(flags)) :
                                (attribute.CustomSerializer, attribute.Propogate);

            PopulateMember(instance, member, childNode, serializer, newFlags);
        }
    }

    private void PopulateMember(
        object instance, 
        MemberInfo member, 
        YamlNode node, 
        Type? customSerializer,
        SerializerPropagationFlags flags = SerializerPropagationFlags.Default)
    {
        switch (member)
        {
            case FieldInfo field:
                var value = DeserializeNode(field.FieldType, node, customSerializer, flags);
                field.SetValue(instance, value);
                break;

            case PropertyInfo property:
                value = DeserializeNode(property.PropertyType, node, customSerializer, flags);
                if (property.GetSetMethod(nonPublic: true) is null)
                    throw new InvalidOperationException($"Attempted to set property \"{member.Name}\" without a valid setter.");
                property.SetValue(instance, value);
                break;

            default:
                throw new InvalidOperationException($"Unsupported member type. Unable to populate.");
        }
    }

    public void RegisterAssemblySerializers(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsDefined(typeof(DefaultTypeSerializerAttribute), false))
                continue;

            if (!type.IsClass || type.IsAbstract)
                continue;

            var serializerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISerializer<,>))
                .ToArray();

            if (serializerInterfaces.Length == 0)
                throw new InvalidOperationException($"Serializer \"{type.FullName}\" does not implement ISerializer<TNode, TData>.");

            foreach (var serializerInterface in serializerInterfaces)
            {
                var args = serializerInterface.GetGenericArguments();

                var nodeType = args[0];
                var dataType = args[1];

                if (dataType.ContainsGenericParameters)
                {
                    if (!type.IsGenericTypeDefinition)
                        throw new InvalidOperationException($"Serializer \"{type.FullName}\" targets an open generic data type" +
                                                            $"\"{dataType.FullName} but is not an open generic serializer.");

                    dataType = dataType.GetGenericTypeDefinition();
                }

                if (!_defaultSerializers.TryAdd((nodeType, dataType), type))
                    throw new InvalidOperationException($"Duplicate serializer for YAML node \"{nodeType.FullName}\" and data type \"{dataType.FullName}\".");
            }
        }
    }

    private bool TryGetSerializer(Type nodeType, Type dataType, out Type serializerType)
    {
        serializerType = default!;

        // Exact Match
        if (_defaultSerializers.TryGetValue((nodeType, dataType), out var result))
        {
            serializerType = result;
            return true;
        }

        if (!dataType.IsGenericType)
            return false;
        var genericType = dataType.GetGenericTypeDefinition();

        if (!_defaultSerializers.TryGetValue((nodeType, genericType), out result))
            return false;

        serializerType = result.MakeGenericType(dataType.GetGenericArguments());
        return true;
    }

    private bool IsCompatibleSerializer(Type serializerType, Type dataType, YamlNode node)
    {
        var expectedInterface = typeof(ISerializer<,>).MakeGenericType(node.GetType(), dataType);

        return expectedInterface.IsAssignableFrom(serializerType);
    }

    #region Simple Helpers
    /// <summary>
    ///     Returns whether a given type is a data definition through the presence of a DataDefinition attribute or an implicite definition through its parents.
    /// </summary>
    private bool IsDataDefinition(Type type)
    {
        if (type.IsDefined(typeof(DataDefinitionAttribute), false))
            return true;

        if (type.IsDefined(typeof(ImplicitDataDefinitionForInheritorsAttribute), true))
            return true;

        return false;
    }

    /// <summary>
    ///     Takes a string and converts it to camel case by making the first letter lowercase-invariant.
    /// </summary>
    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return char.ToLowerInvariant(name[0]) + name[1..]; // TODO: A better camel case converter...
    }

    /// <summary>
    ///     Attempts to retrieve the element type of a sequence type.
    /// </summary>
    private static bool TryGetSequenceElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();

            if (genericType == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = default!;
        return false;
    }

    private bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var args = type.GetGenericArguments();
            keyType = args[0];
            valueType = args[1];
            return true;
        }

        keyType = default!;
        valueType = default!;
        return false;
    }

    private SerializerPropagationFlags GetNextFlags(SerializerPropagationFlags currentFlags)
    {
        if (currentFlags.HasFlag(SerializerPropagationFlags.RecusivePropagation))
            return currentFlags;
        return SerializerPropagationFlags.None;
    }
    #endregion
}