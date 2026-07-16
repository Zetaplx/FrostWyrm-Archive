using FrostWyrm.Archive.Core.Entities;

namespace FrostWyrm.Archive.Test.Entities;

public sealed class EntityManagerLifeCycleTests
{
    [Fact]
    public void CreateReturnsLivingEntity()
    {
        var manager = new EntityManager();

        var uid = manager.Create();

        Assert.True(uid.IsValid);
        Assert.True(manager.Exists(uid));
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void CreateReturnsDistinctEntities()
    {
        var manager = new EntityManager();

        var first = manager.Create();
        var second = manager.Create();

        Assert.NotEqual(first, second);
        Assert.True(manager.Exists(first));
        Assert.True(manager.Exists(second));
        Assert.Equal(2, manager.Count);
    }

    [Fact]
    public void DestroyRemovesLivingEntity()
    {
        var manager = new EntityManager();
        var uid = manager.Create();

        var destroyed = manager.Destroy(uid);

        Assert.True(destroyed);
        Assert.False(manager.Exists(uid));
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void DestroyReturnsFalseForInvalidUid()
    {
        var manager = new EntityManager();

        var destroyed = manager.Destroy(EntityUid.Invalid);

        Assert.False(destroyed);
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void DestroyReturnsFalseForAlreadyDestroyedEntity()
    {
        var manager = new EntityManager();
        var uid = manager.Create();

        Assert.True(manager.Destroy(uid));
        Assert.False(manager.Destroy(uid));
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void DestroyDoesNotAffectOtherEntities()
    {
        var manager = new EntityManager();
        var first = manager.Create();
        var second = manager.Create();

        manager.Destroy(first);

        Assert.False(manager.Exists(first));
        Assert.True(manager.Exists(second));
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void DestroyedIndexIsReusedWithNewGeneration()
    {
        var manager = new EntityManager();
        var oldUid = manager.Create();

        manager.Destroy(oldUid);
        var newUid = manager.Create();

        Assert.Equal(oldUid.Index, newUid.Index);
        Assert.Equal(oldUid.Generation + 1, newUid.Generation);

        Assert.False(manager.Exists(oldUid));
        Assert.True(manager.Exists(newUid));
    }

    [Fact]
    public void StaleUidCannotDestroyReplacementEntity()
    {
        var manager = new EntityManager();
        var staleUid = manager.Create();

        manager.Destroy(staleUid);
        var replacementUid = manager.Create();

        var destroyed = manager.Destroy(staleUid);

        Assert.False(destroyed);
        Assert.True(manager.Exists(replacementUid));
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void ExistsReturnsFalseForUnknownIndex()
    {
        var manager = new EntityManager();
        var unknownUid = new EntityUid(50, 0);

        Assert.False(manager.Exists(unknownUid));
    }

    [Fact]
    public void FreeIndicesAreReusedBeforeCreatingNewSlots()
    {
        var manager = new EntityManager();

        var first = manager.Create();
        var second = manager.Create();
        var third = manager.Create();

        manager.Destroy(second);

        var replacement = manager.Create();

        Assert.Equal(second.Index, replacement.Index);
        Assert.NotEqual(second.Generation, replacement.Generation);
        Assert.Equal(3, manager.Count);

        Assert.True(manager.Exists(first));
        Assert.True(manager.Exists(third));
        Assert.True(manager.Exists(replacement));
    }
}