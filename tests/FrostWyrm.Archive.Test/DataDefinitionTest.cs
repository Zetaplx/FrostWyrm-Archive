using FrostWyrm.Archive.Serialization;

using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Archive.Tests.Serialization;

public sealed class SerializationManagerTests
{
    [Fact]
    public void DeserializeDataDefinition()
    {
        const string yaml = """
            name: Mothman
            count: 5
            tags:
              - cryptid
              - flying
            nested:
              value: 42
            """;

        var yamlStream = new YamlStream();

        using var reader = new StringReader(yaml);
        yamlStream.Load(reader);

        var node = yamlStream.Documents[0].RootNode;

        var manager = new SerializationManager();

        var result = manager.DeserializeNode<TestDefinition>(node);

        Assert.Equal("Mothman", result.Name);
        Assert.Equal(5, result.Count);

        Assert.Equal(2, result.Tags.Count);
        Assert.Equal("cryptid", result.Tags[0]);
        Assert.Equal("flying", result.Tags[1]);

        Assert.NotNull(result.Nested);
        Assert.Equal(42, result.Nested.Value);
    }

    [DataDefinition]
    private sealed class TestDefinition
    {
        [DataField]
        public string Name { get; set; } = string.Empty;

        [DataField]
        public int Count { get; set; }

        [DataField]
        public List<string> Tags { get; set; } = [];

        [DataField]
        public NestedDefinition Nested { get; set; } = new();
    }

    [DataDefinition]
    private sealed class NestedDefinition
    {
        [DataField]
        public int Value { get; set; }
    }
}