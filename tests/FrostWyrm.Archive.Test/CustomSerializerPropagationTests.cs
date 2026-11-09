using FrostWyrm.Archive.Serialization;
using FrostWyrm.Archive.Prototypes;
using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Archive.Tests;

public sealed class CustomSerializerPropagationTests
{
    private static SerializationManager CreateManager()
    {
        var manager = new SerializationManager();
        manager.RegisterAssemblySerializers(typeof(ProtoIdSerializer<>).Assembly);
        return manager;
    }

    private static YamlNode Parse(string yaml)
    {
        using var reader = new StringReader(yaml);
        var stream = new YamlStream();
        stream.Load(reader);
        return stream.Documents.Single().RootNode;
    }

    // Deliberately distinct results make it obvious which serializer was selected.
    public sealed class AddHundredSerializer : ISerializer<YamlScalarNode, int>
    {
        public bool Validate(YamlScalarNode node) => int.TryParse(node.Value, out _);
        public int Deserialize(YamlScalarNode node) => int.Parse(node.Value!) + 100;
    }

    public sealed class AddThousandSerializer : ISerializer<YamlScalarNode, int>
    {
        public bool Validate(YamlScalarNode node) => int.TryParse(node.Value, out _);
        public int Deserialize(YamlScalarNode node) => int.Parse(node.Value!) + 1000;
    }

    public sealed class PrefixProtoIdSerializer : ISerializer<YamlScalarNode, ProtoId<DefaultSerializerTests.TestPrototype>>
    {
        public bool Validate(YamlScalarNode node) => node.Value is not null;
        public ProtoId<DefaultSerializerTests.TestPrototype> Deserialize(YamlScalarNode node)
            => new("custom-" + node.Value);
    }

    [Fact]
    public void DirectCustomSerializer_TakesPrecedenceOverBuiltIn()
    {
        var result = CreateManager().DeserializeNode<int>(
            new YamlScalarNode("7"), typeof(AddHundredSerializer),
            SerializerPropagationFlags.None);

        Assert.Equal(107, result);
    }

    [Fact]
    public void DirectCustomSerializer_TakesPrecedenceOverRegisteredDefault()
    {
        var result = CreateManager().DeserializeNode<ProtoId<DefaultSerializerTests.TestPrototype>>(
            new YamlScalarNode("alpha"), typeof(PrefixProtoIdSerializer),
            SerializerPropagationFlags.None);

        Assert.Equal("custom-alpha", result.ID);
    }

    [Fact]
    public void SequenceFlag_PropagatesToListElements()
    {
        var result = CreateManager().DeserializeNode<List<int>>(
            Parse("[1, 2]"), typeof(AddHundredSerializer),
            SerializerPropagationFlags.Sequence);

        Assert.Equal(new[] { 101, 102 }, result);
    }

    [Fact]
    public void SequenceFlag_PropagatesToArrayElements()
    {
        var result = CreateManager().DeserializeNode<int[]>(
            Parse("[1, 2]"), typeof(AddHundredSerializer),
            SerializerPropagationFlags.Sequence);

        Assert.Equal(new[] { 101, 102 }, result);
    }

    [Fact]
    public void NoSequenceFlag_DoesNotPropagateToElements()
    {
        var result = CreateManager().DeserializeNode<List<int>>(
            Parse("[1, 2]"), typeof(AddHundredSerializer),
            SerializerPropagationFlags.None);

        Assert.Equal(new[] { 1, 2 }, result);
    }

    [Fact]
    public void SequenceFlag_WithoutRecursivePropagation_OnlyCrossesOneBoundary()
    {
        var result = CreateManager().DeserializeNode<List<List<int>>>(
            Parse("[[1, 2], [3]]"), typeof(AddHundredSerializer),
            SerializerPropagationFlags.Sequence);

        Assert.Equal(new[] { 1, 2 }, result[0]);
        Assert.Equal(new[] { 3 }, result[1]);
    }

    [Fact]
    public void SequenceFlag_WithRecursivePropagation_ReachesNestedScalars()
    {
        var flags = SerializerPropagationFlags.Sequence |
                    SerializerPropagationFlags.RecusivePropagation;

        var result = CreateManager().DeserializeNode<List<List<int>>>(
            Parse("[[1, 2], [3]]"), typeof(AddHundredSerializer), flags);

        Assert.Equal(new[] { 101, 102 }, result[0]);
        Assert.Equal(new[] { 103 }, result[1]);
    }

    [Fact]
    public void DictionaryKeysFlag_OnlyTransformsKeys()
    {
        var result = CreateManager().DeserializeNode<Dictionary<int, int>>(
            Parse("{1: 2}"), typeof(AddHundredSerializer),
            SerializerPropagationFlags.DictionaryKeys);

        Assert.Equal(2, result[101]);
    }

    [Fact]
    public void DictionaryValuesFlag_OnlyTransformsValues()
    {
        var result = CreateManager().DeserializeNode<Dictionary<int, int>>(
            Parse("{1: 2}"), typeof(AddHundredSerializer),
            SerializerPropagationFlags.DictionaryValues);

        Assert.Equal(102, result[1]);
    }

    [Fact]
    public void DictionaryKeysAndValuesFlags_TransformBoth()
    {
        var flags = SerializerPropagationFlags.DictionaryKeys |
                    SerializerPropagationFlags.DictionaryValues;

        var result = CreateManager().DeserializeNode<Dictionary<int, int>>(
            Parse("{1: 2}"), typeof(AddHundredSerializer), flags);

        Assert.Equal(102, result[101]);
    }

    [Fact]
    public void RecursivePropagation_CrossesDictionaryAndSequenceBoundaries()
    {
        var flags = SerializerPropagationFlags.DictionaryValues |
                    SerializerPropagationFlags.Sequence |
                    SerializerPropagationFlags.RecusivePropagation;

        var result = CreateManager().DeserializeNode<Dictionary<string, List<int>>>(
            Parse("{group: [1, 2]}"), typeof(AddHundredSerializer), flags);

        Assert.Equal(new[] { 101, 102 }, result["group"]);
    }

    [Fact]
    public void NonRecursivePropagation_StopsAfterDictionaryBoundary()
    {
        var flags = SerializerPropagationFlags.DictionaryValues |
                    SerializerPropagationFlags.Sequence;

        var result = CreateManager().DeserializeNode<Dictionary<string, List<int>>>(
            Parse("{group: [1, 2]}"), typeof(AddHundredSerializer), flags);

        Assert.Equal(new[] { 1, 2 }, result["group"]);
    }

    [Fact]
    public void ExplicitDataFieldSerializer_AppliesToField()
    {
        var result = CreateManager().DeserializeNode<DirectFieldDefinition>(
            Parse("{value: 3}"));

        Assert.Equal(103, result.Value);
    }

    [Fact]
    public void DataFieldsFlag_PropagatesIntoMembers()
    {
        var result = CreateManager().DeserializeNode<PlainDefinition>(
            Parse("{value: 3}"), typeof(AddHundredSerializer),
            SerializerPropagationFlags.DataFields);

        Assert.Equal(103, result.Value);
    }

    [Fact]
    public void DataFieldsFlag_RespectsExplicitNestedSerializer()
    {
        var result = CreateManager().DeserializeNode<ExplicitFieldDefinition>(
            Parse("{value: 3}"), typeof(AddHundredSerializer),
            SerializerPropagationFlags.DataFields);

        Assert.Equal(1003, result.Value);
    }

    [Fact]
    public void DataFieldsOverrideExisting_OverridesExplicitNestedSerializer()
    {
        var flags = SerializerPropagationFlags.DataFields |
                    SerializerPropagationFlags.DataFieldsOverrideExisting;

        var result = CreateManager().DeserializeNode<ExplicitFieldDefinition>(
            Parse("{value: 3}"), typeof(AddHundredSerializer), flags);

        Assert.Equal(103, result.Value);
    }

    [Fact]
    public void RecursiveDataFieldsOverride_ReachesGrandchildExplicitSerializer()
    {
        var flags = SerializerPropagationFlags.DataFields |
                    SerializerPropagationFlags.DataFieldsOverrideExisting |
                    SerializerPropagationFlags.RecusivePropagation;

        var result = CreateManager().DeserializeNode<OuterDefinition>(
            Parse("{inner: {value: 3}}"), typeof(AddHundredSerializer), flags);

        Assert.Equal(103, result.Inner.Value);
    }

    [Fact]
    public void WithoutRecursiveFlag_GrandchildKeepsItsExplicitSerializer()
    {
        var flags = SerializerPropagationFlags.DataFields |
                    SerializerPropagationFlags.DataFieldsOverrideExisting;

        var result = CreateManager().DeserializeNode<OuterDefinition>(
            Parse("{inner: {value: 3}}"), typeof(AddHundredSerializer), flags);

        Assert.Equal(1003, result.Inner.Value);
    }

    [DataDefinition]
    public sealed class PlainDefinition
    {
        [DataField] public int Value;
    }

    [DataDefinition]
    public sealed class DirectFieldDefinition
    {
        [DataField(CustomSerializer = typeof(AddHundredSerializer))]
        public int Value;
    }

    [DataDefinition]
    public sealed class ExplicitFieldDefinition
    {
        [DataField(CustomSerializer = typeof(AddThousandSerializer))]
        public int Value;
    }

    [DataDefinition]
    public sealed class OuterDefinition
    {
        [DataField] public ExplicitFieldDefinition Inner = new();
    }
}
