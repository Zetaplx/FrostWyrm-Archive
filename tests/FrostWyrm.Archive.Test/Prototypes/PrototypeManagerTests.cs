using FrostWyrm.Archive.DependencyInjection;
using FrostWyrm.Archive.Prototypes;
using FrostWyrm.Archive.Serialization;

using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Archive.Tests.Prototypes;

public sealed class PrototypeManagerTests
{
    [Fact]
    public void LoadPrototypesDeserializesAndIndexesDefinitions()
    {
        var manager = CreatePrototypeManager();

        const string yaml = """
            - type: test
              id: alpha
              value: 5

            - type: test
              id: beta
              value: 10
            """;

        manager.LoadPrototypes(new StringReader(yaml));

        var alpha = manager.Index<TestPrototype>("alpha");
        var beta = manager.Index<TestPrototype>("beta");

        Assert.Equal("alpha", alpha.ID);
        Assert.Equal(5, alpha.Value);
        Assert.Equal("beta", beta.ID);
        Assert.Equal(10, beta.Value);
    }

    [Fact]
    public void EnumeratePrototypesCanFilterResults()
    {
        var manager = CreatePrototypeManager();

        const string yaml = """
            - type: test
              id: low
              value: 2

            - type: test
              id: high
              value: 8
            """;

        manager.LoadPrototypes(new StringReader(yaml));

        var results = manager
            .EnumeratePrototypes<TestPrototype>(prototype => prototype.Value >= 5)
            .ToArray();

        var result = Assert.Single(results);
        Assert.Equal("high", result.ID);
    }

    [Fact]
    public void LoadPrototypesRejectsUnknownPrototypeType()
    {
        var manager = CreatePrototypeManager();

        const string yaml = """
            - type: doesNotExist
              id: bad
            """;

        Assert.Throws<InvalidOperationException>(
            () => manager.LoadPrototypes(new StringReader(yaml)));
    }

    [Fact]
    public void LoadPrototypesRequiresSequenceRoot()
    {
        var manager = CreatePrototypeManager();

        const string yaml = """
            type: test
            id: invalidRoot
            value: 1
            """;

        Assert.Throws<InvalidOperationException>(
            () => manager.LoadPrototypes(new StringReader(yaml)));
    }

    [Fact]
    public void DuplicatePrototypeTypeAndIdThrows()
    {
        var manager = CreatePrototypeManager();

        const string yaml = """
            - type: test
              id: duplicate
              value: 1

            - type: test
              id: duplicate
              value: 2
            """;

        Assert.Throws<ArgumentException>(
            () => manager.LoadPrototypes(new StringReader(yaml)));
    }

    [Fact]
    public void RegistryCanResolveDerivedPrototypeByBaseType()
    {
        var registry = new PrototypeRegistry();
        var prototype = new DerivedLookupPrototype
        {
            ID = "derived"
        };

        registry.RegisterPrototype(prototype);

        var found = registry.TryGetById(
            typeof(BaseLookupPrototype),
            "derived",
            out var result);

        Assert.True(found);
        Assert.Same(prototype, result);
    }

    [Fact]
    public void RegistryThrowsForAmbiguousInheritanceLookup()
    {
        var registry = new PrototypeRegistry();

        registry.RegisterPrototype(new DerivedLookupPrototype { ID = "shared" });
        registry.RegisterPrototype(new OtherDerivedLookupPrototype { ID = "shared" });

        Assert.Throws<InvalidOperationException>(() =>
            registry.TryGetById(
                typeof(BaseLookupPrototype),
                "shared",
                out _));
    }

    private static PrototypeManager CreatePrototypeManager()
    {
        var dependencies = new DependencyManager();
        dependencies.Register(new SerializationManager());

        var manager = new PrototypeManager();
        dependencies.InjectDependencies(manager);

        manager.RegisterAssemblyPrototypes(typeof(TestPrototype).Assembly);

        return manager;
    }

    [Prototype]
    public sealed class TestPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; init; } = default!;

        [DataField]
        public int Value { get; init; }
    }

    public abstract class BaseLookupPrototype : IPrototype
    {
        public string ID { get; init; } = default!;
    }

    public sealed class DerivedLookupPrototype : BaseLookupPrototype;

    public sealed class OtherDerivedLookupPrototype : BaseLookupPrototype;
}
