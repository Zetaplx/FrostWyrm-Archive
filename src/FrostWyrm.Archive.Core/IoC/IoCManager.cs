using System.Reflection;
using FrostWyrm.Archive.Core.Systems;

namespace FrostWyrm.Archive.Core.IoC;

public sealed class IoCManager
{
    private readonly List<Type> _registeredTypes = new();
    private readonly Dictionary<Type, ECSManager> _instances = new();

    private readonly List<ECSManager> _initializationOrder = new();
    private readonly List<EntitySystem> _systems = new();

    private bool _built;

    public IReadOnlyList<EntitySystem> Systems => _systems;

    /// <summary>
    /// Register types to the IoCManager based on a reflective assembly.
    /// </summary>
    public void DiscoverFromAssembly(Assembly assembly)
    {
        ThrowIfBuilt();

        var discovery = assembly
            .GetTypes()
            .Where(type => typeof(ECSManager).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false })
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var type in discovery)
        {
            Register(type);
        }
    }

    /// <summary>
    /// Manually register a type to the manager
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void Register<T>() where T : ECSManager
    {
        Register(typeof(T));
    }

    /// <summary>
    /// Manually register a type to the manager
    /// </summary>
    public void Register(Type type)
    {
        ThrowIfBuilt();

        if (!typeof(ECSManager).IsAssignableFrom(type))
            throw new ArgumentException($"{type.FullName} does not inheret from {nameof(ECSManager)}");

        if (type.IsAbstract)
            throw new ArgumentException($"Cannot register abstract member type {type.FullName}");

        if (_registeredTypes.Contains(type))
            throw new InvalidOperationException($"{type.FullName} has already been registered.");

        _registeredTypes.Add(type);
    }

    /// <summary>
    /// Attempts to manually resolve a dependency of a given type.
    /// </summary>
    public bool TryResolve<TDependency>(out TDependency dependency) where TDependency : ECSManager
    {

        bool untyped = TryResolve(typeof(TDependency), out var dep);
        dependency = (TDependency)dep;
        return untyped;
    }
    /// <summary>
    /// Attempts to manually resolve a dependency of a given type.
    /// </summary>
    private bool TryResolve(Type dependencyType, out ECSManager dependency)
    {
        dependency = default!;
        if (!_built && _instances.Count == 0)
            return false;

        dependency = ResolveConstructedInstance(dependencyType);
        return dependency is not null;
    }

    #region System Lifecycle

    public void Update(float frameTime)
    {
        EnsureBuilt();

        foreach (var system in _systems)
        {
            system.Update(frameTime);
        }
    }

    public void Shutdown()
    {
        if (!_built)
            return;
        
        for (var i = _initializationOrder.Count - 1; i >= 0; i--)
        {
            _initializationOrder[i].Shutdown();
        }

        _systems.Clear();
        _initializationOrder.Clear();
        _instances.Clear();
        _built = false;
    }

    #endregion

    #region Build
    public void Build()
    {
        ThrowIfBuilt();

        ValidateDependencies();
        ConstructInstances();
        InjectDependencies();
        InitializeInstances();

        _built = true;
    }

    private void ValidateDependencies()
    {
        foreach (var ownerType in _registeredTypes)
        {
            foreach (var member in GetDependencyMembers(ownerType))
            {
                var dependencyType = GetDependencyType(member);
                var matches = FindRegisteredMatches(dependencyType);

                if (matches.Count == 0)
                    throw new InvalidOperationException($"{ownerType.FullName} depends on {dependencyType.FullName}, but no matching type is registered.");
                if (matches.Count > 1)
                    throw new InvalidOperationException($"{ownerType.FullName} depends on {dependencyType.FullName}, but multiple matching types are registered: {string.Join(", ", matches.Select(type => type.FullName))}");

                ValidateInjectionmember(ownerType, member);
            }
        }
    }

    private void ConstructInstances()
    {
        foreach (var type in _registeredTypes)
        {
            ECSManager instance;

            try
            {
                instance = (ECSManager?)Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException($"Activator returned nul while constructing ${type.FullName}.");
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Failed to construct {type.FullName}. Managers and systems must have a parameterless constructor. {exception}");
            }

            _instances.Add(type, instance);
        }
    }

    private void InjectDependencies()
    {
        foreach (var (ownerType, instance) in _instances)
        {
            foreach (var member in GetDependencyMembers(ownerType))
            {
                var dependencyType = GetDependencyType(member);
                var dependency = ResolveConstructedInstance(dependencyType);

                switch (member)
                {
                    case FieldInfo field:
                        field.SetValue(instance, dependency);
                        break;

                    case PropertyInfo property:
                        property.SetValue(instance, dependency);
                        break;

                        throw new InvalidOperationException($"Unsupported dependency member {member.Name}.");
                }
            }
        }
    }

    private void InitializeInstances()
    {
        var managers = _registeredTypes.Where(type => !typeof(EntitySystem).IsAssignableFrom(type));
        var systems = _registeredTypes.Where(type => typeof(EntitySystem).IsAssignableFrom(type));

        foreach (var type in managers.Concat(systems)) // Managers first, then systems
        {
            var instance = _instances[type];

            instance.Initialize();
            _initializationOrder.Add(instance);

            if (instance is EntitySystem system)
                _systems.Add(system);
        }
    }

    private ECSManager ResolveConstructedInstance(Type dependencyType)
    {
        if (_instances.TryGetValue(dependencyType, out var exact))
            return exact;

        var matches = _instances
            .Where(pair => dependencyType.IsAssignableFrom(pair.Key))
            .Select(pair => pair.Value)
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],

            0 => throw new InvalidOperationException($"No constucted dependency matches {dependencyType.FullName}."),
            _ => throw new InvalidOperationException($"Multiple constructed dependencies match {dependencyType.FullName}.")
        };
    }

    private void ValidateInjectionmember(Type ownerType, MemberInfo member)
    {
        switch (member)
        {
            case FieldInfo { IsStatic: true }:
                throw new InvalidOperationException($"{ownerType.FullName}.{member.Name} is static.");

            case FieldInfo { IsInitOnly: true }:
                throw new InvalidOperationException($"{ownerType.FullName}.{member.Name} is readonly.");

            case PropertyInfo { SetMethod: null }:
                throw new InvalidOperationException($"{ownerType.FullName}.{member.Name} has no setter.");

            case PropertyInfo property when property.GetIndexParameters().Length != 0:
                throw new InvalidOperationException($"{ownerType.FullName}.{member.Name} is an indexer.");
        }
    }

    private List<Type> FindRegisteredMatches(Type dependencyType)
        => _registeredTypes.Where(dependencyType.IsAssignableFrom).ToList();

    public static IEnumerable<MemberInfo> GetDependencyMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var field in type.GetFields(flags))
            if (field.GetCustomAttribute<DependencyAttribute>() is not null)
                yield return field;

        foreach (var prop in type.GetProperties(flags))
            if (prop.GetCustomAttribute<DependencyAttribute>() is not null)
                yield return prop;
    }

    public static Type GetDependencyType(MemberInfo member)
    {
        return member switch
        {
            FieldInfo field => field.FieldType,
            PropertyInfo prop => prop.PropertyType,

            _ => throw new InvalidOperationException($"{member.MemberType} cannot receive a dependency.")
        };
    }

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException(
                "Dependencies cannot be registered after the container is built.");
        }
    }

    private void EnsureBuilt()
    {
        if (!_built)
            throw new InvalidOperationException("The dependency manager is not built.");
    }
    #endregion
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DependencyAttribute : Attribute { }