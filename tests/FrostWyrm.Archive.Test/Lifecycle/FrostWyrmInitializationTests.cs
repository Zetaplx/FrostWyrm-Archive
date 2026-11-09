using System.Reflection;

using FrostWyrm.Archive.DependencyInjection;
using FrostWyrm.Archive;
using FrostWyrm.Archive.Prototypes;
using FrostWyrm.Archive.Serialization;

namespace FrostWyrm.Archive.Tests.Lifecycle;

public sealed class FrostWyrmInitializationTests : IDisposable
{
    public FrostWyrmInitializationTests()
    {
        ResetArchive();
    }

    public void Dispose()
    {
        ResetArchive();
    }

    [Fact]
    public void ArchiveAssemblyRegistersCoreManagersAsDefaultDependencies()
    {
        var dependencies = new DependencyManager();

        dependencies.RegisterDefaultDependencies(typeof(FrostWyrm).Assembly);

        Assert.True(dependencies.TryResolve<DependencyManager>(out _));
        Assert.True(dependencies.TryResolve<IDependencyManager>(out _));
        Assert.True(dependencies.TryResolve<SerializationManager>(out _));
        Assert.True(dependencies.TryResolve<ISerializationManager>(out _));
        Assert.True(dependencies.TryResolve<PrototypeManager>(out _));
        Assert.True(dependencies.TryResolve<IPrototypeManager>(out _));
    }

    [Fact]
    public void InitializeBuildsCompleteCoreDependencyGraph()
    {
        FrostWyrm.Initialize();

        var archive = GetArchiveInstance();
        var dependencies = archive.DependencyManager;

        Assert.NotNull(dependencies);

        var serialization = dependencies.Resolve<ISerializationManager>();
        var prototypes = dependencies.Resolve<IPrototypeManager>();

        Assert.IsType<SerializationManager>(serialization);
        Assert.IsType<PrototypeManager>(prototypes);
    }

    [Fact]
    public void InitializeRejectsSecondInitialization()
    {
        FrostWyrm.Initialize();

        Assert.Throws<InvalidOperationException>(
            () => FrostWyrm.Initialize());
    }

    private static FrostWyrm GetArchiveInstance()
    {
        var field = GetInstanceField();

        return (FrostWyrm?)field.GetValue(null)
            ?? throw new InvalidOperationException("FrostWyrm singleton was not initialized.");
    }

    private static void ResetArchive()
    {
        GetInstanceField().SetValue(null, null);
    }

    private static FieldInfo GetInstanceField()
    {
        return typeof(FrostWyrm)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unable to locate FrostWyrm singleton instance field.");
    }
}
