using LinkAudioPlayer.Library;

namespace LinkAudioPlayer.Ui.Services;

/// <summary>
/// Runs one save at a time and holds its progress outside any page, so leaving the
/// library mid save neither cancels it nor loses the status when you come back.
/// </summary>
public sealed class AudioSaveTracker
{
    private readonly AudioExportService _export;
    private readonly UiErrorState _errors;
    private readonly ToastService _toasts;

    /// <summary>Creates the tracker.</summary>
    public AudioSaveTracker(AudioExportService export, UiErrorState errors, ToastService toasts)
    {
        _export = export;
        _errors = errors;
        _toasts = toasts;
    }

    /// <summary>Raised whenever the status changes.</summary>
    public event Action? Changed;

    /// <summary>True while a save is running.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>What the running save is doing, for display. Null when idle.</summary>
    public string? Status { get; private set; }

    /// <summary>Saves a link's audio. Ignored while another save is running.</summary>
    public async Task SaveAsync(string url, AudioFormat format)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var label = format == AudioFormat.Mp3 ? "MP3" : "M4A";
        Update(busy: true, "Choose where to save");

        var progress = new Progress<ExportProgress>(step =>
        {
            // A report can be delivered after the save finished, and must not revive the status.
            if (IsBusy)
            {
                Update(busy: true, Describe(step, label));
            }
        });

        string? saved = null;
        var ok = await _errors.TryAsync(
            $"Saving the {label}",
            async () => saved = await _export.SaveAsync(url, format, progress, CancellationToken.None));

        Update(busy: false, status: null);

        if (ok && saved is not null)
        {
            _toasts.Show($"Saved {saved}", "download");
        }
    }

    private void Update(bool busy, string? status)
    {
        IsBusy = busy;
        Status = status;
        Changed?.Invoke();
    }

    /// <summary>"Downloading 40%", "Converting to MP3 75%", "Saving".</summary>
    private static string Describe(ExportProgress step, string label) => step.Stage switch
    {
        ExportStage.Downloading => $"Downloading {Percent(step.Fraction)}",
        ExportStage.Converting => $"Converting to {label} {Percent(step.Fraction)}",
        _ => $"Saving the {label}",
    };

    private static string Percent(double fraction) => $"{Math.Clamp((int)(fraction * 100), 0, 100)}%";
}
