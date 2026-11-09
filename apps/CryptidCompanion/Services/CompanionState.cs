using System.Text.Json;
using CryptidCompanion.Domain;
using FrostWyrm.Archive;
using FrostWyrm.Archive.DependencyInjection;
using Microsoft.JSInterop;

namespace CryptidCompanion.Services;

public sealed class DisplayPreferences
{
    public bool ShowPieces { get; set; }
    public bool ShowCoordinates { get; set; } = true;
}

public sealed class CompletedGame
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Players { get; set; }
    public string Human { get; set; } = "";
    public DateTimeOffset CompletedAt { get; set; }
    public string Session { get; set; } = "";
    public Dictionary<string, string> Content { get; set; } = new();
}

public sealed class CompanionState : IManager
{
    public List<string> Errors { get; } = new();
    public string? Notice { get; set; }
    public Dictionary<string, string> Files { get; set; } = new();
    public Dictionary<string, string> Imports { get; set; } = new();
    public DisplayPreferences Display { get; set; } = new();
    public List<CompletedGame> PastGames { get; set; } = new();
    public string DraftCard { get; set; } = "companion-1";
    public int DraftPlayers { get; set; } = 3;
}

public sealed class BrowserSessionStore(GameManager game, ContentCatalog catalog, CompanionState state, IJSRuntime js)
{
    private readonly HashSet<Guid> _persistedGames = new();

    public async Task Initialize()
    {
        try
        {
            var display = await js.InvokeAsync<string?>("localStorage.getItem", "cryptid-display-v1");
            if (display is not null) state.Display = JsonSerializer.Deserialize<DisplayPreferences>(display) ?? new();
        }
        catch (Exception) { state.Notice = "Display preferences could not be restored. Default settings are in use."; }
        try
        {
            var history = await js.InvokeAsync<string?>("localStorage.getItem", "cryptid-history-v1");
            if (history is not null) state.PastGames = JsonSerializer.Deserialize<List<CompletedGame>>(history) ?? new();
        }
        catch (Exception) { state.Notice = "Past games could not be restored. Your current game will still be loaded."; }
        try
        {
            var packs = await js.InvokeAsync<string?>("localStorage.getItem", "cryptid-content-v1");
            if (packs is not null)
            {
                var imports = JsonSerializer.Deserialize<Dictionary<string, string>>(packs) ?? new();
                var merged = new Dictionary<string, string>(state.Files);
                foreach (var file in imports) merged[file.Key] = file.Value;
                catalog.Load(merged);
                state.Files = merged; state.Imports = imports;
            }
            var save = await js.InvokeAsync<string?>("localStorage.getItem", "cryptid-session-v1");
            if (save is not null) game.Restore(save);
            if (game.HasSession && game.Phase == SessionPhase.Won) await Save();
        }
        catch (Exception exception) { state.Notice = "Saved game/content could not be restored: " + exception.Message; }
    }

    public async Task Save()
    {
        try
        {
            if (game.HasSession)
            {
                var session = game.Export();
                var completed = game.Phase == SessionPhase.Won ? new CompletedGame
                {
                    Id = game.SessionId, Name = game.Card!.Name, Players = game.PlayerCount, Human = game.HumanBook,
                    CompletedAt = DateTimeOffset.UtcNow, Session = session, Content = new(state.Files)
                } : null;
                await js.InvokeVoidAsync("localStorage.setItem", "cryptid-session-v1", session);
                if (completed is not null)
                {
                    if (!state.PastGames.Any(g => g.Id == completed.Id)) state.PastGames.Add(completed);
                    await js.InvokeVoidAsync("localStorage.setItem", "cryptid-history-v1", JsonSerializer.Serialize(state.PastGames));
                    _persistedGames.Add(completed.Id);
                }
            }
            else await js.InvokeVoidAsync("localStorage.removeItem", "cryptid-session-v1");
        }
        catch (JSException) { state.Notice = "Browser storage could not save this game. Keep this tab open; recent progress may not survive closing it."; }
    }

    public async Task FinishCompletedGame()
    {
        if (!game.HasSession || game.Phase != SessionPhase.Won) return;
        var id = game.SessionId;
        await Save();
        game.Clear();
        // Retain the winning save if history persistence failed (for example, storage quota).
        if (_persistedGames.Contains(id)) await Save();
    }

    public async Task SavePreferences()
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", "cryptid-display-v1", JsonSerializer.Serialize(state.Display)); }
        catch (JSException) { state.Notice = "Display settings apply in this tab, but could not be saved on this browser."; }
    }

    public (GameManager Game, ContentCatalog Catalog) Review(Guid id)
    {
        var entry = state.PastGames.SingleOrDefault(g => g.Id == id)
            ?? throw new InvalidOperationException("That completed game is not saved on this browser.");
        var content = new ContentCatalog(); content.Load(entry.Content);
        var dependencies = new DependencyManager();
        dependencies.Register(content); dependencies.Register(content.Prototypes);
        var review = new GameManager(); dependencies.InjectDependencies(review);
        review.Restore(entry.Session);
        if (review.Phase != SessionPhase.Won) throw new InvalidOperationException("This journal is not a completed game.");
        return (review, content);
    }

    public async Task Import(string name, string text)
    {
        if (game.HasSession) throw new InvalidOperationException("End the active game before importing content.");
        var merged = new Dictionary<string, string>(state.Files) { [name] = text };
        catalog.Load(merged);
        state.Files = merged; state.Imports[name] = text;
        try { await js.InvokeVoidAsync("localStorage.setItem", "cryptid-content-v1", JsonSerializer.Serialize(state.Imports)); }
        catch (JSException) { state.Notice = "Content loaded for this tab, but browser storage is unavailable."; }
    }
}
