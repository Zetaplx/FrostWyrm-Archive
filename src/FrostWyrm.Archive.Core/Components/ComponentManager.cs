using System.ComponentModel;
using FrostWyrm.Archive.Core.Entities;
using FrostWyrm.Archive.Core.IoC;
using FrostWyrm.Archive.Core.Systems;

namespace FrostWyrm.Archive.Core.Components;

public sealed class ComponentManager : ECSManager
{
    [Dependency] private EntityManager _entityManager = default!;
    private readonly Dictionary<Type, IComponentStore> _stores = new();

    /// <summary>
    /// Adds a component to an entity, creating a store for the component type if
    /// one does not already exist.
    /// </summary>
    public bool AddComponent<TComponent>(EntityUid uid, TComponent component) where TComponent : IComponent
    {
        if (!_entityManager.Exists(uid))
            return false;

        if (TryGetTypedStore<TComponent>(out var store))
        {
            return store.Add(uid, component);
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
        if (!_entityManager.Exists(uid) || !TryGetTypedStore<TComponent>(out var store))
            return false;

        return store.TryGet(uid, out component);
    }

    public bool RemoveComponent<TComponent>(EntityUid uid, out TComponent removed) where TComponent : IComponent
    {
        removed = default!;
        if (!_entityManager.Exists(uid) || !TryGetTypedStore<TComponent>(out var store))
            return false;

        if (!store.Remove(uid, out removed))
            return false;

        if (store.Count <= 0)
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

    private bool TryGetTypedStore<TComponent>(out ComponentStore<TComponent> typed) where TComponent : IComponent
    {
        typed = default!;
        if (!_stores.TryGetValue(typeof(TComponent), out var store))
            return false;

        if (store is not ComponentStore<TComponent> typedStore)
            return false;

        typed = typedStore;
        return true;
    }

    #region Queries

    public IEnumerable<EntityUid> Query<TComponent>() where TComponent : IComponent
    {
        if (!TryGetTypedStore<TComponent>(out var store))
            yield break;

        foreach (var (uid, _) in store.All())
            yield return uid;
    }

    public IEnumerable<EntityUid> Query<TComponent1, TComponent2>()
        where TComponent1 : IComponent
        where TComponent2 : IComponent
    {
        if (!TryGetTypedStore<TComponent1>(out var store1) || !TryGetTypedStore<TComponent2>(out var store2))
            yield break;

        var combined = store1.Ids().Intersect(store2.Ids());
        foreach (var uid in combined)
            yield return uid;
    }

    public IEnumerable<EntityUid> Query(params Type[] types)
    {
        IEnumerable<EntityUid>? ids = null;
        foreach (var type in types)
        {
            if (!typeof(IComponent).IsAssignableFrom(type))
                throw new InvalidOperationException($"Invalid component type {type.FullName}. Queried types must inherit IComponent.");

            if (!_stores.TryGetValue(type, out var store))
                continue;

            if (ids == null)
            {
                ids = store.Ids().ToList();
                continue;
            }

            ids = ids.Intersect(store.Ids());
        }
        if (ids is null)
            yield break;

        foreach (var uid in ids)
            yield return uid;
    }
    #endregion
}