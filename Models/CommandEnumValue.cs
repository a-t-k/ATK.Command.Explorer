namespace ATK.Command.Explorer.Models;

/// <summary>
/// Ein erlaubter Wert einer Enumeration.
/// </summary>
public sealed class CommandEnumValue
{
    /// <summary>Name des Enum-Mitglieds.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Numerischer Wert des Enum-Mitglieds.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>XML-Summary des Enum-Mitglieds, falls dokumentiert.</summary>
    public string? Summary { get; init; }
}
