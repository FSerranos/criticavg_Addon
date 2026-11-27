using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CriticAvg.Configuration;

/// <summary>Settings shown on the plugin's page in the Jellyfin dashboard.</summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the Critic AVG server address, e.g. https://criticavg.yourname.workers.dev.</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the weights preset: balanced, equal, critics, audience, cinephile or spain.</summary>
    public string Preset { get; set; } = "balanced";

    /// <summary>
    /// Gets or sets which rating field holds the Critic AVG score, so libraries can be sorted by it:
    /// CommunityRating (star, 0–10), CriticRating (0–100), Both, or None (nothing is written, no sorting).
    /// </summary>
    public string TargetField { get; set; } = "CriticRating";

    /// <summary>
    /// Gets or sets how the film page shows the score (Jellyfin web and apps built on it):
    /// Beside = a Critic AVG badge next to the star and the tomato, which keep their original values;
    /// Alone = only the Critic AVG badge, the star and the tomato are hidden.
    /// </summary>
    public string DisplayMode { get; set; } = "Beside";

    /// <summary>Gets or sets how many days a film's score is kept before it is fetched again.</summary>
    public int RefreshDays { get; set; } = 7;

    /// <summary>Gets or sets the most scores fetched in one run (keeps the server within its free daily limits).</summary>
    public int MaxPerRun { get; set; } = 300;
}
