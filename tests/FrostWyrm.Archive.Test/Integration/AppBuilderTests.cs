using FrostWyrm.Archive.Core;
using FrostWyrm.Archive.Core.IoC;
using FrostWyrm.Archive.Test.Integration;

namespace FrostWyrm.Archive.Test;

public sealed class AppBuilderTests
{
    [Fact]
    public void BuildDiscoversAndInitializesTestSystem()
    {
        var app = new AppBuilder()
            .AddAssemblyContaining<TestSystem>()
            .Build();

        try
        {
            app.IoCManager.TryResolve<TestSystem>(out var system);

            Assert.True(system.Initialized);
        }
        finally
        {
            app.Shutdown();
        }
    }

    [Fact]
    public void BuiltAppCanCreateAndDestroyComponentEntity()
    {
        var app = new AppBuilder()
            .AddAssemblyContaining<TestSystem>()
            .Build();

        try
        {
            app.IoCManager.TryResolve<TestSystem>(out var system);

            var uid = system.CreateTestEntity("Hello Archive");

            Assert.True(system.TryGet(uid, out var component));
            Assert.Equal("Hello Archive", component.Value);

            Assert.True(system.Destroy(uid));
            Assert.False(system.TryGet(uid, out _));
        }
        finally
        {
            app.Shutdown();
        }
    }
}