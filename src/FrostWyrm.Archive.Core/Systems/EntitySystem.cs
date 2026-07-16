namespace FrostWyrm.Archive.Core.Systems;

public abstract class EntitySystem : ECSManager
{
    public virtual void Update(float frameTime) { }
}

public abstract class ECSManager
{
    public bool Initialized = false;
    public virtual void Initialize() { Initialized = true; }
    public virtual void Shutdown() { }

    public ECSManager() { }
}
