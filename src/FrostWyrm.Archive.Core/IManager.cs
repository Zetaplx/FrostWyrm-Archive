using FrostWyrm.Archive.DependencyInjection;

namespace FrostWyrm.Archive;

/// <summary>
///     The basic building block of the FrostWyrm Archive ECS. The highest level of dependency. Should be as self contained as possible.
/// </summary>
[DefaultDependency, AutoInjectDependencies]
public interface IManager;