using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CryptidCompanion.Domain;

using FrostWyrm.Archive;
using FrostWyrm.Archive.DependencyInjection;
using FrostWyrm.Archive.Prototypes;

namespace CryptidCompanion;

public sealed class GameManager : IManager
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ContentCatalog _catalog = default!;

    private Dictionary<string, string> _assignments = new();
    private readonly List<Marker> _markers = new();
    private readonly List<TurnRecord> _history = new();
    private GameRandom _random = new(0);
    public SetupCardPrototype? Card { get; private set; }
    public BoardState? Board { get; private set; }
    public string HumanBook { get; private set; } = "";
    public string HumanClueText => HasSession ? _catalog.Prototypes.Index<CluePrototype>(_assignments[HumanBook]).Text : "";
    public bool HasSession => Card is not null;
    public SessionPhase Phase { get; private set; }
    public int PendingHumanCubes { get; private set; }
    public IReadOnlyList<Marker> Markers => _markers.AsReadOnly();
    public IReadOnlyList<TurnRecord> History => _history.AsReadOnly();
    public IReadOnlyList<string> Books => CurrentSetup?.Books.Select(b => b.ID).ToArray() ?? Array.Empty<string>();
    public IReadOnlyList<string> Opponents => Books.Where(b => b != HumanBook).ToArray();
    public int Seed => _random.Seed;

    public int CardNumber { get; private set; }
    public int PlayerCount { get; private set; }
    public PlayerSetup? CurrentSetup { get; private set; }

    public void Begin(string cardId, int players, string human, int? seed = null)
    {
        var (card, setup, board) = Prepare(cardId, players);
        if (!setup.Books.Any(b => b.ID == human)) throw new InvalidOperationException("Choose a color used by this setup.");
        var assignments = CardClues.Resolve(setup, board, _catalog.Prototypes);
        var random = new GameRandom(seed ?? RandomNumberGenerator.GetInt32(int.MaxValue));
        Card = card; CurrentSetup = setup; Board = board; PlayerCount = players;
        CardNumber = int.TryParse(card.ID, out var number) ? number : 0;
        HumanBook = human; _assignments = assignments; _random = random;
        _markers.Clear(); _history.Clear(); Phase = SessionPhase.Opening; PendingHumanCubes = 2;
        foreach (var book in Opponents)
            for (var i = 0; i < 2; i++) AddRandomCube(book, "Opening");
    }

    private (SetupCardPrototype Card, PlayerSetup Setup, BoardState Board) Prepare(string cardId, int players)
    {
        var card = _catalog.Cards.FirstOrDefault(c => c.ID == cardId)
            ?? throw new InvalidOperationException("Choose an available setup with complete board data.");
        var setup = card.Setups.SingleOrDefault(s => s.PlayerCount == players)
            ?? throw new InvalidOperationException("This card does not support that player count.");
        return (card, setup, new BoardState(_catalog.Prototypes.Index(card.Board!), _catalog.Prototypes));
    }

    public void StartPlay()
    {
        if (!HasSession || Phase != SessionPhase.Opening || PendingHumanCubes != 0)
            throw new InvalidOperationException("Place both of your opening cubes first.");
        Phase = SessionPhase.Playing;
    }

    public void PlaceHumanCube(Position position)
    {
        if (!HasSession || PendingHumanCubes <= 0 || Phase == SessionPhase.Won)
            throw new InvalidOperationException("No cube placement is due.");
        if (Matches(HumanBook, position)) throw new InvalidOperationException("Your cube must go on a space that fails your clue.");
        AddMarker(HumanBook, position, Piece.Cube, Phase == SessionPhase.Opening ? "Opening" : "Search/question penalty");
        PendingHumanCubes--;
    }

    public TurnRecord Ask(string book, Position position)
    {
        EnsureTurn(position);
        if (!Opponents.Contains(book)) throw new InvalidOperationException("Choose a simulated clue-holder.");
        if (OwnMarker(book, position)) throw new InvalidOperationException("That clue-holder already has a piece on this space.");
        var marker = AddMarker(book, position, Matches(book, position) ? Piece.Disc : Piece.Cube, "Question");
        if (marker.Piece == Piece.Cube) PendingHumanCubes = 1;
        return Record("Question " + book, position, new[] { marker }, false);
    }

    public TurnRecord Search(Position position)
    {
        EnsureTurn(position);
        if (!Matches(HumanBook, position)) throw new InvalidOperationException("You may only search a space that satisfies your own clue.");
        List<Marker> placed = new();
        var penalties = new List<string> { HumanBook };
        if (!OwnMarker(HumanBook, position)) placed.Add(AddMarker(HumanBook, position, Piece.Disc, "Search"));
        var humanIndex = Books.ToList().IndexOf(HumanBook);
        for (var offset = 1; offset < Books.Count; offset++)
        {
            var book = Books[(humanIndex + offset) % Books.Count];
            if (OwnMarker(book, position)) continue; // Existing discs already answer yes; no new piece is placed.
            var agrees = Matches(book, position);
            placed.Add(AddMarker(book, position, agrees ? Piece.Disc : Piece.Cube, "Search"));
            if (!agrees)
            {
                // Stop at the first no: later clue-holders reveal nothing.
                foreach (var participant in penalties.Where(p => p != HumanBook))
                    placed.Add(AddRandomCube(participant, "Failed search penalty"));
                PendingHumanCubes = 1;
                return Record("Search stopped at " + book, position, placed, false);
            }
            penalties.Add(book);
        }
        Phase = SessionPhase.Won;
        return Record("Habitat found", position, placed, true);
    }

    public bool HasCube(Position position) => _markers.Any(m => m.Position == position && m.Piece == Piece.Cube);
    public bool OwnMarker(string book, Position position) => _markers.Any(m => m.Position == position && m.Book == book);
    private bool Matches(string book, Position position) => ClueEvaluator.Matches(_catalog.Prototypes.Index<CluePrototype>(_assignments[book]), Board!, position);
    private void EnsureTurn(Position position)
    {
        if (!HasSession || Phase != SessionPhase.Playing) throw new InvalidOperationException("Finish opening setup before taking a turn, or start a new game after winning.");
        if (PendingHumanCubes != 0) throw new InvalidOperationException("Place your required cube before another action.");
        _ = Board![position];
        if (HasCube(position)) throw new InvalidOperationException("You cannot question or search a space containing any cube.");
    }
    private Marker AddMarker(string book, Position position, Piece piece, string reason)
    {
        _ = Board![position];
        if (OwnMarker(book, position)) throw new InvalidOperationException($"{book} already has a piece at {position}.");
        var marker = new Marker(book, position, piece, reason);
        _markers.Add(marker);
        return marker;
    }
    private Marker AddRandomCube(string book, string reason)
    {
        var legal = Board!.Spaces.Where(s => !Matches(book, s.Position) && !OwnMarker(book, s.Position)).Select(s => s.Position).ToArray();
        return AddMarker(book, _random.Choose(legal), Piece.Cube, reason);
    }
    private TurnRecord Record(string action, Position position, IReadOnlyList<Marker> placements, bool success)
    {
        var record = new TurnRecord(_history.Count + 1, action, position, placements, success);
        _history.Add(record);
        return record;
    }

    public string Export() => JsonSerializer.Serialize(new SessionSave
    {
        Catalog = _catalog.Fingerprint, Card = Card!.ID, Players = PlayerCount, Human = HumanBook,
        Seed = _random.Seed, Draws = _random.Draws, Assignments = _assignments, Markers = _markers,
        History = _history, Phase = Phase, PendingHumanCubes = PendingHumanCubes
    });

    public void Restore(string json)
    {
        var save = JsonSerializer.Deserialize<SessionSave>(json) ?? throw new InvalidOperationException("Empty saved game.");
        if (save.Version != 1 || save.Catalog != _catalog.Fingerprint) throw new InvalidOperationException("This save uses different content. Start a new game or restore its original content pack.");
        var (card, setup, board) = Prepare(save.Card, save.Players);
        var expected = CardClues.Resolve(setup, board, _catalog.Prototypes);
        if (save.Assignments.Count != setup.Books.Count || !save.Assignments.ContainsKey(save.Human) || save.Assignments.Values.Distinct().Count() != setup.Books.Count ||
            setup.Books.Any(b => !save.Assignments.TryGetValue(b.ID, out var clue) || expected[b.ID] != clue))
            throw new InvalidOperationException("Saved clue assignments are invalid.");
        var intersection = board.Spaces.Select(s => s.Position).ToHashSet();
        foreach (var clue in save.Assignments.Values) intersection.IntersectWith(ClueEvaluator.Evaluate(_catalog.Prototypes.Index<CluePrototype>(clue), board));
        if (intersection.Count != 1 || !Enum.IsDefined(save.Phase) || save.PendingHumanCubes is < 0 or > 2 || save.Draws is < 0 or > 1000000 ||
            save.Markers.Select(m => (m.Book, m.Position)).Distinct().Count() != save.Markers.Count)
            throw new InvalidOperationException("Saved game state is invalid.");
        foreach (var marker in save.Markers)
            if (!save.Assignments.TryGetValue(marker.Book, out var clue) || !Enum.IsDefined(marker.Piece) ||
                ClueEvaluator.Matches(_catalog.Prototypes.Index<CluePrototype>(clue), board, marker.Position) != (marker.Piece == Piece.Disc))
                throw new InvalidOperationException("Saved pieces contradict their clues.");
        Card = card; CurrentSetup = setup; Board = board; HumanBook = save.Human; PlayerCount = save.Players;
        _assignments = save.Assignments; _random = new(save.Seed, save.Draws);
        _markers.Clear(); _markers.AddRange(save.Markers); _history.Clear(); _history.AddRange(save.History);
        Phase = save.Phase; PendingHumanCubes = save.PendingHumanCubes;
    }

    public void Clear()
    {
        Card = null; Board = null; CurrentSetup = null; HumanBook = "";
        _markers.Clear(); _history.Clear(); _assignments.Clear(); PendingHumanCubes = 0;
    }

    public void SetupStart(int cardNumber, int playerCount)
    {
        if (cardNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(cardNumber), "Choose a positive card number.");
        if (playerCount is < 3 or > 5)
            throw new ArgumentOutOfRangeException(nameof(playerCount), "Choose between 3 and 5 players.");

        var card = _prototypes.EnumeratePrototypes<SetupCardPrototype>()
            .SingleOrDefault(card => card.ID == cardNumber.ToString(CultureInfo.InvariantCulture))
            ?? throw new InvalidOperationException($"Setup card {cardNumber} is not available.");
        var setup = card.Setups.SingleOrDefault(setup => setup.PlayerCount == playerCount)
            ?? throw new InvalidOperationException($"Card {cardNumber} has no setup for {playerCount} players.");

        CardNumber = cardNumber;
        PlayerCount = playerCount;
        CurrentSetup = setup;
    }
}
