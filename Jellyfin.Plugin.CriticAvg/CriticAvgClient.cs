using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CriticAvg;

/// <summary>The part of the Critic AVG /api/score answer the plugin uses.</summary>
public sealed class CriticAvgScore
{
    /// <summary>Gets or sets the Critic AVG score out of 10, or null when no source had a score.</summary>
    [JsonPropertyName("score")]
    public double? Score { get; set; }

    /// <summary>Gets or sets how many sources were used.</summary>
    [JsonPropertyName("used")]
    public int Used { get; set; }

    /// <summary>Gets or sets the film title the server matched.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Gets or sets an error message, when the server could not score the film.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>Talks to the Critic AVG server.</summary>
public static class CriticAvgClient
{
    /// <summary>The presets the server understands.</summary>
    public static readonly ISet<string> Presets = new HashSet<string>(StringComparer.Ordinal)
    {
        "balanced", "equal", "critics", "audience", "cinephile", "spain"
    };

    /// <summary>Asks the server for one film's score, by TMDB id (preferred) or IMDb id.</summary>
    /// <param name="http">HTTP client.</param>
    /// <param name="serverUrl">Critic AVG server address.</param>
    /// <param name="tmdbId">TMDB id, or null.</param>
    /// <param name="imdbId">IMDb id (tt…), or null.</param>
    /// <param name="preset">Weights preset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The score, or null when the server had nothing.</returns>
    public static async Task<CriticAvgScore?> GetScoreAsync(
        HttpClient http,
        string serverUrl,
        string? tmdbId,
        string? imdbId,
        string preset,
        CancellationToken cancellationToken)
    {
        var id = !string.IsNullOrEmpty(tmdbId)
            ? "tmdb=" + Uri.EscapeDataString(tmdbId)
            : "imdb=" + Uri.EscapeDataString(imdbId ?? string.Empty);
        var safePreset = Presets.Contains(preset) ? preset : "balanced";
        var url = serverUrl.TrimEnd('/') + "/api/score?" + id + "&preset=" + safePreset;

        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException("Critic AVG server answered HTTP " + (int)response.StatusCode + " for " + url);
        }

        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<CriticAvgScore>(body, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
