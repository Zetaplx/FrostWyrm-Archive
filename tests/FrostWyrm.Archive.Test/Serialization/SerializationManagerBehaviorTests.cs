using FrostWyrm.Archive.Serialization;

using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Archive.Tests.Serialization;

public sealed class SerializationManagerBehaviorTests
{
    [Fact]
    public void MissingRequiredFieldThrows()
    {
        const string yaml = """
            optional: 5
            """;

        var node = ParseRoot(yaml);
        var manager = new SerializationManager();

        Assert.Throws<InvalidOperationException>(
            () => manager.DeserializeNode<RequiredDefinition>(node));
    }

    [Fact]
    public void MissingOptionalFieldPreservesClrDefault()
    {
        const string yaml = """
            name: test
            """;

        var node = ParseRoot(yaml);
        var manager = new SerializationManager();

        var result = manager.DeserializeNode<DefaultValueDefinition>(node);

        Assert.Equal("test", result.Name);
        Assert.Equal(100, result.Health);
    }

    [Fact]
    public void ArraysAndEnumsDeserialize()
    {
        const string yaml = """
            values:
              - 1
              - 2
              - 3
            mode: active
            """;

        var node = ParseRoot(yaml);
        var manager = new SerializationManager();

        var result = manager.DeserializeNode<ArrayAndEnumDefinition>(node);

        Assert.Equal([1, 2, 3], result.Values);
        Assert.Equal(TestMode.Active, result.Mode);
    }

    [Fact]
    public void MappingCannotDeserializePrototypeAsNestedDataDefinition()
    {
        const string yaml = """
            id: nested
            value: 5
            """;

        var node = ParseRoot(yaml);
        var manager = new SerializationManager();

        Assert.Throws<InvalidOperationException>(
            () => manager.DeserializeNode<NestedPrototype>(node));
    }

    private static YamlNode ParseRoot(string yaml)
    {
        var stream = new YamlStream();

        using var reader = new StringReader(yaml);
        stream.Load(reader);

        return stream.Documents[0].RootNode;
    }

    [DataDefinition]
    public sealed class RequiredDefinition
    {
        [DataField(Required: true)]
        public string Required { get; init; } = default!;

        [DataField]
        public int Optional { get; init; }
    }

    [DataDefinition]
    public sealed class DefaultValueDefinition
    {
        [DataField]
        public string Name { get; init; } = string.Empty;

        [DataField]
        public int Health { get; init; } = 100;
    }

    [DataDefinition]
    public sealed class ArrayAndEnumDefinition
    {
        [DataField]
        public int[] Values { get; init; } = [];

        [DataField]
        public TestMode Mode { get; init; }
    }

    public enum TestMode
    {
        Inactive,
        Active
    }

    public sealed class NestedPrototype : Archive.Prototypes.IPrototype
    {
        [Archive.Prototypes.IdDataField]
        public string ID { get; init; } = default!;

        [DataField]
        public int Value { get; init; }
    }
}
