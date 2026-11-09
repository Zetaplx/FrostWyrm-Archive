using System.Reflection;

namespace FrostWyrm.Archive.DependencyInjection;

/// <summary>
///     Manages the registration, resolution, and injection of all Dependencies in other Archive Systems and Managers.
/// </summary>
public interface IDependencyManager : IManager
{
    /// <summary>
    ///     Registers a new instance of a type to the DependencyManager.
    /// </summary>
    /// <param name="overrideExisting">If true, any existing instance will be overwritten by the new instance.</param>
    public void Register<T>(bool overrideExisting = false) where T : class, new();

    /// <summary>
    ///     Registers a new instance of a type to the DependencyManager.
    /// </summary>
    /// <param name="overrideExisting">If true, any existing instance will be overwritten by the provided instance.</param>
    public void Register<T>(T instance, bool overrideExisting = false) where T : class;

    /// <summary>
    ///     Resolves a dependency to the registered instance within the DependencyManager.
    ///     If no such instance is registered, throws and exception.
    /// </summary>
    public T Resolve<T>(bool allowInheritanceLookup = true);

    /// <summary>
    ///     Attempts to resolve a dependency, returning true if there exists a registered instance.
    /// </summary>
    public bool TryResolve<T>(out T instance, bool allowInheritanceLookup = true);

    /// <summary>
    ///     Injects all explicit dependencies into an object instance, as identified with the DependencyAttribute.
    /// </summary>
    public void InjectDependencies(object instance);

    /// <summary>
    ///     Registers any class/struct marked with the <see cref="DefaultDependencyAttribute"/> within the provided assembly.
    /// </summary>
    public void RegisterDefaultDependencies(Assembly assembly);

    /// <summary>
    ///     Injects all dependencies in any class/structed marked with the <see cref="AutoInjectDependenciesAttribute"/>.
    /// 
    ///     Will only inject dependencies into already registed classes/structs.
    /// </summary>
    public void AutoInjectDependencies();

    /// <summary>
    ///     Returns an enumarable of all dependencies currently registered in the Dependency Manager.
    /// </summary>
    public IEnumerable<(Type Type, object Instance)> EnumerateDependencies();
}

/// <inheritdoc cref="IDependencyManager"/>
public sealed class DependencyManager : IDependencyManager
{
    private readonly Dictionary<Type, object> _registeredDependencies = new();

    public DependencyManager()
    {
        Register(this);
    }

    /// <inheritdoc/>
    public void Register<T>(bool overrideExisting = false) where T : class, new()
        => Register<T>(new(), overrideExisting);

    /// <inheritdoc/>
    public void Register<T>(T instance, bool overrideExisting = false) where T : class
    {
        ValidateRegistration(instance.GetType());
        if (_registeredDependencies.ContainsKey(instance.GetType()) && !overrideExisting)
            return;

        _registeredDependencies[instance.GetType()] = instance;
    }

    private void ValidateRegistration(Type type)
    {
        if (!type.IsClass)
            throw new InvalidOperationException($"Dependency \"{type.FullName}\" must be a class.");

        if (!type.IsSealed)
            throw new InvalidOperationException($"Dependency \"{type.FullName}\" must be sealed.");
    }

    /// <inheritdoc/>
    public bool TryResolve<T>(out T instance, bool allowInheritanceLookup = true)
    {
        instance = default!;
        if (!TryResolve(typeof(T), out var obj, allowInheritanceLookup))
            return false;

        instance = (T)obj;
        return true;
    }

    /// <summary>
    ///     A runtime implementation of dependency resolution.
    /// </summary>
    private bool TryResolve(Type type, out object instance, bool allowInheritanceLookup = true)
    {
        instance = default!;
        if (_registeredDependencies.TryGetValue(type, out var obj))
        {
            instance = obj;
            return true;
        }
        if (!allowInheritanceLookup)
            return false;

        bool found = false;
        foreach (var (dependencyType, dependency) in _registeredDependencies)
        {
            if (!type.IsAssignableFrom(dependencyType))
                continue;

            if (found)
                throw new InvalidOperationException($"Multiple registered dependencies satisfy \"{type.FullName}\".");

            instance = dependency;
            found = true;
        }

        return found;
    }

    /// <inheritdoc/>
    public T Resolve<T>(bool allowInheritanceLookup = true)
    {
        if (!TryResolve<T>(out var instance, allowInheritanceLookup))
            throw new InvalidOperationException($"Unable to resolve dependency \"{typeof(T).FullName}\".");

        return instance;
    }

    /// <inheritdoc/>
    public void InjectDependencies(object instance)
    {
        var type = instance.GetType();

        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        foreach (var field in type.GetFields(flags))
        {
            if (!field.IsDefined(typeof(DependencyAttribute), true))
                continue;

            if (!TryResolve(field.FieldType, out var dependency))
                throw new InvalidOperationException($"Unable to resolve \"{field.FieldType.FullName}\" dependency \"{type.FullName}.{field.Name}\".");

            field.SetValue(instance, dependency);
        }

        foreach (var property in type.GetProperties(flags))
        {
            if (!property.IsDefined(typeof(DependencyAttribute), true))
                continue;

            if (!TryResolve(property.PropertyType, out var dependency))
                throw new InvalidOperationException($"Unable to resolve \"{property.PropertyType.FullName}\" dependency \"{type.FullName}.{property.Name}\".");

            if (property.SetMethod == null)
                throw new InvalidOperationException($"Attempted to inject dependency into property \"{type.FullName}.{property.Name}\" without a valid setter.");

            property.SetValue(instance, dependency);
        }
    }

    /// <inheritdoc/>
    public void RegisterDefaultDependencies(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!HasRecursiveAttribute<DefaultDependencyAttribute>(type))
                continue;

            if (!type.IsClass || type.IsAbstract)
                continue;

            if (_registeredDependencies.ContainsKey(type))
                continue;

            ValidateRegistration(type);

            var instance = Activator.CreateInstance(type);
            if (instance == null)
                throw new InvalidOperationException($"Unable to instantiate default dependency \"{type.FullName}\".");

            _registeredDependencies[type] = instance;
        }
    }

    /// <summary>
    ///     Determines if a given type has a given attribute type by walking its inheritance tree.
    /// </summary>
    private bool HasRecursiveAttribute<TAttribute>(Type type) where TAttribute : Attribute
    {
        if (type.IsDefined(typeof(TAttribute), true))
            return true;

        foreach (var interfaceType in type.GetInterfaces())
        {
            if (interfaceType.IsDefined(typeof(TAttribute), false))
                return true;
        }

        var current = type.BaseType;
        while (current is not null && current != typeof(object))
        {
            if (current.IsDefined(typeof(TAttribute), false))
                return true;

            current = current.BaseType;
        }

        return false;
    }

    /// <inheritdoc/>
    public void AutoInjectDependencies()
    {
        foreach (var (type, instance) in _registeredDependencies)
        {
            if (!HasRecursiveAttribute<AutoInjectDependenciesAttribute>(type))
                continue;

            InjectDependencies(instance);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<(Type Type, object Instance)> EnumerateDependencies()
    {
        foreach (var (type, instance) in _registeredDependencies)
            yield return (type, instance);
    }
}

/// <summary>
///     Identifies a member as a dependency which should be injected by the DependencyManager.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class DependencyAttribute : Attribute;

/// <summary>
///     Marks a class/struct and its children to have its dependencies automatically injected by the DependencyManager.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public sealed class AutoInjectDependenciesAttribute : Attribute;

/// <summary>
///     Identifies a class/struct and its children as a default dependency to be automatically registered by the DependencyManager.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public sealed class DefaultDependencyAttribute : Attribute;