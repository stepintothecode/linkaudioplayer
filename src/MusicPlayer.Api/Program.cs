using MusicPlayer.Api;
using MusicPlayer.Api.Endpoints;
using MusicPlayer.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMusicPlayer(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseExceptionHandler();

// Create the SQLite file and schema on first run.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

// Serves the Blazor client from the same origin, so the audio element needs no CORS.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapHealthEndpoints();
app.MapTrackEndpoints();
app.MapPlaylistEndpoints();
app.MapStreamEndpoints();

app.MapFallbackToFile("index.html");

app.Run();
