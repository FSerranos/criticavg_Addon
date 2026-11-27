using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Jellyfin.Plugin.CriticAvg;

/// <summary>One film's last Critic AVG score.</summary>
public sealed class ScoreEntry
{
    /// <summary>The format written by this version. Entries saved by version 1.0 read as 0.</summary>
    public const int CurrentFormat = 2;

    /// <summary>Gets or sets the format of this entry (0 = saved by version 1.0).</summary>
    public int Format { get; set; }

    /// <summary>Gets or sets the score out of 10.</summary>
    public double Score { get; set; }

    /// <summary>Gets or sets when it was fetched (UTC).</summary>
    public DateTime Updated { get; set; }

    /// <summary>Gets or sets the preset it was computed with.</summary>
    public string Preset { get; set; } = "balanced";

    /// <summary>Gets or sets the film's own community rating (normally TMDB's) before Critic AVG replaced it.</summary>
    public float? OriginalCommunityRating { get; set; }

    /// <summary>Gets or sets the film's own critic rating (normally Rotten Tomatoes') before Critic AVG replaced it.</summary>
    public float? OriginalCriticRating { get; set; }

    /// <summary>Gets or sets the value Critic AVG last wrote into the community rating (null = it holds no Critic AVG value).</summary>
    public float? WrittenCommunityRating { get; set; }

    /// <summary>Gets or sets the value Critic AVG last wrote into the critic rating (null = it holds no Critic AVG value).</summary>
    public float? WrittenCriticRating { get; set; }
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

    /// <summary>
    /// Version 1.0 did not record what it wrote. It wrote the score into the field(s) chosen in the settings,
    /// so assume that, and the next metadata refresh gives the films their own values back to remember.
    /// </summary>
    /// <param name="targetField">The current "where to write the score" setting.</param>
    public void Migrate(string targetField)
    {
        foreach (var entry in Entries.Values)
        {
            if (entry.Format >= ScoreEntry.CurrentFormat)
            {
                continue;
            }

            if (targetField is "CommunityRating" or "Both")
            {
                entry.WrittenCommunityRating = UpdateScoresTask.Ten(entry.Score);
            }

            if (targetField is "CriticRating" or "Both")
            {
                entry.WrittenCriticRating = UpdateScoresTask.Hundred(entry.Score);
            }

            entry.Format = ScoreEntry.CurrentFormat;
        }
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
