using Android.Content;
using Android.Provider;
using LinkAudioPlayer.Abstractions;
using Application = Android.App.Application;
using AndroidUri = Android.Net.Uri;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>
/// Saves through the system "Save to" dialog. The user picks the folder, and the app
/// only ever writes the one file they chose, so no storage permission is needed.
/// </summary>
public sealed class AndroidAudioFileSaver : IAudioFileSaver
{
    /// <inheritdoc />
    public async Task<string?> PickAsync(string suggestedName, string mimeType, CancellationToken cancellationToken)
    {
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(mimeType);
        intent.PutExtra(Intent.ExtraTitle, suggestedName);

        var result = await ActivityResults.StartAsync(intent);
        return result?.Data?.ToString();
    }

    /// <inheritdoc />
    public Task WriteAsync(string handle, string sourcePath, CancellationToken cancellationToken) =>
        Task.Run(
            async () =>
            {
                var resolver = Application.Context.ContentResolver!;
                await using var output = resolver.OpenOutputStream(AndroidUri.Parse(handle)!, "w")
                    ?? throw new IOException("The chosen location cannot be written to.");
                await using var input = File.OpenRead(sourcePath);
                await input.CopyToAsync(output, cancellationToken);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string handle) =>
        Task.Run(() => DocumentsContract.DeleteDocument(Application.Context.ContentResolver!, AndroidUri.Parse(handle)!));
}
