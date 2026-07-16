using FrostWyrm.Archive.Core.Components;
using FrostWyrm.Archive.Core.IoC;
using FrostWyrm.Archive.Core.Systems;

namespace FrostWyrm.Archive.Core.Entities;

/// <summary>
/// Creates, destroys, and tracks entities within an App.
/// </summary>
public sealed class EntityManager : ECSManager
{
    [Dependency] private readonly ComponentManager _componentManager = default!;

    private readonly List<uint> _generations = new();
    private readonly HashSet<int> _alive = new();
    private readonly Stack<int> _freeIndices = new();

    /// <summary>
    /// Number of currently living entities.
    /// </summary>
    public int Count => _alive.Count;

    /// <summary>
    /// Creates a new entity, returning its UID.
    /// </summary>
    public EntityUid Create()
    {
        int index;
        if (_freeIndices.TryPop(out var freeIndex))
            index = freeIndex;
        else
        {
            index = _generations.Count;
            _generations.Add(0);
        }

        _alive.Add(index);
        return new EntityUid(index, _generations[index]);
    }

    /// <summary>
    /// Returns whether the provided uid is currently alive and matches the living generation.
    /// </summary>
    public bool Exists(EntityUid uid)
    {
        if (!uid.IsValid) 
            return false;

        if (uid.Index >= _generations.Count)
            return false;
        
        return _alive.Contains(uid.Index) && _generations[uid.Index] == uid.Generation;
    }

    /// <summary>
    /// Kills a living entity, incrementing the index's generation count and makes the index available for future entities.<br/><br/>
    /// 
    /// Returns true if the entity was successfully deleted, otherwise false.
    /// </summary>
    public bool Destroy(EntityUid uid)
    {
        if (!Exists(uid))
            return false;

        _componentManager.RemoveAllComponents(uid);
        
        _alive.Remove(uid.Index);
        _generations[uid.Index] = checked(_generations[uid.Index] + 1);
        _freeIndices.Push(uid.Index);
        return true;
    }
}