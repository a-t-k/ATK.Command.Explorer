using System.Text;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Documentation;
using ATK.Command.Explorer.Framework;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Controller;

/// <summary>
/// Stellt die Metadaten aller Commands für die interaktive Explorer-UI bereit.
/// </summary>
/// <remarks>
/// Dieses Gegenstück zu Swashbuckle liefert keine OpenAPI-Beschreibung, sondern die
/// Command-Metadaten, die für das dynamische Formular benötigt werden.
/// </remarks>
[ApiController]
[Route("commands/explorer")]
public sealed class CommandExplorerController : ControllerBase
{
    private readonly CommandMetadataProvider provider;
    private readonly MarkdownVaultGenerator markdownGenerator;
    private readonly CommandPdfGenerator pdfGenerator;
    private readonly TypeScriptExportGenerator typeScriptGenerator;
    private readonly FrameworkTemplateProvider frameworkProvider;
    private readonly CommandAuthenticationResolver authenticationResolver;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IOptions<CommandExplorerOptions> options;

    /// <summary>
    /// Erzeugt den Controller.
    /// </summary>
    /// <param name="provider">Provider, der die Command-Metadaten erzeugt.</param>
    /// <param name="markdownGenerator">Erzeugt den Markdown-Vault.</param>
    /// <param name="pdfGenerator">Erzeugt das PDF.</param>
    /// <param name="typeScriptGenerator">Erzeugt den TypeScript-Export.</param>
    /// <param name="frameworkProvider">Liefert die Framework-Vorlagen.</param>
    /// <param name="authenticationResolver">Löst die gewählte Authentifizierung auf.</param>
    /// <param name="httpClientFactory">Erzeugt den Client für den Command-Proxy.</param>
    /// <param name="options">Die Explorer-Optionen.</param>
    public CommandExplorerController(
        CommandMetadataProvider provider,
        MarkdownVaultGenerator markdownGenerator,
        CommandPdfGenerator pdfGenerator,
        TypeScriptExportGenerator typeScriptGenerator,
        FrameworkTemplateProvider frameworkProvider,
        CommandAuthenticationResolver authenticationResolver,
        IHttpClientFactory httpClientFactory,
        IOptions<CommandExplorerOptions> options)
    {
        this.provider = provider;
        this.markdownGenerator = markdownGenerator;
        this.pdfGenerator = pdfGenerator;
        this.typeScriptGenerator = typeScriptGenerator;
        this.frameworkProvider = frameworkProvider;
        this.authenticationResolver = authenticationResolver;
        this.httpClientFactory = httpClientFactory;
        this.options = options;
    }

    /// <summary>
    /// Liefert alle Commands der WebApi.
    /// </summary>
    /// <returns>Liste aller Commands inklusive Properties und Autorisierung.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CommandDescriptor>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<CommandDescriptor>> GetCommands() =>
        this.Ok(this.provider.GetCommands());

    /// <summary>
    /// Liefert alle Commands als Namespace-Hierarchie.
    /// </summary>
    /// <returns>Baum aus Namespace-Knoten mit den jeweils enthaltenen Commands.</returns>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(IReadOnlyList<NamespaceNode>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<NamespaceNode>> GetNamespaceTree() =>
        this.Ok(this.provider.GetNamespaceTree());

    /// <summary>
    /// Liefert einen einzelnen Command anhand seines Namens.
    /// </summary>
    /// <param name="name">Klassenname oder vollqualifizierter Name des Commands.</param>
    /// <returns>Die Beschreibung des Commands.</returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(CommandDescriptor), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CommandDescriptor> GetCommand(string name)
    {
        CommandDescriptor? command = this.provider.GetCommand(name);
        return command is null ? this.NotFound() : this.Ok(command);
    }

    /// <summary>
    /// Liefert die konfigurierten Authentifizierungen für das Dropdown der UI.
    /// </summary>
    /// <returns>Beschreibungen ohne Token.</returns>
    /// <remarks>
    /// Der Token bleibt im Backend. Die UI erhält nur <c>Id</c>, <c>Name</c>, <c>Type</c> und
    /// den Headernamen, damit sie die Auswahl anzeigen kann.
    /// </remarks>
    [HttpGet("authentications")]
    [ProducesResponseType(
        typeof(IReadOnlyList<CommandAuthenticationDescriptor>),
        StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<CommandAuthenticationDescriptor>> GetAuthentications() =>
        this.Ok(this.authenticationResolver.GetDescriptors());

    /// <summary>
    /// Führt ein Command im Auftrag der UI aus und setzt dabei die gewählte Authentifizierung.
    /// </summary>
    /// <param name="authenticationId">
    /// <c>Id</c> der gewählten Authentifizierung. <c>null</c> oder leer bedeutet „ohne“.
    /// </param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>
    /// Status, Content-Type und Body der Antwort des Command-Endpunkts – unverändert
    /// weitergereicht.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Warum ein Proxy und kein direkter Aufruf aus dem Browser: Der Token darf den Server
    /// nicht verlassen. Die UI kennt nur die <c>Id</c>; dieses Endpunkt holt den Token,
    /// setzt den passenden Header und leitet den Request an
    /// <see cref="CommandExplorerOptions.CommandEndpoint"/> weiter.
    /// </para>
    /// <para>
    /// Der Body wird unverändert durchgereicht, die Antwort ebenfalls – inklusive
    /// Fehlerstatus und <c>ProblemDetails</c>. Die UI wertet sie wie eine direkte Antwort aus.
    /// </para>
    /// </remarks>
    [HttpPost("execute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ExecuteCommand(
        [FromQuery] string? authenticationId,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> authHeaders =
            await this.authenticationResolver.ResolveHeadersAsync(authenticationId, cancellationToken);

        // Body vollständig einlesen, bevor der Request weitergeleitet wird.
        // StreamContent auf Request.Body reicht nicht: Der Stream ist nicht wiederholbar und
        // ohne Content-Length kann der Ziel-ModelBinder den Body nicht lesen (400).
        using MemoryStream buffer = new();
        await this.Request.Body.CopyToAsync(buffer, cancellationToken);
        byte[] payload = buffer.ToArray();

        using HttpRequestMessage request = new(
            HttpMethod.Post,
            this.BuildCommandUri());

        request.Content = new ByteArrayContent(payload);
        request.Content.Headers.ContentType =
            System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");

        foreach (KeyValuePair<string, string> header in authHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using HttpClient client = this.httpClientFactory.CreateClient(nameof(CommandExplorerController));

        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        // Antwort unverändert zurückgeben: Status, Content-Type und Body.
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        string contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = body,
            ContentType = contentType,
        };
    }

    /// <summary>
    /// Baut die absolute URI des Command-Endpunkts aus der aktuellen Anfrage.
    /// </summary>
    /// <remarks>
    /// Gleiche Origin, gleiches Schema – dadurch funktioniert der Proxy auch hinter einem
    /// Reverse-Proxy, ohne dass die Basis-URL konfiguriert werden muss.
    /// </remarks>
    private Uri BuildCommandUri()
    {
        HttpRequest current = this.Request;
        string path = "/" + this.options.Value.CommandEndpoint.Trim('/');

        return new Uri($"{current.Scheme}://{current.Host}{path}");
    }

    /// <summary>
    /// Liefert die Command-Dokumentation als Obsidian-Vault im ZIP-Archiv.
    /// </summary>
    /// <param name="scope">
    /// Optionaler Namespace oder Command-Name. Ohne Angabe werden alle Commands dokumentiert.
    /// </param>
    /// <returns>ZIP-Datei mit Markdown-Notizen, Wikilinks und <c>.obsidian</c>-Ordner.</returns>
    [HttpGet("documentation/vault")]
    [Produces("application/zip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetDocumentationVault([FromQuery] string? scope = null)
    {
        IReadOnlyList<CommandDescriptor>? commands = this.ResolveScope(scope);

        if (commands is null)
        {
            return this.NotFound();
        }

        string title = this.ResolveTitle(scope);

        IReadOnlyDictionary<string, string> files = this.markdownGenerator.Generate(commands, title);
        byte[] archive = VaultArchiveBuilder.Build(
            files,
            MarkdownVaultGenerator.SanitizeFileName(title));

        return this.File(archive, "application/zip", $"{Slug(title)}-vault.zip");
    }

    /// <summary>
    /// Liefert die Command-Dokumentation als PDF.
    /// </summary>
    /// <param name="scope">
    /// Optionaler Namespace oder Command-Name. Ohne Angabe werden alle Commands dokumentiert.
    /// </param>
    /// <returns>PDF-Datei mit Titelseite, Inhaltsverzeichnis und einem Abschnitt pro Command.</returns>
    [HttpGet("documentation/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetDocumentationPdf([FromQuery] string? scope = null)
    {
        IReadOnlyList<CommandDescriptor>? commands = this.ResolveScope(scope);

        if (commands is null)
        {
            return this.NotFound();
        }

        string title = this.ResolveTitle(scope);
        byte[] pdf = this.pdfGenerator.Generate(commands, title);

        return this.File(pdf, "application/pdf", $"{Slug(title)}.pdf");
    }

    /// <summary>
    /// Liefert die Command-Dokumentation als einzelne Markdown-Datei.
    /// </summary>
    /// <param name="scope">
    /// Optionaler Namespace oder Command-Name. Ohne Angabe werden alle Commands dokumentiert.
    /// </param>
    /// <returns>Eine Markdown-Datei mit allen Commands im gewählten Bereich.</returns>
    [HttpGet("documentation/markdown")]
    [Produces("text/markdown")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetDocumentationMarkdown([FromQuery] string? scope = null)
    {
        IReadOnlyList<CommandDescriptor>? commands = this.ResolveScope(scope);

        if (commands is null)
        {
            return this.NotFound();
        }

        string title = this.ResolveTitle(scope);

        IReadOnlyDictionary<string, string> files = this.markdownGenerator.Generate(commands, title);

        // Alle Notizen zu einem Dokument zusammenfassen – praktisch für Wikis ohne Obsidian.
        // Nur die Startseite behält ihr Frontmatter; bei den übrigen Notizen wäre ein
        // zweiter Frontmatter-Block mitten im Dokument wirkungslos und würde zusammen mit
        // dem Trenner als doppelte `---`-Linie erscheinen.
        string combined = string.Join(
            "\n\n---\n\n",
            files
                .Where(entry => entry.Key != "00 Start.md")
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => StripFrontMatter(entry.Value)));

        string document = files["00 Start.md"] + "\n\n---\n\n" + combined;

        return this.File(
            Encoding.UTF8.GetBytes(document),
            "text/markdown; charset=utf-8",
            $"{Slug(title)}.md");
    }

    /// <summary>
    /// Liefert das Framework-Gerüst für den TypeScript-Client im ZIP-Archiv.
    /// </summary>
    /// <param name="framework">Framework-Name, Standard <c>angular</c>.</param>
    /// <returns>
    /// ZIP-Datei mit <c>CommandProcessor</c>, <c>BaseHttp</c>, <c>AppConfig</c> und einer
    /// Markdown-Anleitung.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Getrennt vom TypeScript-Export: Dort kommen die generierten Commands, hier das
    /// Gerüst, das man einmal einbaut. Die Anleitung beschreibt Schritt für Schritt, was
    /// zu tun ist – inklusive der Provider-Registrierung, ohne die die Injektion von
    /// <c>CommandProcessor</c> scheitert.
    /// </para>
    /// <para>
    /// Die Vorlagen liegen als eingebettete Ressourcen im Paket. Neue Frameworks kommen
    /// als weiterer Ordner unter <c>Framework/Templates/</c> dazu.
    /// </para>
    /// </remarks>
    [HttpGet("framework")]
    [Produces("application/zip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetFramework([FromQuery] string framework = "angular")
    {
        IReadOnlyDictionary<string, string>? files = this.frameworkProvider.GetTemplates(framework);

        if (files is null)
        {
            return this.NotFound();
        }

        string title = $"{this.options.Value.DocumentationTitle} - {framework}";

        byte[] archive = ZipArchiveBuilder.Build(
            files,
            MarkdownVaultGenerator.SanitizeFileName(title));

        return this.File(archive, "application/zip", $"{Slug(title)}-framework.zip");
    }

    /// <summary>
    /// Liefert die Commands als TypeScript-Typen im ZIP-Archiv.
    /// </summary>
    /// <param name="scope">
    /// Optionaler Namespace oder Command-Name. Ohne Angabe werden alle Commands exportiert.
    /// </param>
    /// <returns>ZIP-Datei mit einer <c>.ts</c>-Datei pro Typ und einer <c>index.ts</c>.</returns>
    /// <remarks>
    /// <para>
    /// Bewusst <b>nicht</b> unter <c>documentation/</c>: Das hier ist keine Dokumentation,
    /// sondern lauffähiger Code für das Frontend.
    /// </para>
    /// <para>
    /// Der Export nutzt <c>ATK.Command.CsToTsGenerator</c> – dasselbe Paket, das auch das
    /// Projekt <c>ATK.Command.Ui.TypeConverterCsToTs</c> verwendet. Jeder Typ landet in einer
    /// eigenen Datei, abhängige Typen wie Enums und verschachtelte Klassen werden
    /// automatisch mitgenommen.
    /// </para>
    /// </remarks>
    [HttpGet("typescript")]
    [Produces("application/zip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetTypeScript([FromQuery] string? scope = null)
    {
        IReadOnlyList<Type>? types = this.provider.GetCommandTypes(scope);

        if (types is null)
        {
            return this.NotFound();
        }

        IReadOnlyDictionary<string, string> files = this.typeScriptGenerator.Generate(types);
        string title = this.ResolveTitle(scope);

        byte[] archive = ZipArchiveBuilder.Build(
            files,
            MarkdownVaultGenerator.SanitizeFileName(title));

        return this.File(archive, "application/zip", $"{Slug(title)}-typescript.zip");
    }

    /// <summary>
    /// Ermittelt die Commands für einen Dokumentationsbereich.
    /// </summary>
    /// <param name="scope">
    /// Namespace oder Command-Name. <c>null</c> oder leer bedeutet „alle Commands“.
    /// </param>
    /// <returns>
    /// Die Commands im Bereich oder <c>null</c>, wenn der Bereich unbekannt ist.
    /// </returns>
    /// <remarks>
    /// Ein Namespace umfasst immer den ganzen Teilbaum – der Knoten selbst und alle
    /// Kind-Namespaces. Ein Command-Name liefert genau diesen einen Command.
    /// </remarks>
    private IReadOnlyList<CommandDescriptor>? ResolveScope(string? scope)
    {
        IReadOnlyList<CommandDescriptor> all = this.provider.GetCommands();

        if (string.IsNullOrWhiteSpace(scope))
        {
            return all;
        }

        string value = scope.Trim();

        CommandDescriptor? single = all.FirstOrDefault(
            command => string.Equals(command.FullName, value, StringComparison.Ordinal)
                       || string.Equals(command.Name, value, StringComparison.Ordinal));

        if (single is not null)
        {
            return [single];
        }

        string prefix = value + ".";

        List<CommandDescriptor> matches = all
            .Where(command => string.Equals(command.Namespace, value, StringComparison.Ordinal)
                              || command.Namespace.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        return matches.Count == 0 ? null : matches;
    }

    /// <summary>
    /// Ergänzt den Dokumenttitel um den Bereich, damit Dateiname und Titelseite passen.
    /// </summary>
    /// <param name="scope">Der gewählte Bereich oder <c>null</c>.</param>
    /// <returns>Der Basis-Titel oder „Basis-Titel - Bereich“.</returns>
    private string ResolveTitle(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? this.options.Value.DocumentationTitle
            : $"{this.options.Value.DocumentationTitle} - {scope.Trim()}";

    /// <summary>
    /// Entfernt einen YAML-Frontmatter-Block am Anfang einer Notiz.
    /// </summary>
    /// <param name="note">Der Inhalt der Notiz.</param>
    /// <returns>Die Notiz ohne Frontmatter.</returns>
    private static string StripFrontMatter(string note)
    {
        if (!note.StartsWith("---", StringComparison.Ordinal))
        {
            return note;
        }

        int end = note.IndexOf("\n---", 3, StringComparison.Ordinal);

        if (end < 0)
        {
            return note;
        }

        return note[(end + 4)..].TrimStart('\r', '\n');
    }

    private static string Slug(string value) =>
        string.Concat(value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-'))
            .Trim('-');
}
