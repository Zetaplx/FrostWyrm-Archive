namespace FrostWyrm.Archive.Core.Systems;

public abstract class EntitySystem : ECSManager
{
    public virtual void Update(float frameTime) { }
}

public abstract class ECSManager
{
    public virtual void Initialize() { }
    public virtual void Shutdown() { }

    public ECSManager() { }
}
