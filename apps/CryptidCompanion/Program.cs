using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CryptidCompanion;

using FrostWyrm.Blazor;
using FrostWyrm.Archive.Prototypes;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddFrostWyrm(typeof(Program).Assembly);

var protoMan = FrostWyrm.Archive.FrostWyrm.Resolve<IPrototypeManager>();

var host = builder.Build();
var http = host.Services.GetRequiredService<HttpClient>();
var yaml = await http.GetStringAsync("Resources/setup-cards.yml");
using var reader = new StringReader(yaml);
protoMan.LoadPrototypes(reader);

await host.RunAsync();
