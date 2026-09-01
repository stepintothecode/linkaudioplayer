using Microsoft.JSInterop;

namespace LinkAudioPlayer.Ui.Services;

/// <summary>Copies text to the device clipboard from the web view.</summary>
public sealed class ClipboardService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    /// <summary>Creates the service over the Blazor JavaScript runtime.</summary>
    public ClipboardService(IJSRuntime js) => _js = js;

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
