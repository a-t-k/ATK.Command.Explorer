# ATK.Command.Explorer

Swashbuckle-Pendant für command-basierte ASP.NET-Core-APIs.

Statt REST-Endpunkte zu beschreiben, dokumentiert der Explorer **Commands**:
Beschreibung, Properties, Autorisierung und JSON-Schema – abrufbar über HTTP
und nutzbar über eine mitgelieferte Angular-UI.

## Was es liefert

| Endpunkt | Inhalt |
| --- | --- |
| `GET /commands/explorer` | alle Commands als `CommandDescriptor[]` |
| `GET /commands/explorer/tree` | Namespace-Hierarchie |
| `GET /commands/explorer/{name}` | ein Command oder 404 |
| `GET /commands/explorer/documentation/vault` | Obsidian-Vault als ZIP |
| `GET /commands/explorer/documentation/markdown` | eine Markdown-Datei |
| `GET /commands/explorer/documentation/pdf` | PDF-Referenz |
| `GET /commands/explorer/typescript` | TypeScript-Typen als ZIP |
| `GET /commands/explorer/framework?framework=angular` | Angular-Gerüst als ZIP |

Alle Dokumentations-Endpunkte akzeptieren `?scope=` (Namespace oder Command-Name).
Ohne `scope` wird alles exportiert, bei unbekanntem Wert antwortet die API mit `404`.

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
| `ATK.Command.AspNet.ModelBinder` | 0.0.5 | |
| `ATK.Command.CsToTsGenerator` | 1.1.9 | TypeScript-Export |
| `Cortex.Mediator` | 3.1.2 | |
| `PDFsharp` | 6.2.4 | letzte MIT-Version – **nicht** aktualisieren |
| `System.Security.Cryptography.Pkcs` | 10.0.14 | gepinnt wegen NU1902/NU1903 |

> PDFsharp ab 6.5.0 steht unter AGPL-3.0-only und verlangt zusätzlich einen
> Lizenzschlüssel zur Laufzeit. Für ein weitergegebenes NuGet-Paket nicht tragbar.

## Lizenz

MIT – siehe [LICENSE](LICENSE).
