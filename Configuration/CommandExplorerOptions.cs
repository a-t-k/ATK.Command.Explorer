namespace ATK.Command.Explorer.Configuration;

/// <summary>
/// Konfiguration des Command Explorers.
/// </summary>
/// <remarks>
/// Die Optionen werden aus dem Abschnitt <c>CommandExplorer</c> der <c>appsettings.json</c>
/// gelesen.
/// </remarks>
public sealed class CommandExplorerOptions
{
    /// <summary>Name des Konfigurationsabschnitts.</summary>
    public const string SectionName = "CommandExplorer";

    /// <summary>
    /// Namespace-Prefixe, die im Command-Baum nicht angezeigt werden.
    /// </summary>
    /// <remarks>
    /// Lange Firmen- und Produkt-Namespaces wie <c>ATK.Command.Ui.WebApi</c> würden den Baum
    /// unnötig tief machen. Jeder hier eingetragene Prefix wird von den Namespaces der
    /// Commands abgeschnitten, sodass nur der fachlich relevante Rest sichtbar bleibt.
    /// Der längste passende Prefix gewinnt.
    /// </remarks>
    public IList<string> NamespacePrefixes { get; set; } = [];

    /// <summary>
    /// Basisroute, unter der die Explorer-Endpunkte erreichbar sind.
    /// </summary>
    /// <remarks>
    /// Standard ist <c>commands/explorer</c>. Der Wert wird ohne führenden oder
    /// abschließenden Schrägstrich erwartet.
    /// </remarks>
    public string RoutePrefix { get; set; } = "commands/explorer";

    /// <summary>
    /// Gibt an, ob die Endpunkte anonym erreichbar sind.
    /// </summary>
    /// <remarks>
    /// In Produktivumgebungen sollte der Explorer abgeschaltet oder geschützt werden, weil er
    /// die komplette Command-Oberfläche offenlegt. Ist der Wert <c>false</c>, greifen die
    /// übrigen Autorisierungsangaben dieser Klasse: <see cref="Roles"/>, <see cref="Policies"/>,
    /// <see cref="Claims"/> und <see cref="AuthenticationSchemes"/>.
    /// </remarks>
    public bool AllowAnonymous { get; set; } = true;

    /// <summary>
    /// Rollen, von denen mindestens eine erforderlich ist, um den Explorer zu öffnen.
    /// </summary>
    /// <remarks>
    /// Entspricht <c>[Authorize(Roles = "...")]</c>: Die Rollen werden mit ODER verknüpft,
    /// der Aufrufer braucht also nur eine davon. Wirkt nur, wenn
    /// <see cref="AllowAnonymous"/> <c>false</c> ist.
    /// </remarks>
    public IList<string> Roles { get; set; } = [];

    /// <summary>
    /// Namen registrierter Autorisierungs-Policies, die alle erfüllt sein müssen.
    /// </summary>
    /// <remarks>
    /// Entspricht <c>[Authorize(Policy = "...")]</c>. Die Policies werden mit UND verknüpft und
    /// müssen in der Anwendung über <c>AddAuthorization</c> registriert sein. Wirkt nur, wenn
    /// <see cref="AllowAnonymous"/> <c>false</c> ist.
    /// </remarks>
    public IList<string> Policies { get; set; } = [];

    /// <summary>
    /// Claims, die der Aufrufer besitzen muss, um den Explorer zu öffnen.
    /// </summary>
    /// <remarks>
    /// Alle Einträge müssen erfüllt sein (UND-Verknüpfung). Innerhalb eines Eintrags genügt
    /// einer der angegebenen Werte (ODER-Verknüpfung); eine leere Werteliste verlangt nur das
    /// Vorhandensein des Claims. Wirkt nur, wenn <see cref="AllowAnonymous"/> <c>false</c> ist.
    /// </remarks>
    public IList<CommandExplorerClaim> Claims { get; set; } = [];

    /// <summary>
    /// Authentifizierungsschemata, gegen die geprüft wird.
    /// </summary>
    /// <remarks>
    /// Bleibt die Liste leer, verwendet ASP.NET Core das Standard-Schema. Wirkt nur, wenn
    /// <see cref="AllowAnonymous"/> <c>false</c> ist.
    /// </remarks>
    public IList<string> AuthenticationSchemes { get; set; } = [];

    /// <summary>
    /// Gibt an, ob die mitgelieferte Explorer-UI ausgeliefert wird.
    /// </summary>
    /// <remarks>
    /// Die UI steckt als eingebettete Ressource im Paket. Ist der Wert <c>true</c>, liefert die
    /// Anwendung sie unter <see cref="UiPath"/> aus – ohne eigenen Webserver und ohne
    /// CORS-Konfiguration, weil UI und API dieselbe Origin haben.
    /// </remarks>
    public bool ServeUi { get; set; }

    /// <summary>
    /// Pfad, unter dem die Explorer-UI ausgeliefert wird.
    /// </summary>
    /// <remarks>
    /// Standard ist <c>commands/explorer-ui</c>. Der Wert wird ohne führenden oder
    /// abschließenden Schrägstrich erwartet.
    /// <para>
    /// Bewusst <b>nicht</b> unter <see cref="RoutePrefix"/>: dort liegt
    /// <c>GET /commands/explorer/{name}</c>, das sonst den Pfad der UI als Command-Namen
    /// interpretieren würde.
    /// </para>
    /// </remarks>
    public string UiPath { get; set; } = "commands/explorer-ui";

    /// <summary>
    /// Titel der erzeugten Dokumentation.
    /// </summary>
    /// <remarks>
    /// Erscheint als Überschrift im PDF, als Titel der Startseite im Markdown-Vault und im
    /// Dateinamen der Downloads. Standard ist <c>Command-Referenz</c>.
    /// </remarks>
    public string DocumentationTitle { get; set; } = "Command-Referenz";

    /// <summary>
    /// Ordner mit <c>.ttf</c>-Dateien für die PDF-Erzeugung.
    /// </summary>
    /// <remarks>
    /// Bleibt der Wert leer, durchsucht der <c>CommandPdfFontResolver</c> die üblichen
    /// Systempfade (Windows, Linux, macOS). Auf einem Container ohne Schriften sollte hier
    /// ein Ordner mit DejaVu- oder Liberation-Fonts angegeben werden.
    /// </remarks>
    public string? FontDirectory { get; set; }

    /// <summary>
    /// Authentifizierungen, die die UI beim Ausführen von Commands zur Auswahl anbietet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Liste kann vollständig aus der Konfiguration kommen (Abschnitt
    /// <c>CommandExplorer:Authentications</c>) oder im Code ergänzt werden. Beide Wege lassen
    /// sich mischen: Konfigurationseinträge werden zuerst geladen, im Code hinzugefügte
    /// Einträge kommen hinzu.
    /// </para>
    /// <para>
    /// Die UI zeigt die Einträge als Dropdown. Der gewählte Eintrag wird bei jeder
    /// Command-Ausführung über den Header <c>X-Explorer-Auth</c> an das Backend gemeldet;
    /// das Backend setzt daraufhin den passenden Authentifizierungs-Header. Der Token selbst
    /// verlässt den Server nie.
    /// </para>
    /// <para>
    /// Ist die Liste leer, zeigt die UI kein Dropdown und es wird kein Auth-Header gesetzt.
    /// </para>
    /// </remarks>
    public IList<CommandExplorerAuthentication> Authentications { get; set; } = [];

    /// <summary>
    /// Name des Headers, über den die UI die gewählte Authentifizierung meldet.
    /// </summary>
    /// <remarks>
    /// Standard ist <c>X-Explorer-Auth</c>. Der Wert ist die <c>Id</c> eines Eintrags aus
    /// <see cref="Authentications"/>.
    /// </remarks>
    public string AuthenticationHeaderName { get; set; } = "X-Explorer-Auth";

    /// <summary>
    /// Gibt an, ob die UI das Dropdown für Authentifizierungen anzeigt.
    /// </summary>
    /// <remarks>
    /// Wird automatisch abgeleitet: <c>true</c>, sobald <see cref="Authentications"/> Einträge
    /// enthält. Kann explizit auf <c>false</c> gesetzt werden, um das Dropdown zu verbergen,
    /// ohne die Einträge zu entfernen.
    /// </remarks>
    public bool ShowAuthenticationSelector { get; set; } = true;

    /// <summary>
    /// Relativer Pfad des Command-Endpunkts der Anwendung.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Explorer leitet Command-Ausführungen serverseitig an diesen Pfad weiter, weil nur
    /// so die Authentifizierungs-Header gesetzt werden können, ohne den Token an den Browser
    /// auszuliefern. Standard ist <c>commands</c> – also derselbe Endpunkt, den die UI
    /// normalerweise direkt aufruft.
    /// </para>
    /// <para>
    /// Der Wert wird ohne führenden Schrägstrich erwartet.
    /// </para>
    /// </remarks>
    public string CommandEndpoint { get; set; } = "commands";

    /// <summary>
    /// Basis-URL der eigenen Anwendung für den Command-Proxy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bleibt der Wert leer, ermittelt der Explorer die Adresse aus der aktuellen Anfrage
    /// (Schema + Host). Das ist der Normalfall und funktioniert auch hinter einem
    /// Reverse-Proxy.
    /// </para>
    /// <para>
    /// Setzen ist nur nötig, wenn der Proxy außerhalb einer HTTP-Anfrage läuft oder die
    /// Anwendung unter einer anderen Adresse erreichbar ist, als der Explorer annimmt.
    /// Beispiel: <c>http://localhost:5100</c>.
    /// </para>
    /// </remarks>
    public string? CommandBaseUrl { get; set; }

    /// <summary>
    /// Name der Policy, die der Explorer für seine Endpunkte verwendet.
    /// </summary>
    /// <remarks>
    /// Die Policy wird zur Laufzeit aus diesen Optionen gebaut und per
    /// <c>AuthorizeFilter</c> an den Controller gehängt. Der Name dient nur der
    /// Nachvollziehbarkeit in Logs und Diagnose.
    /// </remarks>
    public const string PolicyName = "ATK.Command.Explorer";
}
