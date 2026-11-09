using FrostWyrm.Archive.Serialization;

using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Math.Hex;

public readonly record struct Hex
{
    public int A { get; }
    public int B { get; }
    public int C => 0;

    public static readonly Hex Alpha = (1, 0, 0);
    public static readonly Hex Beta = (0, 1, 0);
    public static readonly Hex Gamma = (0, 0, 1);
    public static readonly Hex Zero = (0, 0, 0);

    public Hex(int a, int b, int c = 0)
    {
        A = a - c;
        B = b - c;
    }

    public Hex(params int[] values)
    {
        if (values.Length < 2 || values.Length > 3)
            throw new InvalidOperationException($"Constructed Hex values must have 2 or 3 elements.");

        var (a, b, c) = (values[0], values[1], values.Length == 3 ? values[2] : 0);
        A = a - c;
        B = b - c;
    }

    public bool IsCenter => (A + B) % 3 == 0;

    public float Length
    {
        get
        {
            var k = (A + B) / 3.0f;
            var (a, b, c) = (A - k, B - k, -k);
            if (a < 0) a = -a;
            if (b < 0) b = -b;
            if (c < 0) c = -c;
            return (a + b + c) / 2.0f;
        }
    }
    public static float Distance(Hex a, Hex b) => (b - a).Length;

    public (int a, int b, int c) Normalized()
    {
        var k = (A + B) / 3;
        if ((A + B) < 0 && (A + B) % 3 != 0)
            k--;

        return (A - k, B - k, -k);
    }

    public Hex[] GetAbout()
    {
        var remainder = (((A + B) % 3) + 3) % 3;

        if (remainder == 0)
            throw new InvalidOperationException($"GetAbout requires a vertex position.");

        var delta = remainder == 1 ? -1 : 1;

        return [
            this + new Hex(delta, 0),
            this + new Hex(0, delta),
            this - new Hex(delta, delta)
        ];
    }

    public static Hex operator +(Hex a, Hex b) => new Hex(a.A + b.A, a.B + b.B);
    public static Hex operator -(Hex a) => new Hex(-a.A, -a.B);
    public static Hex operator -(Hex a, Hex b) => a + (-b);
    public static Hex operator *(int c, Hex a) => new Hex(a.A * c, a.B * c, a.C * c);
    public static Hex operator *(Hex a, int c) => c * a;

    public static implicit operator Hex((int a, int b) t) => new Hex(t.a, t.b);
    public static implicit operator Hex((int a, int b, int c) t) => new Hex(t.a, t.b, t.c);

    public void Deconstruct(out int a, out int b)
        => (a, b) = (A, B);
    public void Deconstruct(out int a, out int b, out int c)
        => (a, b, c) = Normalized();

    public override string ToString()
    {
        var (a, b, c) = Normalized();
        return $"<{a}, {b}, {c}>";
    }

    public static Hex StructuralToHex(int row, int column, Orientation orientation)
    {
        if (orientation == Orientation.FlatTop)
        {
            var fullCols = column / 2;
            var a = 2 * column + row - fullCols;
            var b = 2 * row + column - 2 * fullCols;
            return (a, b);
        }

        if (orientation == Orientation.PointTop)
        {
            var fullRows = row / 2;
            var a = -column + row + fullRows;
            var b = -2 * column - row + 2 * fullRows;
            return (a, b);
        }

        throw new InvalidOperationException($"Invalid orientation \"{orientation}\".");
    }

    public enum Orientation
    {
        FlatTop,
        PointTop,
    }
}

[DefaultTypeSerializer]
public sealed class HexSerializer : ISerializer<YamlScalarNode, Hex>
{
    public static Hex.Orientation DefaultOrientation = Hex.Orientation.FlatTop;

    public Hex Deserialize(YamlScalarNode node)
    {
        List<int> values = [.. node.Value!.Trim().Split(",").Select(int.Parse)];

        if (values.Count == 2)
            return Hex.StructuralToHex(values[1], values[0], DefaultOrientation);
        return new Hex(values.ToArray());
    }

    public bool Validate(YamlScalarNode node)
    {
        if (node.Value is not { } value)
            return false;

        value = value.Trim();
        var split = value.Split(",");
        if (split.Length is < 2 or > 3)
            return false;

        foreach (var i in split)
        {
            if (!int.TryParse(i, out _))
                return false;
        }

        return true;
    }
}