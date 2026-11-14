# ATK.Command.Explorer

Swashbuckle-Pendant für command-basierte ASP.NET-Core-APIs.

Statt REST-Endpunkte zu beschreiben, dokumentiert der Explorer **Commands**:
Beschreibung, Properties, Autorisierung und JSON-Schema – abrufbar über HTTP
und nutzbar über eine mitgelieferte Angular-UI.

## Was es liefert

Der Explorer hat **genau einen Endpunkt**: `POST /commands/explorer`. Welches Anliegen
erledigt wird, steht im JSON-Feld `$type` – genau wie bei den Commands der Anwendung.

| Command | Ergebnis |
| --- | --- |
| `GetCommandsCommand` | alle Commands als `CommandDescriptor[]` |
| `GetNamespaceTreeCommand` | Namespace-Hierarchie |
| `GetCommandCommand` | ein Command oder `null` |
| `GetAuthenticationsCommand` | Authentifizierungen für das Dropdown |
| `GetDocumentationVaultCommand` | Obsidian-Vault als ZIP (`FileModel`) |
| `GetDocumentationMarkdownCommand` | eine Markdown-Datei (`FileModel`) |
| `GetDocumentationPdfCommand` | PDF-Referenz (`FileModel`) |
| `GetTypeScriptCommand` | TypeScript-Typen als ZIP (`FileModel`) |
| `GetFrameworkCommand` | Angular-Gerüst als ZIP (`FileModel`) |
| `ExecuteAppCommandCommand` | führt ein Anwendungs-Command aus (Proxy) |

Die Dokumentations-Commands akzeptieren `Scope` (Namespace oder Command-Name). Ohne
`Scope` wird alles exportiert, bei unbekanntem Wert antwortet die API mit `400`.

```json
{
  "$type": "ATK.Command.Explorer.Features.Documentation.GetDocumentationPdfCommand, ATK.Command.Explorer",
  "scope": "MyCompany.MyProduct.Orders"
}
```

> [!IMPORTANT]
> `$type` muss das **erste** Feld im JSON sein. Der ModelBinder liest den Typnamen über
> Newtonsoft `TypeNameHandling.Objects`; steht ein anderes Feld davor, schlägt die Bindung
> mit `Cant parse to object.` fehl.

## Installation

```powershell
dotnet add package ATK.Command.Explorer
```

```csharp
builder.Services.Configure<CommandExplorerOptions>(
    builder.Configuration.GetSection(CommandExplorerOptions.SectionName));
builder.Services.AddCommandExplorer<MyApiMarker, IRequestCommand>();
```

`TMarker` liefert die Assembly, `TCommand` das Marker-Interface. Das Paket kennt
weder `IRequestCommand` noch einen Anwendungstyp.

Alternativ ohne Marker-Typen:

```csharp
builder.Services.AddCommandExplorer(
    typeof(Program).Assembly,
    typeof(IRequestCommand),
    options => options.NamespacePrefixes.Add("MyCompany.MyProduct"));
```

## Voraussetzungen

- .NET 10
- Commands implementieren ein Marker-Interface (z. B. `IRequestCommand`)
- `SuppressModelStateInvalidFilter = true`, damit `401` statt `400` zurückkommt:

```csharp
builder.Services.Configure<ApiBehaviorOptions>(
    o => o.SuppressModelStateInvalidFilter = true);
```

## Autorisierung: Commands sind per Default gesperrt

**Jedes Command ist ohne weiteres Zutun geschützt.** Der ModelBinder
(`DefaultCommandAuthentication`) verlangt einen angemeldeten Benutzer, solange kein
`[AllowAnonymous]` gesetzt ist – auch dann, wenn gar kein `[Authorize]` am Command steht.

```csharp
// geschützt: Anmeldung erforderlich, keine Rollen-/Claim-Prüfung
public class CreateOrderCommand : ICommand<CreateOrderResult>, IRequestCommand { }

// geschützt: Anmeldung + Rolle + Claim
[Authorize(Roles = "Admin,Manager")]
[ClaimRequirement("tenant", "acme")]
public class PurgeCacheCommand : ICommand<PurgeCacheResult>, IRequestCommand { }

// offen: ohne Anmeldung ausführbar
[AllowAnonymous]
public class SayHelloCommand : ICommand<string>, IRequestCommand { }
```

| Attribute am Command | Ergebnis ohne Token | Ergebnis mit gültigem Token |
| --- | --- | --- |
| keine | `401` | `200` |
| `[Authorize(Roles = "Admin")]` | `401` | `200` mit Rolle, sonst `403` |
| `[ClaimRequirement("k", "v")]` | `401` | `200` mit Claim, sonst `401` |
| `[AllowAnonymous]` | `200` | `200` |

> Der Explorer zeigt dieselbe Einstufung in `CommandAuthorizationInfo`:
> `AllowAnonymous`, `RequiresAuthentication`, `Roles`, `Policies`, `Claims`.

## Authentifizierungen für die UI (Dropdown)

Der Explorer kann mehrere Authentifizierungen bereitstellen, die in der UI als Dropdown
ausgewählt werden. Die Auswahl gilt für die nächste Command-Ausführung und lässt sich
jederzeit wechseln – ideal, um dasselbe Command mit verschiedenen Konten zu testen.

**Der Token verlässt den Server nie.** Die UI kennt nur `Id`, `Name` und `Type`; das Backend
setzt den passenden Header.

### Variante 1: statisch über die Konfiguration

```json
{
  "CommandExplorer": {
    "Authentications": [
      {
        "Id": "admin",
        "Name": "Admin (tenant=acme)",
        "Type": "Bearer",
        "Token": "eyJhbGciOi..."
      },
      {
        "Id": "apikey",
        "Name": "API-Key",
        "Type": "ApiKey",
        "HeaderName": "X-Api-Key",
        "Token": "geheim"
      },
      {
        "Id": "basic",
        "Name": "Basic",
        "Type": "Basic",
        "UserName": "tester",
        "Password": "geheim"
      }
    ]
  }
}
```

### Variante 2: dynamisch im Code (Lambda)

Für Tokens, die zur Laufzeit beschafft werden müssen – etwa aus einem Identity-Service,
per Client-Credentials oder aus der Datenbank:

```csharp
builder.Services.AddCommandExplorer&lt;Program, IRequestCommand&gt;(options =&gt;
{
    options.Authentications.Add(new CommandExplorerAuthentication
    {
        Id = "service",
        Name = "Service-Konto",
        Type = CommandExplorerAuthenticationType.Bearer,
        TokenFactory = async (services, ct) =&gt;
        {
            ITokenService tokens = services.GetRequiredService&lt;ITokenService&gt;();
            return await tokens.GetAccessTokenAsync(ct);
        }
    });
});
```

`TokenFactory` hat Vorrang vor `Token` und wird bei **jeder** Anfrage aufgerufen – ein Cache
ist Sache der Anwendung. Beide Varianten lassen sich mischen.

### Typen

| `Type` | Header | Token-Bedarf |
| --- | --- | --- |
| `None` | – | keiner |
| `Bearer` | `Authorization: Bearer <token>` | `Token` oder `TokenFactory` |
| `Basic` | `Authorization: Basic <base64>` | `Token` (base64) oder `UserName`/`Password` |
| `ApiKey` | `<HeaderName>: <token>` (Standard `X-Api-Key`) | `Token` oder `TokenFactory` |
| `Custom` | `<HeaderName>: <Prefix><token>` | `Token` oder `TokenFactory` |

### Ablauf

```
1. UI lädt GetAuthenticationsCommand
   → [{ id, name, type, headerName, isDynamic }]   (ohne Token)

2. Benutzer wählt im Dropdown eine Id

3. UI sendet ExecuteAppCommandCommand mit authenticationId
   → Payload (Command-JSON) wird unverändert durchgereicht

4. Explorer-Controller
   → CommandAuthenticationResolver.ResolveHeadersAsync(id)
   → Token aus Konfiguration oder TokenFactory
   → Header bauen (Bearer/Basic/ApiKey/Custom)

5. Server leitet an CommandEndpoint (Standard "commands") weiter
   → gleiche Origin, gleiches Schema
   → Antwort unverändert zurück (Status, Content-Type, Body)
```

### Warum ein Proxy?

Der Browser kann die Header nicht selbst setzen, ohne den Token zu kennen. Ein Proxy löst das:
Die UI sendet nur die `Id`, der Server holt den Token und setzt den Header. Dadurch taucht der
Token nie im Browser auf – auch nicht im Netzwerk-Tab oder in den DevTools.

### Validierung beim Start

Fehlerhafte Einträge fallen sofort beim Start auf, nicht erst beim Ausführen:

- leere `Id`
- doppelte `Id`
- Typ verlangt Token, aber weder `Token` noch `TokenFactory` gesetzt

### Neue Optionen

| Option | Standard | Zweck |
| --- | --- | --- |
| `Authentications` | leer | Liste der auswählbaren Authentifizierungen |
| `AuthenticationHeaderName` | `X-Explorer-Auth` | Header, über den die UI die Id meldet |
| `ShowAuthenticationSelector` | `true` | Dropdown anzeigen (auch ohne Einträge unsichtbar) |
| `CommandEndpoint` | `commands` | Ziel des Proxys, relativ zur aktuellen Origin |

## Optionen

Abschnitt `CommandExplorer` in `appsettings.json`:

| Option | Standard | Zweck |
| --- | --- | --- |
| `NamespacePrefixes` | leer | Prefixe, die im Baum abgeschnitten werden |
| `RoutePrefix` | `commands/explorer` | Basisroute (derzeit fest im `[Route]`-Attribut) |
| `AllowAnonymous` | `true` | Endpunkte ohne Anmeldung erreichbar |
| `Roles` | leer | ODER-Verknüpfung, eine Rolle genügt |
| `Policies` | leer | UND-Verknüpfung, alle müssen greifen |
| `Claims` | leer | UND-Verknüpfung; `Values` intern ODER |
| `AuthenticationSchemes` | leer | Schemata für die Challenge |
| `ServeUi` | `false` | mitgelieferte UI ausliefern |
| `UiPath` | `commands/explorer-ui` | Pfad der UI |
| `DocumentationTitle` | `Command-Referenz` | Titel der Doku + Dateiname |
| `FontDirectory` | leer | `.ttf`-Ordner für PDF |

## Eingebettete UI

```csharp
app.UseCommandExplorerUi();   // vor MapControllers
```

Die Angular-UI liegt als `EmbeddedResource` in der Assembly. `UiPath` darf **nicht**
unter `RoutePrefix` liegen, sonst interpretiert der Controller den UI-Pfad als
Command-Namen.

## Autorisierung des Explorers

Attribute können keine Konfigurationswerte aufnehmen, deshalb setzt eine
`IControllerModelConvention` zur Laufzeit `AllowAnonymousFilter` oder `AuthorizeFilter`.

```
CommandExplorerOptions → CommandExplorerPolicyBuilder → AuthorizationPolicy
                       → CommandExplorerAuthorizationConvention → Filter am Controller
```

Ohne konfiguriertes Authentifizierungsschema wirft die Konvention beim Start eine
`InvalidOperationException` mit Handlungsanweisung – sonst gäbe es zur Laufzeit `500`.

## JSON-Schema

`CommandDescriptor.JsonSchema` und `ResultJsonSchema` folgen Draft 2020-12:

- `$type` immer in `required`, mit `const` auf den Typnamen
- `required` = alle nicht-nullable Wertetypen
- `additionalProperties: false`
- Enum → `type: integer, enum: [...], x-enumNames: {...}`

Die strukturelle Prüfung übernimmt das Schema. Commands selbst validieren über
FluentValidation, nicht über DataAnnotations.

## Build

```powershell
dotnet build
dotnet pack -c Release -o artifacts
```

Beim Packen wird die Angular-UI automatisch gebaut, sofern
`../ATK.Command.Ui/node_modules` existiert. Fehlt es, wird ohne UI gepackt
(Paket ~26 KB statt ~323 KB) und eine Warnung ausgegeben.

| Property | Wirkung |
| --- | --- |
| `-p:BuildExplorerUiOnBuild=true` | UI auch beim normalen Build bauen |
| `-p:SkipExplorerUiBuild=true` | kein UI-Build |

## Abhängigkeiten

| Paket | Version | Anmerkung |
| --- | --- | --- |
| `ATK.Command.AspNet.ModelBinder` | 0.0.5 | ModelBinding der Explorer-Commands |
| `ATK.Command.Communication.Models` | 0.0.3 | `FileModel` für Downloads |
| `ATK.Command.CsToTsGenerator` | 1.1.9 | TypeScript-Export |
| `Cortex.Mediator` | 3.1.2 | |
| `PDFsharp` | 6.2.4 | letzte MIT-Version – **nicht** aktualisieren |
| `System.Security.Cryptography.Pkcs` | 10.0.14 | gepinnt wegen NU1902/NU1903 |

> PDFsharp ab 6.5.0 steht unter AGPL-3.0-only und verlangt zusätzlich einen
> Lizenzschlüssel zur Laufzeit. Für ein weitergegebenes NuGet-Paket nicht tragbar.

## Lizenz

MIT – siehe [LICENSE](LICENSE).
