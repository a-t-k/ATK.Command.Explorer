using ATK.Command.AspNet.ModelBinder;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ATK.Command.Explorer.Binding;

/// <summary>
/// Authentifizierung für den ModelBinder des Explorers: lässt jedes Command durch.
/// </summary>
/// <remarks>
/// <para>
/// Der <c>RequestCommandModelBinder</c> aus <c>ATK.Command.AspNet.ModelBinder</c> führt nur
/// dann eine Bindung durch, wenn mindestens eine <see cref="ICommandAuthentication"/>
/// registriert ist und alle zustimmen. Für den Explorer ist das nicht gewünscht: Er ist ein
/// eigenständiges Werkzeug und schützt seinen Endpunkt über die
/// <c>CommandExplorerAuthorizationConvention</c>, nicht über Attribute an den Commands.
/// </para>
/// <para>
/// Diese Implementierung ist deshalb die minimale Zustimmung, die der Binder verlangt.
/// Sie prüft bewusst nichts – die Autorisierung des Explorer-Endpunkts passiert vorher.
/// </para>
/// </remarks>
internal sealed class ExplorerCommandAuthentication : ICommandAuthentication
{
    /// <inheritdoc />
    public bool Execute(ModelBindingContext bindingContext, object model) => true;
}
