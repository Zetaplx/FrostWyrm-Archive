using FrostWyrm.Archive.Core.Entities;
using FrostWyrm.Archive.Core.IoC;
using FrostWyrm.Archive.Core.Systems;

namespace FrostWyrm.Archive.Core.Components;

public sealed class ComponentManager : ECSManager
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    private readonly Dictionary<Type, IComponentStore> _stores = new();

    /// <summary>
    /// Adds a component to an entity, creating a store for the component type if
    /// one does not already exist.
    /// </summary>
    public bool AddComponent<TComponent>(EntityUid uid, TComponent component) where TComponent : IComponent
    {
        if (!_entityManager.Exists(uid))
            return false;

        if (_stores.TryGetValue(typeof(TComponent), out var store))
        {
            if (store is not ComponentStore<TComponent> typedStore)
                throw new InvalidOperationException($"Component store for {typeof(TComponent)} has an incompatible type.");


            return typedStore.Add(uid, component);
        }

        var newStore = new ComponentStore<TComponent>();
        _stores.Add(typeof(TComponent), newStore);
        return newStore.Add(uid, component);
    }

    /// <summary>
    /// Checks if a given entity uid has a given component type.
    /// </summary>
    public bool HasComponent<TComponent>(EntityUid uid) where TComponent : IComponent => TryGetComponent<TComponent>(uid, out _);

    /// <summary>
    /// Attempts to retrieve a component from a given entity uid.
    /// </summary>
    public bool TryGetComponent<TComponent>(EntityUid uid, out TComponent component) where TComponent : IComponent
    {
        component = default!;
        if (!_entityManager.Exists(uid) || !_stores.TryGetValue(typeof(TComponent), out var store))
            return false;

        if (store is not ComponentStore<TComponent> typedStore)
            throw new InvalidOperationException($"Component store for {typeof(TComponent)} has an incompatible type.");

        return typedStore.TryGet(uid, out component);
    }

    public bool RemoveComponent<TComponent>(EntityUid uid, out TComponent removed) where TComponent : IComponent
    {
        removed = default!;
        if (!_entityManager.Exists(uid) || !_stores.TryGetValue(typeof(TComponent), out var store))
            return false;

        if (store is not ComponentStore<TComponent> typedStore)
            throw new InvalidOperationException($"Component store for {typeof(TComponent)} has an incompatible type.");

        if (!typedStore.Remove(uid, out removed))
            return false;

        if (typedStore.Count <= 0)
            _stores.Remove(typeof(TComponent));
        return true;
    }

    public void RemoveAllComponents(EntityUid uid)
    {
        foreach (var (_, store) in _stores)
        {
            store.Remove(uid);
        }
    }
}