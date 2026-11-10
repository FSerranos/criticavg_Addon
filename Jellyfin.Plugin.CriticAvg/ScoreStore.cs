using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Jellyfin.Plugin.CriticAvg;

/// <summary>One film's last Critic AVG score.</summary>
public sealed class ScoreEntry
{
    /// <summary>Gets or sets the score out of 10.</summary>
    public double Score { get; set; }

    /// <summary>Gets or sets when it was fetched (UTC).</summary>
    public DateTime Updated { get; set; }

    /// <summary>Gets or sets the preset it was computed with.</summary>
    public string Preset { get; set; } = "balanced";
}

/// <summary>
/// Remembers each film's score in a small JSON file in the plugin's data folder, so the server is only asked again
/// after <c>RefreshDays</c>, and the score can be re-applied for free if a metadata refresh overwrote it.
/// </summary>
public sealed class ScoreStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>Gets the scores, keyed by Jellyfin item id.</summary>
    public Dictionary<string, ScoreEntry> Entries { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Loads the store, or returns an empty one if the file is missing or unreadable.</summary>
    /// <param name="path">File path.</param>
    /// <returns>The store.</returns>
    public static ScoreStore Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var entries = JsonSerializer.Deserialize<Dictionary<string, ScoreEntry>>(File.ReadAllText(path), JsonOptions);
                if (entries is not null)
                {
                    return new ScoreStore { Entries = new Dictionary<string, ScoreEntry>(entries, StringComparer.Ordinal) };
                }
            }
        }
        catch (Exception)
        {
            // A damaged file just means every film is fetched again.
        }

        return new ScoreStore();
    }

    /// <summary>Saves the store (atomically, via a temporary file).</summary>
    /// <param name="path">File path.</param>
    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Entries, JsonOptions));
        File.Move(tmp, path, true);
    }
}
