using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Recording;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer;

/// <summary>
/// Registrierung der Aufnahme-Middleware.
/// </summary>
public static class CommandRecordingApplicationBuilderExtensions
{
    /// <summary>
    /// Schaltet den Mitschnitt von Command-Requests frei.
    /// </summary>
    /// <param name="app">Die Anwendung.</param>
    /// <returns>Die Anwendung, damit sich Aufrufe verketten lassen.</returns>
    /// <remarks>
    /// <para>
    /// Die Middleware wird nur eingehängt, wenn <see cref="CommandExplorerOptions.EnableRecording"/>
    /// gesetzt ist. Ohne die Option entsteht kein zusätzlicher Aufwand pro Request.
    /// </para>
    /// <para>
    /// Muss <b>vor</b> <c>MapControllers</c> aufgerufen werden, damit der Mitschnitt den
    /// ModelBinder und die Command-Ausführung umschließt.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// app.UseCommandRecording();
    /// app.MapControllers();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseCommandRecording(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.ApplicationServices
            .GetRequiredService<IOptions<CommandExplorerOptions>>()
            .Value;

        if (!options.EnableRecording)
        {
            return app;
        }

        return app.UseMiddleware<CommandRecordingMiddleware>();
    }
}
