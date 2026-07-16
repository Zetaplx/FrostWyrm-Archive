using FrostWyrm.Archive.Core.Entities;

namespace FrostWyrm.Archive.Core.Components;

public sealed class ComponentManager
{
    private readonly EntityManager _entityManager = default!;
    private readonly Dictionary<Type, IComponentStore> _stores = new();

    public ComponentManager(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

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
}