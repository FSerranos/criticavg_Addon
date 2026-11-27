using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CriticAvg.Web;

/// <summary>Registers the injector with Jellyfin when the plugin loads.</summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddTransient<IStartupFilter, ClientScriptStartupFilter>();
    }
}

/// <summary>
/// Adds one line to Jellyfin's web page, in memory (no files on disk are changed):
/// &lt;script src="../CriticAvg/ClientScript" defer&gt;&lt;/script&gt;. That script shows the Critic AVG badge.
/// </summary>
public class ClientScriptStartupFilter : IStartupFilter
{
    private const string Marker = "CriticAvg/ClientScript";
    private const string Tag = "<script src=\"../CriticAvg/ClientScript\" defer></script>";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(InjectAsync);
            next(app);
        };
    }

    private static bool IsWebIndex(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        var path = request.Path.Value ?? string.Empty;
        return path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task InjectAsync(HttpContext context, Func<Task> next)
    {
        if (!IsWebIndex(context.Request))
        {
            await next().ConfigureAwait(false);
            return;
        }

        // Ask for the plain, full page (not compressed, not "304 not modified"), so it can be edited.
        context.Request.Headers.Remove("Accept-Encoding");
        context.Request.Headers.Remove("If-None-Match");
        context.Request.Headers.Remove("If-Modified-Since");
        context.Request.Headers.Remove("Range");

        var original = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next().ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = original;
        }

        buffer.Position = 0;
        var isHtml = context.Response.StatusCode == StatusCodes.Status200OK
            && (context.Response.ContentType ?? string.Empty).Contains("text/html", StringComparison.OrdinalIgnoreCase);

        if (isHtml)
        {
            var html = Encoding.UTF8.GetString(buffer.ToArray());
            if (!html.Contains(Marker, StringComparison.Ordinal))
            {
                var at = html.LastIndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                html = at >= 0 ? html.Insert(at, Tag) : html + Tag;
            }

            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentLength = bytes.Length;
            context.Response.Headers.Remove("ETag");
            context.Response.Headers.Remove("Last-Modified");
            context.Response.Headers["Cache-Control"] = "no-cache";
            await original.WriteAsync(bytes).ConfigureAwait(false);
            return;
        }

        await buffer.CopyToAsync(original).ConfigureAwait(false);
    }
}
