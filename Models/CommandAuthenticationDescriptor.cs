namespace ATK.Command.Explorer.Models;

/// <summary>
/// Eine Authentifizierung, wie sie die UI im Dropdown anzeigt.
/// </summary>
/// <remarks>
/// Bewusst ohne Token: Der Token bleibt im Backend. Die UI wählt nur die <see cref="Id"/>
/// aus und meldet sie beim Ausführen eines Commands.
/// </remarks>
public sealed class CommandAuthenticationDescriptor
{
    /// <summary>Eindeutige Kennung, die beim Ausführen mitgesendet wird.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Anzeigename im Dropdown.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Art der Authentifizierung (z. B. <c>Bearer</c>).</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Name des Headers, der gesetzt wird.</summary>
    public string HeaderName { get; init; } = string.Empty;

    /// <summary>
    /// Gibt an, ob der Token zur Laufzeit beschafft wird.
    /// </summary>
    /// <remarks>
    /// Hilfreich für die UI, um „statisch“ von „dynamisch“ zu unterscheiden.
    /// </remarks>
    public bool IsDynamic { get; init; }
}
