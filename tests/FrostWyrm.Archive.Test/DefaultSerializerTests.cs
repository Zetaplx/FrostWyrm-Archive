using FrostWyrm.Archive.Prototypes;
using FrostWyrm.Archive.Serialization;
using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Archive.Tests;

public sealed class DefaultSerializerTests
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

    [Fact]
    public void ProtoIdSerializer_DeserializesScalar()
    {
        var manager = CreateManager();

        var result = manager.DeserializeNode<ProtoId<TestPrototype>>(new YamlScalarNode("sample"));

        Assert.Equal("sample", result.ID);
        Assert.Equal(typeof(TestPrototype), result.Type);
    }

    [Fact]
    public void ProtoIdSerializer_HandlesDifferentGenericArguments()
    {
        var manager = CreateManager();

        var first = manager.DeserializeNode<ProtoId<TestPrototype>>(new YamlScalarNode("first"));
        var second = manager.DeserializeNode<ProtoId<OtherTestPrototype>>(new YamlScalarNode("second"));

        Assert.Equal(typeof(TestPrototype), first.Type);
        Assert.Equal(typeof(OtherTestPrototype), second.Type);
        Assert.Equal("second", second.ID);
    }

    [Fact]
    public void DefaultSerializer_DeserializesListElementsRecursively()
    {
        var manager = CreateManager();

        var result = manager.DeserializeNode<List<ProtoId<TestPrototype>>>(
            Parse("[alpha, beta, gamma]"));

        Assert.Equal(new[] { "alpha", "beta", "gamma" }, result.Select(id => id.ID));
        Assert.All(result, id => Assert.Equal(typeof(TestPrototype), id.Type));
    }

    [Fact]
    public void DefaultSerializer_DeserializesArrayElementsRecursively()
    {
        var manager = CreateManager();

        var result = manager.DeserializeNode<ProtoId<TestPrototype>[]>(
            Parse("[alpha, beta]"));

        Assert.Equal(new[] { "alpha", "beta" }, result.Select(id => id.ID));
    }

    [Fact]
    public void DefaultSerializer_DeserializesDictionaryValuesRecursively()
    {
        var manager = CreateManager();

        var result = manager.DeserializeNode<Dictionary<string, ProtoId<TestPrototype>>>(
            Parse("{first: alpha, second: beta}"));

        Assert.Equal("alpha", result["first"].ID);
        Assert.Equal("beta", result["second"].ID);
    }

    [Fact]
    public void DefaultSerializer_DeserializesNestedCollections()
    {
        var manager = CreateManager();

        var result = manager.DeserializeNode<Dictionary<string, List<ProtoId<TestPrototype>>>>(
            Parse("{group: [alpha, beta]}"));

        Assert.Equal(new[] { "alpha", "beta" }, result["group"].Select(id => id.ID));
    }

    [Fact]
    public void DefaultSerializer_PopulatesDataDefinitionMembers()
    {
        var manager = CreateManager();

        var result = manager.DeserializeNode<TestDefinition>(
            Parse("{name: sample, reference: alpha, references: [beta, gamma]}"));

        Assert.Equal("sample", result.Name);
        Assert.Equal("alpha", result.Reference.ID);
        Assert.Equal(new[] { "beta", "gamma" }, result.References.Select(id => id.ID));
    }

    [Fact]
    public void DefaultSerializer_PopulatesPrototypeMembers()
    {
        var manager = CreateManager();

        var result = (TestPrototype)manager.DeserializePrototype(
            typeof(TestPrototype),
            (YamlMappingNode)Parse("{id: sample, reference: alpha}"));

        Assert.Equal("sample", result.ID);
        Assert.Equal("alpha", result.Reference.ID);
    }

    [Fact]
    public void InvalidScalar_IsRejectedBySerializerValidation()
    {
        var manager = CreateManager();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            manager.DeserializeNode<ProtoId<TestPrototype>>(new YamlScalarNode((string?)null)));

        Assert.Contains("Invalid YAML value", exception.Message);
    }

    [Fact]
    public void UnregisteredType_UsesBuiltInScalarDeserializer()
    {
        var manager = CreateManager();

        Assert.Equal(42, manager.DeserializeNode<int>(new YamlScalarNode("42")));
        Assert.True(manager.DeserializeNode<bool>(new YamlScalarNode("true")));
    }

    [Fact]
    public void DuplicateAssemblyRegistration_IsRejected()
    {
        var manager = CreateManager();

        Assert.Throws<InvalidOperationException>(() =>
            manager.RegisterAssemblySerializers(typeof(ProtoIdSerializer<>).Assembly));
    }

    [DataDefinition]
    public sealed class TestDefinition
    {
        [DataField] public string Name = string.Empty;
        [DataField] public ProtoId<TestPrototype> Reference = new("default");
        [DataField] public List<ProtoId<TestPrototype>> References = new();
    }

    [Prototype("serializerTest")]
    public sealed class TestPrototype : IPrototype
    {
        [IdDataField] public string ID { get; init; } = string.Empty;
        [DataField] public ProtoId<TestPrototype> Reference = new("default");
    }

    public sealed class OtherTestPrototype : IPrototype
    {
        public string ID { get; init; } = string.Empty;
    }
}
