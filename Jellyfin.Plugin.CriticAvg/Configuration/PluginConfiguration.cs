using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CriticAvg.Configuration;

/// <summary>Settings shown on the plugin's page in the Jellyfin dashboard.</summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the Critic AVG server address, e.g. https://criticavg.yourname.workers.dev.</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the weights preset: balanced, equal, critics, audience, cinephile or spain.</summary>
    public string Preset { get; set; } = "balanced";

    /// <summary>Gets or sets where the score goes: CommunityRating (star, 0–10), CriticRating (0–100) or Both.</summary>
    public string TargetField { get; set; } = "CommunityRating";

    /// <summary>Gets or sets how many days a film's score is kept before it is fetched again.</summary>
    public int RefreshDays { get; set; } = 7;

    /// <summary>Gets or sets the most scores fetched in one run (keeps the server within its free daily limits).</summary>
    public int MaxPerRun { get; set; } = 300;
}
