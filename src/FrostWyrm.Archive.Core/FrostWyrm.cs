using System.Reflection;

using FrostWyrm.Archive.DependencyInjection;
using FrostWyrm.Archive.Prototypes;
using FrostWyrm.Archive.Serialization;

namespace FrostWyrm.Archive;

/// <summary>
///     The core class of the FrostWyrm Archive. Can be used to facilitate and run all archive functionality.
/// </summary>
public sealed class FrostWyrm
{
    private static FrostWyrm? _instance = null;

    [Dependency] public IDependencyManager DependencyManager = default!;
    [Dependency] public IPrototypeManager PrototypeManager = default!;
    [Dependency] public ISerializationManager SerializationManager = default!;

    /// <summary>
    ///     Ensures that the singleton instance is initialized.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private static void VerifyInitialization()
    {
        if (_instance is null)
            throw new InvalidOperationException($"Unable to execute method with uninitialized singleton instance.");
    }

    /// <summary>
    ///     Executes all necessary dependency registration and injection for all default dependencies from all provided assemblies.
    ///     Always includes the Archive assembly.
    /// </summary>
    public static void Initialize(params Assembly[] assemblies)
    {
        if (_instance is not null)
            throw new InvalidOperationException($"Attempted to initialize an already initialized Archive.");

        _instance = new();
        _instance.InitializeInstance(assemblies);
    }

    /// <summary>
    ///     Executes all necessary dependency registration and injection for all default dependencies from all provided assemblies.
    ///     Always includes the Archive assembly.
    /// </summary>
    private void InitializeInstance(params Assembly[] assemblies)
    {
        var dependencies = new DependencyManager();
        dependencies.RegisterDefaultDependencies(typeof(FrostWyrm).Assembly);
        foreach (var assembly in assemblies)
            dependencies.RegisterDefaultDependencies(assembly);

        dependencies.AutoInjectDependencies();
        dependencies.InjectDependencies(this);

        PrototypeManager.RegisterAssemblyPrototypes(typeof(FrostWyrm).Assembly);
        SerializationManager.RegisterAssemblySerializers(typeof(FrostWyrm).Assembly);
        foreach (var assembly in assemblies)
        {
            PrototypeManager.RegisterAssemblyPrototypes(assembly);
            SerializationManager.RegisterAssemblySerializers(assembly);
        }

    }

    /// <summary>
    ///     Uses the Dependency Manager to resolve a dependency.
    /// </summary>
    public static T Resolve<T>()
    {
        VerifyInitialization();

        return _instance!.DependencyManager.Resolve<T>();
    }
}