using System.Reflection;
using FrostWyrm.Archive.Core.Components;
using FrostWyrm.Archive.Core.Entities;
using FrostWyrm.Archive.Core.IoC;

namespace FrostWyrm.Archive.Core;

public sealed class App
{
    public readonly IoCManager IoCManager;
    public App()
    {
        IoCManager = new IoCManager();
    }

    public void Build(params Assembly[] assemblies)
    {
        IoCManager.DiscoverFromAssembly(typeof(App).Assembly); // Always include core
        foreach (var assembly in assemblies)
            IoCManager.DiscoverFromAssembly(assembly);
        IoCManager.Build();
    }

    public void Update(float frameTime)
    {
        IoCManager.Update(frameTime);
    }

    public void Shutdown()
    {
        IoCManager.Shutdown();
    }
}