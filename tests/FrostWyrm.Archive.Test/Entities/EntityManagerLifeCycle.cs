using FrostWyrm.Archive.Core;
using FrostWyrm.Archive.Core.Entities;

namespace FrostWyrm.Archive.Test.Entities;

public sealed class EntityManagerLifeCycleTests : IDisposable
{
    private readonly App _app;
    private readonly EntityManager _manager;

    public EntityManagerLifeCycleTests()
    {
        _app = new AppBuilder().Build();
        _app.IoCManager.TryResolve(out _manager);
    }

    public void Dispose()
    {
        _app.Shutdown();
    }

    [Fact]
    public void CreateReturnsLivingEntity()
    {
        var uid = _manager.Create();

        Assert.True(uid.IsValid);
        Assert.True(_manager.Exists(uid));
        Assert.Equal(1, _manager.Count);
    }

    [Fact]
    public void CreateReturnsDistinctEntities()
    {
        var first = _manager.Create();
        var second = _manager.Create();

        Assert.NotEqual(first, second);
        Assert.True(_manager.Exists(first));
        Assert.True(_manager.Exists(second));
        Assert.Equal(2, _manager.Count);
    }

    [Fact]
    public void DestroyRemovesLivingEntity()
    {
        var uid = _manager.Create();

        var destroyed = _manager.Destroy(uid);

        Assert.True(destroyed);
        Assert.False(_manager.Exists(uid));
        Assert.Equal(0, _manager.Count);
    }

    [Fact]
    public void DestroyReturnsFalseForInvalidUid()
    {
        var destroyed = _manager.Destroy(EntityUid.Invalid);

        Assert.False(destroyed);
        Assert.Equal(0, _manager.Count);
    }

    [Fact]
    public void DestroyReturnsFalseForAlreadyDestroyedEntity()
    {
        var uid = _manager.Create();

        Assert.True(_manager.Destroy(uid));
        Assert.False(_manager.Destroy(uid));
        Assert.Equal(0, _manager.Count);
    }

    [Fact]
    public void DestroyDoesNotAffectOtherEntities()
    {
        var first = _manager.Create();
        var second = _manager.Create();

        _manager.Destroy(first);

        Assert.False(_manager.Exists(first));
        Assert.True(_manager.Exists(second));
        Assert.Equal(1, _manager.Count);
    }

    [Fact]
    public void DestroyedIndexIsReusedWithNewGeneration()
    {
        var oldUid = _manager.Create();

        _manager.Destroy(oldUid);
        var newUid = _manager.Create();

        Assert.Equal(oldUid.Index, newUid.Index);
        Assert.Equal(oldUid.Generation + 1, newUid.Generation);

        Assert.False(_manager.Exists(oldUid));
        Assert.True(_manager.Exists(newUid));
    }

    [Fact]
    public void StaleUidCannotDestroyReplacementEntity()
    {
        var staleUid = _manager.Create();

        _manager.Destroy(staleUid);
        var replacementUid = _manager.Create();

        var destroyed = _manager.Destroy(staleUid);

        Assert.False(destroyed);
        Assert.True(_manager.Exists(replacementUid));
        Assert.Equal(1, _manager.Count);
    }

    [Fact]
    public void ExistsReturnsFalseForUnknownIndex()
    {
        var unknownUid = new EntityUid(50, 0);

        Assert.False(_manager.Exists(unknownUid));
    }

    [Fact]
    public void FreeIndicesAreReusedBeforeCreatingNewSlots()
    {
        var first = _manager.Create();
        var second = _manager.Create();
        var third = _manager.Create();

        _manager.Destroy(second);

        var replacement = _manager.Create();

        Assert.Equal(second.Index, replacement.Index);
        Assert.NotEqual(second.Generation, replacement.Generation);
        Assert.Equal(3, _manager.Count);

        Assert.True(_manager.Exists(first));
        Assert.True(_manager.Exists(third));
        Assert.True(_manager.Exists(replacement));
    }
}