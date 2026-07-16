using FrostWyrm.Archive.Core.Entities;

namespace FrostWyrm.Archive.Core.Components;

public sealed class ComponentStore<TComponent> : IComponentStore where TComponent : IComponent
{
    private readonly Dictionary<EntityUid, TComponent> _components = new();

    public int Count => _components.Count;

    public Type ComponentType => typeof(TComponent);

    public bool Add(EntityUid uid, TComponent component)
    {
        if (_components.ContainsKey(uid))
            return false;
        
        _components[uid] = component;
        return true;
    }

    public bool Remove(EntityUid uid) => Remove(uid, out _);
    public bool Remove(EntityUid uid, out TComponent removed)
    {
        return _components.Remove(uid, out removed!);
    }

    public bool TryGet(EntityUid uid, out TComponent component)
    {
        return _components.TryGetValue(uid, out component!);
    }
    public bool Has(EntityUid uid) => TryGet(uid, out _);

    public void Clear()
    {
        _components.Clear();
    }
}

public interface IComponentStore
{
    Type ComponentType { get; }

    bool Remove(EntityUid uid);
    bool Has(EntityUid uid);

    void Clear();
}