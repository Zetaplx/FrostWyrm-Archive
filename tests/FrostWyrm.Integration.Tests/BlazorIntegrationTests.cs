using CryptidCompanion;
using FrostWyrm.Archive.DependencyInjection;
using FrostWyrm.Archive.Prototypes;
using FrostWyrm.Archive.Serialization;
using FrostWyrm.Blazor;
using Microsoft.Extensions.DependencyInjection;

using ArchiveRuntime = FrostWyrm.Archive.FrostWyrm;

namespace FrostWyrm.Integration.Tests;

public sealed class BlazorIntegrationTests
{
    [Fact]
    public void BridgeRegistersTheSameCoreAndApplicationInstances()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddFrostWyrm(typeof(GameManager).Assembly));
        using var provider = services.BuildServiceProvider();

        Assert.Same(ArchiveRuntime.Resolve<IDependencyManager>(), provider.GetRequiredService<IDependencyManager>());
        Assert.Same(ArchiveRuntime.Resolve<IPrototypeManager>(), provider.GetRequiredService<IPrototypeManager>());
        Assert.Same(ArchiveRuntime.Resolve<ISerializationManager>(), provider.GetRequiredService<ISerializationManager>());
        Assert.Same(ArchiveRuntime.Resolve<GameManager>(), provider.GetRequiredService<GameManager>());
        Assert.Same(provider.GetRequiredService<PrototypeManager>(), provider.GetRequiredService<IPrototypeManager>());
        Assert.Throws<InvalidOperationException>(() => services.AddFrostWyrm());
    }
}
