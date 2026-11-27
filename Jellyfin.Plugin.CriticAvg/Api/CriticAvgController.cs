using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
 
namespace Jellyfin.Plugin.CriticAvg.Api;
 
/// <summary>
/// The plugin's small web API, used by the script that shows the Critic AVG badge on the film page.
/// GET /CriticAvg/Items/{itemId}  \u2192 the film's score and its own (original) ratings.
/// GET /CriticAvg/ClientScript    \u2192 the script itself.
/// </summary>
[ApiController]
[Route("CriticAvg")]
public class CriticAvgController : ControllerBase
{
    private static readonly object CacheLock = new();
    private static ScoreStore? _cache;
    private static DateTime _cacheStamp;
 
    private readonly ILibraryManager _libraryManager;
 
    /// <summary>Initializes a new instance of the <see cref="CriticAvgController"/> class.</summary>
    /// <param name="libraryManager">Jellyfin library.</param>
    public CriticAvgController(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }
 
    /// <summary>The script that adds the Critic AVG badge to the film page.</summary>
    /// <returns>JavaScript.</returns>
    [HttpGet("ClientScript")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    public ActionResult GetClientScript()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(typeof(Plugin).Namespace + ".Web.criticavg.js");
        if (stream is null)
        {
            return NotFound();
        }
 
        using var reader = new StreamReader(stream);
        Response.Headers["Cache-Control"] = "no-cache";
        return Content(reader.ReadToEnd(), "application/javascript; charset=utf-8");
    }
 
    /// <summary>One film's Critic AVG score, the display settings, and the film's own ratings.</summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <returns>The score, or 404 when the film has none yet.</returns>
    [HttpGet("Items/{itemId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetItemScore([FromRoute] Guid itemId)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return NotFound();
        }
 
        var config = plugin.Configuration;
        var key = itemId.ToString("N", CultureInfo.InvariantCulture);
        var store = LoadStore(Path.Combine(plugin.DataFolderPath, "scores.json"));
        if (!store.Entries.TryGetValue(key, out var entry))
        {
            return NotFound(new { displayMode = config.DisplayMode });
        }
 
        var item = _libraryManager.GetItemById(itemId);
        var community = item?.CommunityRating;
        var critic = item?.CriticRating;
 
        // Does the field show Critic AVG right now? Then the page should show the film's own value instead.
        var communityHoldsScore = entry.WrittenCommunityRating is not null && UpdateScoresTask.Same(community, entry.WrittenCommunityRating);
        var criticHoldsScore = entry.WrittenCriticRating is not null && UpdateScoresTask.Same(critic, entry.WrittenCriticRating);
 
        return Ok(new
        {
            score = Math.Round(entry.Score, 1),
            preset = entry.Preset,
            updated = entry.Updated,
            displayMode = config.DisplayMode,
            communityRating = new { holdsScore = communityHoldsScore, original = entry.OriginalCommunityRating },
            criticRating = new { holdsScore = criticHoldsScore, original = entry.OriginalCriticRating }
        });
    }
 
    /// <summary>Reads scores.json, again only when the nightly task has rewritten it.</summary>
    private static ScoreStore LoadStore(string path)
    {
        lock (CacheLock)
        {
            var stamp = System.IO.File.Exists(path) ? System.IO.File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (_cache is null || stamp != _cacheStamp)
            {
                _cache = ScoreStore.Load(path);
                _cacheStamp = stamp;
            }
 
            return _cache;
        }
    }
}
 
