using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CriticAvg;

/// <summary>
/// Scheduled task (Dashboard → Scheduled Tasks → Critic AVG): fetches each film's Critic AVG score and writes it
/// into the rating field chosen in the plugin settings. Runs every night by default.
/// </summary>
public class UpdateScoresTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpdateScoresTask> _logger;

    /// <summary>Initializes a new instance of the <see cref="UpdateScoresTask"/> class.</summary>
    /// <param name="libraryManager">Jellyfin library.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public UpdateScoresTask(ILibraryManager libraryManager, IHttpClientFactory httpClientFactory, ILogger<UpdateScoresTask> logger)
    {
        _libraryManager = libraryManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Update Critic AVG scores";

    /// <inheritdoc />
    public string Key => "CriticAvgUpdateScores";

    /// <inheritdoc />
    public string Description => "Fetches the Critic AVG score for each film and writes it into its rating.";

    /// <inheritdoc />
    public string Category => "Critic AVG";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Every night at 04:00.
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(4).Ticks
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("Critic AVG plugin is not loaded.");
        var config = plugin.Configuration;
        if (string.IsNullOrWhiteSpace(config.ServerUrl))
        {
            _logger.LogWarning("Critic AVG: no server address set. Open Dashboard → Plugins → Critic AVG and enter it.");
            return;
        }

        var storePath = Path.Combine(plugin.DataFolderPath, "scores.json");
        var store = ScoreStore.Load(storePath);

        var films = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie },
            IsVirtualItem = false,
            Recursive = true
        });

        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(90);

        int fetched = 0, written = 0, failed = 0, done = 0;
        var refreshDays = Math.Max(1, config.RefreshDays);
        var maxPerRun = Math.Max(1, config.MaxPerRun);

        try
        {
            foreach (var film in films)
            {
                cancellationToken.ThrowIfCancellationRequested();
                done++;
                progress.Report(100.0 * done / Math.Max(1, films.Count));

                var key = film.Id.ToString("N", CultureInfo.InvariantCulture);
                store.Entries.TryGetValue(key, out var entry);

                var due = entry is null
                    || (DateTime.UtcNow - entry.Updated).TotalDays >= refreshDays
                    || !string.Equals(entry.Preset, config.Preset, StringComparison.Ordinal);

                if (due && fetched < maxPerRun)
                {
                    var tmdb = film.GetProviderId(MetadataProvider.Tmdb);
                    var imdb = film.GetProviderId(MetadataProvider.Imdb);
                    if (!string.IsNullOrEmpty(tmdb) || !string.IsNullOrEmpty(imdb))
                    {
                        fetched++;
                        try
                        {
                            var result = await CriticAvgClient.GetScoreAsync(http, config.ServerUrl, tmdb, imdb, config.Preset, cancellationToken).ConfigureAwait(false);
                            if (result?.Score is double score)
                            {
                                entry = new ScoreEntry { Score = score, Updated = DateTime.UtcNow, Preset = config.Preset };
                                store.Entries[key] = entry;
                            }
                            else if (!string.IsNullOrEmpty(result?.Error))
                            {
                                _logger.LogInformation("Critic AVG: no score for {Name}: {Error}", film.Name, result!.Error);
                            }
                        }
                        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested) || ex is System.Text.Json.JsonException)
                        {
                            failed++;
                            _logger.LogWarning("Critic AVG: could not fetch {Name}: {Message}", film.Name, ex.Message);
                        }

                        if (fetched % 25 == 0)
                        {
                            store.Save(storePath);
                        }
                    }
                }

                // Write (or re-write, if a metadata refresh replaced it) the stored score.
                if (entry is not null && Apply(film, entry.Score, config.TargetField))
                {
                    await film.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                    written++;
                }
            }
        }
        finally
        {
            store.Save(storePath);
            _logger.LogInformation(
                "Critic AVG: {Films} films checked, {Fetched} scores fetched, {Written} films updated, {Failed} failed.",
                done,
                fetched,
                written,
                failed);
        }

        progress.Report(100);
    }

    /// <summary>Puts the score into the chosen field(s). Returns true when something changed.</summary>
    private static bool Apply(BaseItem film, double score, string targetField)
    {
        var changed = false;
        var outOf10 = (float)Math.Round(score, 1);
        var outOf100 = (float)Math.Round(score * 10);

        if ((targetField is "CommunityRating" or "Both") && film.CommunityRating != outOf10)
        {
            film.CommunityRating = outOf10;
            changed = true;
        }

        if ((targetField is "CriticRating" or "Both") && film.CriticRating != outOf100)
        {
            film.CriticRating = outOf100;
            changed = true;
        }

        return changed;
    }
}
