using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.CriticAvg.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.CriticAvg;

/// <summary>
/// Critic AVG for Jellyfin: writes the Critic AVG score (one weighted average of IMDb, TMDB, Trakt,
/// Letterboxd, MUBI, Rotten Tomatoes, Metacritic and SensaCine) into each film's rating.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>The plugin's permanent ID. Never change it, or Jellyfin treats it as a different plugin.</summary>
    public static readonly Guid PluginId = Guid.Parse("96c4c7d6-624e-40a0-86af-108a3c395608");

    /// <summary>Initializes a new instance of the <see cref="Plugin"/> class.</summary>
    /// <param name="applicationPaths">Jellyfin's application paths.</param>
    /// <param name="xmlSerializer">Jellyfin's XML serializer (stores the settings).</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>Gets the running plugin instance.</summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Critic AVG";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <inheritdoc />
    public override string Description =>
        "Dozens of critics. One clear score. Writes the Critic AVG score into each film's rating so you can sort and filter by it.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        };
    }
}
