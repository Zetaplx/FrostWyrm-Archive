using System.Text.Json;
using CryptidCompanion.Domain;
using FrostWyrm.Archive;
using Microsoft.JSInterop;

namespace CryptidCompanion.Services;

public sealed class CompanionState : IManager
{
    public List<string> Errors { get; } = new();
    public string? Notice { get; set; }
    public Dictionary<string, string> Files { get; set; } = new();
    public Dictionary<string, string> Imports { get; set; } = new();
    public string DraftCard { get; set; } = "companion-1";
    public int DraftPlayers { get; set; } = 3;
}

public sealed class BrowserSessionStore(GameManager game, ContentCatalog catalog, CompanionState state, IJSRuntime js)
{
    public async Task Initialize()
    {
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
        }
        catch (Exception exception) { state.Notice = "Saved game/content could not be restored: " + exception.Message; }
    }

    public async Task Save()
    {
        try
        {
            if (game.HasSession) await js.InvokeVoidAsync("localStorage.setItem", "cryptid-session-v1", game.Export());
            else await js.InvokeVoidAsync("localStorage.removeItem", "cryptid-session-v1");
        }
        catch (JSException) { state.Notice = "Browser storage is unavailable. Keep this tab open; this game cannot be resumed after closing it."; }
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
