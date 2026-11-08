namespace ATK.Command.Explorer.Models;

/// <summary>
/// Grobe Kategorie einer Command-Property. Steuert, welches Eingabefeld das Frontend rendert.
/// </summary>
public enum CommandPropertyKind
{
    /// <summary>Unbekannter oder nicht unterstützter Typ.</summary>
    Unknown = 0,

    /// <summary>Zeichenkette.</summary>
    String = 1,

    /// <summary>Ganzzahl.</summary>
    Integer = 2,

    /// <summary>Gleitkommazahl.</summary>
    Number = 3,

    /// <summary>Wahrheitswert.</summary>
    Boolean = 4,

    /// <summary>Datum ohne Uhrzeit.</summary>
    Date = 5,

    /// <summary>Datum mit Uhrzeit.</summary>
    DateTime = 6,

    /// <summary>Zeitspanne.</summary>
    TimeSpan = 7,

    /// <summary>Eindeutige ID.</summary>
    Guid = 8,

    /// <summary>Enumeration.</summary>
    Enum = 9,

    /// <summary>Liste oder Array.</summary>
    Array = 10,

    /// <summary>Verschachteltes Objekt.</summary>
    Object = 11,

    /// <summary>Wörterbuch.</summary>
    Dictionary = 12,

    /// <summary>Binärdaten als Base64.</summary>
    Binary = 13
}
