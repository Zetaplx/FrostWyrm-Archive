using FrostWyrm.Archive.Core.Components;
using FrostWyrm.Archive.Core.Entities;
using FrostWyrm.Archive.Core.IoC;
using FrostWyrm.Archive.Core.Systems;

namespace FrostWyrm.Archive.Test.Integration;

public sealed class TestSystem : EntitySystem
{
    [Dependency]
    private EntityManager _entities = default!;

    [Dependency]
    private ComponentManager _components = default!;

    public override void Initialize()
    {
        Initialized = true;
    }

    public EntityUid CreateTestEntity(string value)
    {
        var uid = _entities.Create();

        if (!_components.AddComponent(
                uid,
                new TestComponent { Value = value }))
        {
            _entities.Destroy(uid);

            throw new InvalidOperationException(
                "Failed to add TestComponent.");
        }

        return uid;
    }

    public bool TryGet(
        EntityUid uid,
        out TestComponent component)
    {
        return _components.TryGetComponent(uid, out component);
    }

    public bool Destroy(EntityUid uid)
    {
        return _entities.Destroy(uid);
    }
}