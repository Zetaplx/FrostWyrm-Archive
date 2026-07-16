using System.Reflection;
using FrostWyrm.Archive.Core;

public sealed class AppBuilder
{
    private readonly List<Assembly> _assemblies = new();

    public AppBuilder AddAssembly(Assembly assembly)
    {
        _assemblies.Add(assembly);
        return this;
    }

    public App Build()
    {
        var app = new App();
        app.Build(_assemblies.ToArray());
        return app;
    }
}