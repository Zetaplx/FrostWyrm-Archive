namespace FrostWyrm.Archive.Core.Entities;

/// <summary>
/// Identifies a specific entity instance within an App.<br/><br/>
/// 
/// The index identifies an empty slot, while the generation distinguishes between entities that have occupied the same slot.
/// </summary>
public readonly record struct EntityUid(int Index, uint Generation)
{
    public static readonly EntityUid Invalid = new(-1, 0);

    public bool IsValid => Index >= 0;

    public override string ToString()
        => IsValid ? $"{Index}:{Generation}" : "Invalid";
}