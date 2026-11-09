using System.Security.Cryptography;
using System.Text;
using FrostWyrm.Archive;
using FrostWyrm.Archive.DependencyInjection;
using FrostWyrm.Archive.Prototypes;
using FrostWyrm.Archive.Serialization;
using YamlDotNet.RepresentationModel;

namespace CryptidCompanion.Domain;

public sealed class ContentException(IEnumerable<string> errors) : Exception(string.Join("\n", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors.ToArray();
}

public sealed class ContentCatalog : IManager
{
    public IPrototypeManager Prototypes { get; private set; } = default!;
    public string Fingerprint { get; private set; } = "";
    public IReadOnlyList<SetupCardPrototype> Cards => Prototypes.EnumeratePrototypes<SetupCardPrototype>()
        .Where(c => c.Board is not null).OrderBy(c => c.ID).ToArray();

    public void Load(IReadOnlyDictionary<string, string> files)
    {
        var dependencies = new DependencyManager();
        var serialization = new SerializationManager();
        serialization.RegisterAssemblySerializers(typeof(ProtoId).Assembly);
        dependencies.Register(serialization);
        var prototypes = new PrototypeManager();
        dependencies.InjectDependencies(prototypes);
        prototypes.RegisterAssemblyPrototypes(typeof(SetupCardPrototype).Assembly);
        List<string> errors = new();
        foreach (var (file, text) in files)
        {
            try
            {
                var yaml = new YamlStream();
                yaml.Load(new StringReader(text));
                foreach (var document in yaml.Documents)
                {
                    if (document.RootNode is not YamlSequenceNode entries)
                        throw new InvalidOperationException("Expected a sequence of prototypes.");
                    foreach (var entry in entries.Children)
                    {
                        try { prototypes.LoadPrototypes(new YamlSequenceNode(entry)); }
                        catch (Exception exception)
                        {
                            var identity = entry is YamlMappingNode mapping
                                ? string.Join("/", new[] { "type", "id" }.Select(key => mapping.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value.ToString() : "?"))
                                : "entry";
                            errors.Add($"{file}, line {entry.Start.Line}, {identity}: {RootMessage(exception)}");
                        }
                    }
                }
            }
            catch (Exception exception) { errors.Add($"{file}: {RootMessage(exception)}"); }
        }
        Validate(prototypes, errors);
        if (errors.Count != 0) throw new ContentException(errors);
        Prototypes = prototypes; // Failed imports never replace a usable catalog.
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\n', files.OrderBy(p => p.Key).Select(p => p.Key + "\n" + p.Value)))));
    }

    private static string RootMessage(Exception exception)
        => exception.InnerException is null ? exception.Message : RootMessage(exception.InnerException);

    private static void Validate(IPrototypeManager prototypes, List<string> errors)
    {
        bool Reference<T>(ProtoId<T>? id, string context) where T : IPrototype
        {
            if (id is null || !prototypes.EnumeratePrototypes<T>().Any(p => p.ID == id.ID))
            { errors.Add($"{context}: missing {typeof(T).Name} '{id?.ID ?? "(empty)"}'."); return false; }
            return true;
        }
        foreach (var tile in prototypes.EnumeratePrototypes<TilePrototype>())
        {
            if (string.IsNullOrWhiteSpace(tile.ID) || tile.Cells.Count != 18 || tile.Cells.Select(c => c.Position).Distinct().Count() != 18 ||
                tile.Cells.Any(c => c.Position.Row is < 0 or > 2 || c.Position.Column is < 0 or > 5 || !Enum.IsDefined(c.Terrain) || !Enum.IsDefined(c.Animal)))
                errors.Add($"Tile {tile.ID}: expected 18 distinct cells in rows 0–2, columns 0–5 with valid terrain/animal values.");
        }
        foreach (var clue in prototypes.EnumeratePrototypes<CluePrototype>())
        {
            var valid = !string.IsNullOrWhiteSpace(clue.ID) && !string.IsNullOrWhiteSpace(clue.Text) && Enum.IsDefined(clue.Rule) &&
                clue.Terrains.All(t => Enum.IsDefined(t)) && Enum.IsDefined(clue.Animal) && Enum.IsDefined(clue.Color) && Enum.IsDefined(clue.Structure);
            valid &= clue.Rule switch
            {
                ClueRule.TerrainPair => clue.Terrains.Distinct().Count() == 2 && clue.Distance == 0,
                ClueRule.NearTerrain => clue.Terrains.Count == 1 && clue.Distance == 1,
                ClueRule.NearAnimal => clue.Distance == (clue.Animal == Animal.None ? 1 : 2),
                ClueRule.NearStructure => clue.Distance == 2,
                ClueRule.NearColor => clue.Distance == 3 && clue.Color != StructureColor.Black,
                _ => false
            };
            if (!valid) errors.Add($"Clue {clue.ID}: invalid Normal Mode rule parameters or missing text/ID.");
        }
        foreach (var book in prototypes.EnumeratePrototypes<ClueBookPrototype>())
        {
            if (book.Clues.Count == 0 || book.Clues.Select(c => c.ID).Distinct().Count() != book.Clues.Count ||
                !new[] { "red", "green", "blue", "orange", "black" }.Contains(book.Color))
                errors.Add($"Book {book.ID}: expected a player color and distinct clue references.");
            foreach (var clue in book.Clues) Reference(clue, $"Book {book.ID}");
        }
        foreach (var board in prototypes.EnumeratePrototypes<BoardPrototype>())
        {
            var context = $"Board {board.ID}";
            if (board.Rows != 9 || board.Columns != 12 || !Enum.IsDefined(board.Orientation) || board.Tiles.Count != 6 ||
                board.Tiles.Select(t => t.Tile.ID).Distinct().Count() != 6)
                errors.Add($"{context}: expected six different tiles on a 9 × 12 board and a valid orientation.");
            foreach (var structure in board.Structures)
                if (structure.Position.Row < 0 || structure.Position.Row >= board.Rows || structure.Position.Column < 0 || structure.Position.Column >= board.Columns ||
                    !Enum.IsDefined(structure.Kind) || !Enum.IsDefined(structure.Color) || structure.Color == StructureColor.Black)
                    errors.Add($"{context}: invalid Normal Mode structure at {structure.Position}.");
            if (board.Structures.Select(s => s.Position).Distinct().Count() != board.Structures.Count || board.Structures.Count != 6 ||
                board.Structures.Select(s => (s.Kind, s.Color)).Distinct().Count() != 6)
                errors.Add($"{context}: expected one stone and one shack in each of blue, green and white on distinct spaces.");
            if (board.Tiles.Select(t => Reference(t.Tile, context)).All(v => v))
                try { _ = new BoardState(board, prototypes); }
                catch (Exception exception) { errors.Add($"{context}: {exception.Message}"); }
        }
        foreach (var card in prototypes.EnumeratePrototypes<SetupCardPrototype>().Where(c => c.Board is not null))
        {
            var context = $"Card {card.ID}";
            Reference(card.Board, context);
            if (string.IsNullOrWhiteSpace(card.Name) || card.Setups.Count == 0 || card.Setups.Select(s => s.PlayerCount).Distinct().Count() != card.Setups.Count)
                errors.Add($"{context}: expected a name and distinct player-count setups.");
            foreach (var setup in card.Setups)
            {
                if (setup.PlayerCount is < 3 or > 5 || setup.Books.Count != setup.PlayerCount || setup.Books.Select(b => b.ID).Distinct().Count() != setup.PlayerCount)
                    errors.Add($"{context}: {setup.PlayerCount}-player setup needs exactly that many different books.");
                foreach (var book in setup.Books) Reference(book, context);
                foreach (var (book, clue) in setup.Assignments)
                {
                    if (!setup.Books.Any(b => b.ID == book)) errors.Add($"{context}: assignment for unused book {book}.");
                    Reference(clue, context);
                }
                try
                {
                    var board = new BoardState(prototypes.Index(card.Board!), prototypes);
                    CardClues.Resolve(setup, board, prototypes);
                }
                catch (Exception exception) { errors.Add($"{context}, {setup.PlayerCount} players: {exception.Message}"); }

            }
        }
    }
}
