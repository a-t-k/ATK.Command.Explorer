using ATK.Command.AspNet.ModelBinder;
using ATK.Command.Communication.Models;
using ATK.Command.Explorer.Binding;
using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Controller;

/// <summary>
/// Einziger Endpunkt des Command Explorers.
/// </summary>
/// <remarks>
/// <para>
/// Wie die WebApi der Anwendung hat der Explorer genau eine Action: <c>POST</c> auf
/// <c>commands/explorer</c>. Welches Command ausgeführt wird, steht im JSON-Feld
/// <c>$type</c>. Die UI nutzt dafür denselben <c>CommandProcessor</c> wie für die
/// Anwendungs-Commands.
/// </para>
/// <para>
/// Die Authentifizierung für die Command-Ausführung wird über den Header
/// <c>X-Explorer-Auth</c> gewählt. Der Token bleibt im Backend.
/// </para>
/// </remarks>
[ApiController]
[Route("commands/explorer")]
public sealed class CommandExplorerController : ControllerBase
{
    private readonly ExplorerCommandDispatcher dispatcher;
    private readonly CommandAuthenticationResolver authenticationResolver;
    private readonly IOptions<CommandExplorerOptions> options;

    /// <summary>
    /// Erzeugt den Controller.
    /// </summary>
    /// <param name="dispatcher">Führt die Explorer-Commands aus.</param>
    /// <param name="authenticationResolver">Löst die gewählte Authentifizierung auf.</param>
    /// <param name="options">Die Explorer-Optionen.</param>
    public CommandExplorerController(
        ExplorerCommandDispatcher dispatcher,
        CommandAuthenticationResolver authenticationResolver,
        IOptions<CommandExplorerOptions> options)
    {
        this.dispatcher = dispatcher;
        this.authenticationResolver = authenticationResolver;
        this.options = options;
    }

    /// <summary>
    /// Führt das im Body beschriebene Explorer-Command aus.
    /// </summary>
    /// <param name="command">Das anhand von <c>$type</c> gebundene Command.</param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>Eine <see cref="ApiResponse{T}"/> mit dem Ergebnis des Commands.</returns>
    /// <remarks>
    /// <para>
    /// Jede Antwort – Erfolg wie Fehler – kommt in einer <see cref="ApiResponse{T}"/> aus
    /// <c>ATK.Command.Communication.Models</c>. Die UI wertet damit einheitlich
    /// <c>status</c>, <c>message</c> und <c>notifications</c> aus.
    /// </para>
    /// <para>
    /// Liefert das Command ein <see cref="FileModel"/>, enthält <c>data</c> den Dateiinhalt
    /// Base64-kodiert. Die UI erkennt das und speichert die Datei.
    /// </para>
    /// <para>
    /// Die gewählte Authentifizierung wird über den Header
    /// <see cref="CommandExplorerOptions.AuthenticationHeaderName"/> übergeben. Sie wird
    /// aufgelöst und für die Dauer der Anfrage bereitgestellt – Commands, die selbst
    /// HTTP-Aufrufe machen, können sie über <see cref="CommandAuthenticationContext"/>
    /// abrufen.
    /// </para>
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult> Execute(
        [ModelBinder] IExplorerCommand command,
        CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid)
        {
            // Bindungsfehler (fehlendes $type, ungültiges JSON) als Hinweise in der Hülle.
            return this.BadRequest(ApiResponseFactory.Failure(
                "fail",
                "Das Command konnte nicht gelesen werden.",
                ApiResponseFactory.FromModelState(this.ModelState)));
        }

        var authenticationId = this.Request.Headers[
            this.options.Value.AuthenticationHeaderName].FirstOrDefault();

        var authHeaders =
            await this.authenticationResolver.ResolveHeadersAsync(authenticationId, cancellationToken);

        CommandAuthenticationContext context = new(authenticationId, authHeaders);

        using var scope = CommandAuthenticationContext.Begin(context);

        var result = await this.dispatcher.ExecuteAsync(command, cancellationToken);

        return this.Ok(ApiResponseFactory.Success(result));
    }
}