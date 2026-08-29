using System.Security.Cryptography;
using System.Text;

namespace MusicPlayer.Api.Services;

/// <summary>Derives stable, filesystem safe cache keys from source URLs.</summary>
public static class CacheKeyFactory
{
    /// <summary>Returns a lowercase hex key for a source URL.</summary>
    public static string FromSourceUrl(string sourceUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sourceUrl));
        return Convert.ToHexStringLower(hash)[..32];
    }
}
