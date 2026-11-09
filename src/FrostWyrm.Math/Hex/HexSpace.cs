using FrostWyrm.Archive.Serialization;

using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Math.Hex;

public sealed class HexSpace<T>
{
    private readonly Dictionary<Hex, T> _cells = new();

    public int Count => _cells.Count;

    public HexSpace(Dictionary<Hex, T> dict)
    {
        foreach (var (index, value) in dict)
            if (!Add(index, value))
                throw new InvalidOperationException($"Attempted to initialize HexSpace with confliction data dictionary.");
    }

    public HexSpace(T[,] arr, Hex.Orientation orientation)
    {
        for (int row = 0; row < arr.GetLength(0); row++)
        {
            for (int col = 0; col < arr.GetLength(1); col++)
            {
                Add(Hex.StructuralToHex(row, col, orientation), arr[row, col]);
            }
        }
    }

    public T Get(Hex index)
    {
        if (!index.IsCenter)
            throw new InvalidOperationException($"Attempted to get value from non-center index \"{index}\".");

        return _cells[index];
    }

    public bool TryGet(Hex index, out T value)
    {
        if (!index.IsCenter)
            throw new InvalidOperationException($"Attempted to get value from non-center index \"{index}\".");

        return _cells.TryGetValue(index, out value!);
    }

    public IEnumerable<T> GetAbout(Hex index)
    {
        if (index.IsCenter)
        {
            if (TryGet(index, out var value))
                yield return value;
            yield break;
        }

        foreach (var about in index.GetAbout())
        {
            if (TryGet(about, out var value))
                yield return value;
        }
    }

    public bool Add(Hex index, T value)
    {
        if (!index.IsCenter)
            throw new InvalidOperationException($"Attempted to add value to non-center index \"{index}\".");

        if (_cells.ContainsKey(index))
            return false;

        _cells[index] = value;
        return true;
    }

    public void SetOrAdd(Hex index, T value)
    {
        if (!index.IsCenter)
            throw new InvalidOperationException($"Attempted to modify value at non-center index \"{index}\".");

        _cells[index] = value;
    }

    public void AddAbout(Hex index, T value, bool overrideExisting = false)
    {
        if (index.IsCenter)
        {
            if (overrideExisting) SetOrAdd(index, value);
            else Add(index, value);
            return;
        }

        foreach (var about in index.GetAbout())
        {
            if (overrideExisting) SetOrAdd(about, value);
            else Add(about, value);
        }
    }

    public bool Remove(Hex index)
    {
        if (!index.IsCenter)
            throw new InvalidOperationException($"Attempted to remove value from non-center index \"{index}\".");

        return _cells.Remove(index);
    }

    public void RemoveAbout(Hex index)
    {
        if (index.IsCenter) Remove(index);
        else
            foreach (var about in index.GetAbout())
                Remove(about);
    }

    public bool Contains(Hex index)
        => _cells.ContainsKey(index);

    public IEnumerable<(Hex index, T value)> AsEnumerable()
    {
        foreach (var (index, value) in _cells)
        {
            yield return (index, value);
        }
    }


    public void Join(HexSpace<T> other, Hex offset = default, bool prioritizeSelf = true)
    {
        if (!offset.IsCenter)
            throw new ArgumentException($"A HexSpace offset must be a center-to-center displacement.", nameof(offset));

        if (offset == Hex.Zero && other == this)
            return;

        foreach (var (index, value) in other.AsEnumerable().ToArray())
        {
            if (prioritizeSelf)
                Add(index + offset, value);
            else
                SetOrAdd(index + offset, value);
        }
    }
}