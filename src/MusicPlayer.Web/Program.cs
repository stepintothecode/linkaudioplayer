using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MusicPlayer.Web;
using MusicPlayer.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API is served from the same origin as the client.
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});

builder.Services.AddScoped<MusicPlayerApiClient>();

// Singleton so the queue and audio element survive page navigation.
builder.Services.AddSingleton<PlayerService>();

await builder.Build().RunAsync();
