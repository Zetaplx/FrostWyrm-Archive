using System.Reflection;
using FrostWyrm.Archive.Core.Components;
using FrostWyrm.Archive.Core.Entities;
using FrostWyrm.Archive.Core.IoC;

namespace FrostWyrm.Archive.Core;

public sealed class App
{
    private readonly IoCManager _iocManager;
    public App()
    {
        _iocManager = new IoCManager();
    }

    public void Build(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
            _iocManager.DiscoverFromAssembly(assembly);
        _iocManager.Build();
    }

    public void Update(float frameTime)
    {
        _iocManager.Update(frameTime);
    }

    public void Shutdown()
    {
        _iocManager.Shutdown();
    }
}