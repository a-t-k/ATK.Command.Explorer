using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Controller;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Authorization;

/// <summary>
/// Setzt die Basisroute des Explorers aus der Konfiguration.
/// </summary>
/// <remarks>
/// <para>
/// Attribute können keine Werte aus der Konfiguration aufnehmen. Deshalb überschreibt
/// diese Konvention zur Laufzeit die Route des <see cref="CommandExplorerController"/>
/// mit <see cref="CommandExplorerOptions.RoutePrefix"/>.
/// </para>
/// <para>
/// Der <c>[Route]</c>-Attributwert bleibt als Standard erhalten: Ist die Option leer,
/// wird die Route nicht verändert.
/// </para>
/// </remarks>
public sealed class CommandExplorerRouteConvention : IControllerModelConvention
{
    private readonly IOptionsMonitor<CommandExplorerOptions> options;

    /// <summary>
    /// Erzeugt die Konvention.
    /// </summary>
    /// <param name="options">Die Explorer-Optionen.</param>
    public CommandExplorerRouteConvention(IOptionsMonitor<CommandExplorerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
    }

    /// <inheritdoc />
    public void Apply(ControllerModel controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        if (controller.ControllerType != typeof(CommandExplorerController))
        {
            return;
        }

        var prefix = this.options.CurrentValue.RoutePrefix?.Trim().Trim('/');

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return;
        }

        // Die Route steht als Attribut am Controller. Sie wird ersetzt, nicht ergänzt –
        // sonst entstünden zwei konkurrierende Routen.
        foreach (var selector in controller.Selectors)
        {
            selector.AttributeRouteModel = new AttributeRouteModel
            {
                Template = prefix,
            };
        }
    }
}
