using FrostWyrm.Archive.Prototypes;

namespace CryptidCompanion.Domain;

public sealed class GameRandom
{
    private readonly Random _random;
    public int Seed { get; }
    public int Draws { get; private set; }
    public GameRandom(int seed, int draws = 0)
    {
        Seed = seed;
        _random = new(seed);
        for (var i = 0; i < draws; i++) Next(int.MaxValue);
    }
    private int Next(int max) { Draws++; return _random.Next() % max; }
    public T Choose<T>(IReadOnlyList<T> values) => values.Count != 0 ? values[Next(values.Count)]
        : throw new InvalidOperationException("No legal random selection exists.");
    public T[] Shuffle<T>(IEnumerable<T> values)
    {
        var result = values.ToArray();
        for (var i = result.Length - 1; i > 0; i--)
        { var j = Next(i + 1); (result[i], result[j]) = (result[j], result[i]); }
        return result;
    }
}

public sealed class ClueSelection
{
    private readonly IPrototypeManager _prototypes;
    private readonly PlayerSetup _setup;
    private readonly Dictionary<string, HashSet<Position>> _matches = new();
    public ClueSelection(BoardState board, PlayerSetup setup, IPrototypeManager prototypes)
    {
        _setup = setup;
        _prototypes = prototypes;
        foreach (var clue in prototypes.EnumeratePrototypes<CluePrototype>())
            _matches[clue.ID] = ClueEvaluator.Evaluate(clue, board);
    }

    public IReadOnlyList<CluePrototype> Candidates(string book)
    {
        var definition = _prototypes.Index<ClueBookPrototype>(book);
        var ids = _setup.Candidates.TryGetValue(book, out var restricted) ? restricted : definition.Clues;
        return ids.Select(id => _prototypes.Index(id)).Where(c => _matches[c.ID].Count is >= 1 and <= 106).ToArray();
    }

    public Dictionary<string, string>? Find(string humanBook, string humanClue, GameRandom random)
    {
        if (!_setup.Books.Any(b => b.ID == humanBook) || !Candidates(humanBook).Any(c => c.ID == humanClue)) return null;
        var books = _setup.Books.Where(b => b.ID != humanBook).Select(b => b.ID).ToArray();
        var pools = books.ToDictionary(b => b, b => random.Shuffle(Candidates(b).Select(c => c.ID)));
        var assignment = new Dictionary<string, string> { [humanBook] = humanClue };
        return Search(0, _matches[humanClue]) ? assignment : null;

        bool Search(int depth, HashSet<Position> intersection)
        {
            if (depth == books.Length) return intersection.Count == 1;
            foreach (var clue in pools[books[depth]])
            {
                if (assignment.Values.Contains(clue)) continue;
                var next = intersection.Intersect(_matches[clue]).ToHashSet();
                if (next.Count == 0) continue;
                assignment[books[depth]] = clue;
                if (Search(depth + 1, next)) return true;
                assignment.Remove(books[depth]);
            }
            return false;
        }
    }
}
