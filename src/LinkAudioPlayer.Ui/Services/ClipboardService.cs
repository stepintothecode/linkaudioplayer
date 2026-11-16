using LinkAudioPlayer.Abstractions;
using Microsoft.JSInterop;

namespace LinkAudioPlayer.Ui.Services;

/// <summary>Copies text to the device clipboard from the web view, and reads it back natively.</summary>
public sealed class ClipboardService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly IClipboardReader _reader;
    private IJSObjectReference? _module;

    /// <summary>Creates the service over the Blazor JavaScript runtime and the platform clipboard.</summary>
    public ClipboardService(IJSRuntime js, IClipboardReader reader)
    {
        _js = js;
        _reader = reader;
    }

    /// <summary>Clipboard text, trimmed, or null when there is none or it cannot be read.</summary>
    public async Task<string?> PasteAsync()
    {
        try
        {
            var text = await _reader.ReadTextAsync();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch (Exception)
        {
            // Some phones refuse the read outright. Long pressing the field still works.
            return null;
        }
    }

    /// <summary>Copies text. Returns false when the web view refused.</summary>
    public async Task<bool> CopyAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            _module ??= await _js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/LinkAudioPlayer.Ui/js/clipboard.js");

            return await _module.InvokeAsync<bool>("copy", text);
        }
        catch (JSException)
        {
            return false;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The web view is already gone.
        }
    }
}
