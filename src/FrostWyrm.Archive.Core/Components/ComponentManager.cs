using FrostWyrm.Archive.Core.Entities;

namespace FrostWyrm.Archive.Core.Components;

public sealed class ComponentManager
{
    private Dictionary<Type, IComponentStore> _stores = new();

    /// <summary>
    /// Adds a entity to the store for a given component type. Creates a new store if one does not exist.
    /// </summary>
    public void AddComponent<TComponent>(EntityUid uid, TComponent component) where TComponent : IComponent
    {
        if (!uid.IsValid)
            return;
        
        if (_stores.TryGetValue(typeof(TComponent), out var store))
        {
            if (store.Has(uid)) return;
            if (store is not ComponentStore<TComponent> typedStore) return;
            
            typedStore.Add(uid, component);
            return;
        }

        var newStore = new ComponentStore<TComponent>();
        newStore.Add(uid, component);
        _stores.Add(typeof(TComponent), newStore);
    }
}