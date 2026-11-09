namespace FrostWyrm.Math.Hex;

/// <summary>Integer axial coordinates for cell centers, distinct from the legacy vertex-capable Hex.</summary>
public readonly record struct AxialHex(int Q, int R)
{
    public int DistanceTo(AxialHex other)
        => (System.Math.Abs(Q - other.Q) + System.Math.Abs(R - other.R) + System.Math.Abs(Q + R - other.Q - other.R)) / 2;

    public IEnumerable<AxialHex> Neighbors()
    {
        yield return new(Q + 1, R);
        yield return new(Q + 1, R - 1);
        yield return new(Q, R - 1);
        yield return new(Q - 1, R);
        yield return new(Q - 1, R + 1);
        yield return new(Q, R + 1);
    }
}

public static class HexCoordinates
{
    public static AxialHex FromOffset(int row, int column, Hex.Orientation orientation)
    {
        // Preserve the original structural conversion, including C# truncation for negative integers.
        var legacy = Hex.StructuralToHex(row, column, orientation);
        return new((2 * legacy.A - legacy.B) / 3, (2 * legacy.B - legacy.A) / 3);
    }

    public static (int Row, int Column) ToOffset(AxialHex hex, Hex.Orientation orientation)
        => orientation switch
        {
            Hex.Orientation.FlatTop => (hex.R + hex.Q / 2, hex.Q),
            Hex.Orientation.PointTop => (hex.Q, -hex.R - hex.Q + hex.Q / 2),
            _ => throw new ArgumentOutOfRangeException(nameof(orientation))
        };

    public static (double X, double Y) ToPixel(AxialHex hex, Hex.Orientation orientation, double radius)
        => orientation switch
        {
            Hex.Orientation.FlatTop => (radius * 1.5 * hex.Q, radius * System.Math.Sqrt(3) * (hex.R + hex.Q / 2.0)),
            Hex.Orientation.PointTop => (radius * System.Math.Sqrt(3) * (-hex.R - hex.Q / 2.0), radius * 1.5 * hex.Q),
            _ => throw new ArgumentOutOfRangeException(nameof(orientation))
        };
}
