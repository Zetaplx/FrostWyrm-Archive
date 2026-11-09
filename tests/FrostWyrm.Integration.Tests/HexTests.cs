using FrostWyrm.Archive.Serialization;
using FrostWyrm.Math.Hex;
using YamlDotNet.RepresentationModel;

namespace FrostWyrm.Integration.Tests;

public sealed class HexTests
{
    [Fact]
    public void EquivalentThreeAxisCoordinatesHaveTheSameIdentity()
    {
        Assert.Equal(new Hex(4, 5, 6), new Hex(-2, -1));
        Assert.Equal(new Hex(4, 5, 6).GetHashCode(), new Hex(-2, -1).GetHashCode());
        Assert.Equal(Hex.Zero, Hex.Alpha + Hex.Beta + Hex.Gamma);
    }

    [Theory]
    [InlineData(3, 0, 2)]
    [InlineData(-3, 0, 2)]
    [InlineData(0, 0, 0)]
    public void DistanceIsSymmetric(int a, int b, float expected)
    {
        var hex = new Hex(a, b);
        Assert.Equal(expected, Hex.Distance(hex, Hex.Zero));
        Assert.Equal(expected, Hex.Distance(Hex.Zero, hex));
    }

    [Theory]
    [InlineData(Hex.Orientation.FlatTop)]
    [InlineData(Hex.Orientation.PointTop)]
    public void ArrayCoordinatesProduceDistinctCenterCells(Hex.Orientation orientation)
    {
        var space = new HexSpace<int>(new[,] { { 1, 2 }, { 3, 4 } }, orientation);

        Assert.Equal(4, space.Count);
        Assert.All(space.AsEnumerable(), cell => Assert.True(cell.index.IsCenter));
        Assert.Equal(4, space.Get(Hex.StructuralToHex(1, 1, orientation)));
    }

    [Fact]
    public void VertexAccessTouchesItsThreeAdjacentCells()
    {
        var vertex = new Hex(-1, 0);
        var space = new HexSpace<string>(new Dictionary<Hex, string>());

        space.AddAbout(vertex, "tile");

        Assert.Equal(3, space.Count);
        Assert.Equal(3, space.GetAbout(vertex).Count());
        Assert.All(vertex.GetAbout(), cell => Assert.True(cell.IsCenter));
        Assert.Throws<InvalidOperationException>(() => space.Get(vertex));
        space.RemoveAbout(vertex);
        Assert.Equal(0, space.Count);
    }

    [Fact]
    public void JoinAppliesOffsetAndHonorsConflictPriority()
    {
        var offset = new Hex(3, 0);
        var target = new HexSpace<string>(new Dictionary<Hex, string> { [offset] = "original" });
        var source = new HexSpace<string>(new Dictionary<Hex, string> { [Hex.Zero] = "incoming" });

        target.Join(source, offset);
        Assert.Equal("original", target.Get(offset));
        target.Join(source, offset, prioritizeSelf: false);
        Assert.Equal("incoming", target.Get(offset));
        Assert.Throws<ArgumentException>(() => target.Join(source, new Hex(1, 0)));
    }

    [Fact]
    public void MathAssemblyRegistersItsHexSerializer()
    {
        var serialization = new SerializationManager();
        serialization.RegisterAssemblySerializers(typeof(Hex).Assembly);

        Assert.Equal(Hex.StructuralToHex(1, 2, Hex.Orientation.FlatTop),
            serialization.DeserializeNode<Hex>(new YamlScalarNode("2,1")));
        Assert.Equal(new Hex(1, 2, 3),
            serialization.DeserializeNode<Hex>(new YamlScalarNode("1,2,3")));
    }
}
