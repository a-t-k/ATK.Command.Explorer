using ATK.Command.Explorer.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Ui;

/// <summary>
/// Liefert die mitgelieferte Explorer-UI aus.
/// </summary>
/// <remarks>
/// <para>
/// Die UI liegt als eingebettete Ressource im Paket. Damit braucht die Anwendung weder einen
/// zweiten Webserver noch eine CORS-Konfiguration: UI und API haben dieselbe Origin.
/// </para>
/// <para>
/// Der Pfad ist bewusst nicht <c>commands/explorer</c>, weil dort
/// <c>GET /commands/explorer/{name}</c> liegt und den UI-Pfad sonst als Command-Namen
/// interpretieren würde.
/// </para>
/// </remarks>
public static class CommandExplorerUiExtensions
{
    /// <summary>
    /// Präfix der eingebetteten Ressourcen im Paket.
    /// </summary>
    /// <remarks>
    /// Ohne abschließenden Punkt: <see cref="EmbeddedFileProvider"/> hängt selbst einen an.
    /// </remarks>
    internal const string ResourcePrefix = "ATK.Command.Explorer.Ui.wwwroot";

    /// <summary>
    /// Registriert die Auslieferung der Explorer-UI.
    /// </summary>
    /// <param name="app">Die Anwendung.</param>
    /// <returns>Die Anwendung, damit sich Aufrufe verketten lassen.</returns>
    /// <remarks>
    /// Muss <b>vor</b> <c>MapControllers</c> aufgerufen werden. Ohne
    /// <c>CommandExplorer:ServeUi = true</c> passiert nichts.
    /// </remarks>
    /// <example>
    /// <code>
    /// app.UseCommandExplorerUi();
    /// app.MapControllers();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseCommandExplorerUi(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        CommandExplorerOptions explorer = app.ApplicationServices
            .GetRequiredService<IOptions<CommandExplorerOptions>>()
            .Value;

        if (!explorer.ServeUi)
        {
            return app;
        }

        EmbeddedFileProvider provider = new(
            typeof(CommandExplorerUiExtensions).Assembly,
            ResourcePrefix);

        string basePath = "/" + explorer.UiPath.Trim('/');

        // index.html wird selbst ausgeliefert, weil das <base href> auf den konfigurierten
        // Pfad umgeschrieben werden muss. Angular baut die Datei mit href="/", was bei
        // Auslieferung unter einem Unterpfad alle Bundles von der Wurzel laden würde.
        // Diese Middleware läuft deshalb VOR UseStaticFiles.
        app.Use(async (context, next) =>
        {
            if (!HttpMethods.IsGet(context.Request.Method)
                && !HttpMethods.IsHead(context.Request.Method))
            {
                await next(context);
                return;
            }

            if (!context.Request.Path.StartsWithSegments(
                    basePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            string remaining = context.Request.Path.Value![basePath.Length..].Trim('/');

            // Nur der Einstieg und Client-Routen ohne Dateiendung liefern index.html.
            // Eine fehlende .js-Datei soll weiterhin 404 ergeben und nicht als HTML gelten.
            bool isEntryPoint = remaining.Length == 0
                || string.Equals(remaining, "index.html", StringComparison.OrdinalIgnoreCase);

            if (!isEntryPoint && Path.HasExtension(remaining))
            {
                await next(context);
                return;
            }

            IFileInfo file = provider.GetFileInfo("index.html");

            if (!file.Exists)
            {
                await next(context);
                return;
            }

            string html;

            await using (Stream read = file.CreateReadStream())
            using (StreamReader reader = new(read))
            {
                html = await reader.ReadToEndAsync(context.RequestAborted);
            }

            html = html.Replace(
                "<base href=\"/\">",
                $"<base href=\"{basePath}/\">",
                StringComparison.Ordinal);

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";

            await context.Response.WriteAsync(html, context.RequestAborted);
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = provider,
            RequestPath = basePath,
            ContentTypeProvider = new FileExtensionContentTypeProvider(),
        });

        return app;
    }
}
