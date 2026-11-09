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

public static class CardClues
{
    public static Dictionary<string, string> Resolve(PlayerSetup setup, BoardState board, IPrototypeManager prototypes)
    {
        var books = setup.Books.Select(b => b.ID).ToArray();
        if (setup.Assignments.Count != books.Length || books.Any(b => !setup.Assignments.ContainsKey(b)) ||
            setup.Assignments.Values.Select(c => c.ID).Distinct().Count() != books.Length)
            throw new InvalidOperationException("The setup must assign exactly one distinct clue to each book.");
        var intersection = board.Spaces.Select(s => s.Position).ToHashSet();
        foreach (var (book, id) in setup.Assignments)
        {
            if (!prototypes.Index<ClueBookPrototype>(book).Clues.Any(c => c.ID == id.ID))
                throw new InvalidOperationException($"Clue {id.ID} is not in book {book}.");
            var matches = ClueEvaluator.Evaluate(prototypes.Index(id), board);
            if (matches.Count == 0 || board.Spaces.Count - matches.Count < 2)
                throw new InvalidOperationException($"Clue {id.ID} needs possible habitats and at least two legal opening cubes.");
            intersection.IntersectWith(matches);
        }
        if (intersection.Count != 1)
            throw new InvalidOperationException($"Card-defined clues must identify exactly one habitat; found {intersection.Count}.");
        return setup.Assignments.ToDictionary(p => p.Key, p => p.Value.ID);
    }
}
