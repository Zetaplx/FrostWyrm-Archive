namespace CryptidCompanion.Domain;

public enum SessionPhase { Opening, Playing, Won }
public enum Piece { Cube, Disc }
public sealed record Marker(string Book, Position Position, Piece Piece, string Reason);
public sealed record TurnRecord(int Number, string Action, Position Position, IReadOnlyList<Marker> Placements, bool Success);

// A versioned local save, never used as a UI view model. Hidden assignments stay in session state.
public sealed class SessionSave
{
    public int Version { get; set; } = 1;
    public string Catalog { get; set; } = "";
    public string Card { get; set; } = "";
    public int Players { get; set; }
    public string Human { get; set; } = "";
    public int Seed { get; set; }
    public int Draws { get; set; }
    public Dictionary<string, string> Assignments { get; set; } = new();
    public List<Marker> Markers { get; set; } = new();
    public List<TurnRecord> History { get; set; } = new();
    public SessionPhase Phase { get; set; }
    public int PendingHumanCubes { get; set; }
}
