using FrostWyrm.Archive.Core.Components;

namespace FrostWyrm.Archive.Test.Integration;

public sealed partial class TestComponent : IComponent
{
    public string Value = "Hello World";
}