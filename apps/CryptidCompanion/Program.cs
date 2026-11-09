using CryptidCompanion;
using CryptidCompanion.Domain;
using CryptidCompanion.Services;
using FrostWyrm.Blazor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddFrostWyrm(typeof(Program).Assembly);
builder.Services.AddScoped<BrowserSessionStore>();
var host = builder.Build();
var http = host.Services.GetRequiredService<HttpClient>();
var state = host.Services.GetRequiredService<CompanionState>();
try
{
    foreach (var file in new[] { "tiles.yml", "clues.yml", "clue-books.yml", "companion-setups.yml", "setup-cards.yml" })
        state.Files[file] = await http.GetStringAsync("Resources/" + file);
    host.Services.GetRequiredService<ContentCatalog>().Load(state.Files);
}
catch (ContentException exception) { state.Errors.AddRange(exception.Errors); }
catch (Exception exception) { state.Errors.Add("Could not load game content: " + exception.Message); }
await host.RunAsync();
