# Commands im Angular-Frontend verwenden

Diese Anleitung beschreibt, wie du die generierten Commands in eine Angular-Anwendung
einbaust. Sie gehört zum ZIP aus dem Command Explorer (`GET /commands/explorer/framework`).

---

## 1. Was im ZIP steckt

```
<Projektname>/
├─ README.md                     diese Anleitung
├─ base.http.ts                  Basisklasse für HTTP-Services
├─ commandProcessor.ts           führt Commands aus
├─ config/config.ts              AppConfig – liest environment
├─ commands/
│  ├─ ICommand.ts                Marker-Interface
│  ├─ index.ts                   Barrel
│  └─ <Namespace>/…              die generierten Commands
└─ environment.example.ts        Vorlage für environment.ts
```

---

## 2. Dateien kopieren

Kopiere den Inhalt des ZIP nach `src/app/modules/shared/services/`:

```
src/app/modules/shared/services/
├─ config/config.ts
├─ commands/
│  ├─ base.http.ts
│  ├─ commandProcessor.ts
│  ├─ commandsApi.ts
│  ├─ index.ts
│  └─ ui/                        ← die generierten Commands
└─ servicesApi.ts
```

Die Barrel-Kette verbindet die Ordner:

```
modules/shared/index.ts
  → sharedApi.ts
      → services/index.ts
          → servicesApi.ts
              → commands/index.ts
                  → commandsApi.ts   (BaseHttp, CommandProcessor)
                  → ui/index.ts      (ICommand + generierte Commands)
              → config/config.ts     (AppConfig)
```

---

## 3. `environment.ts` anpassen

```ts
export const environment = {
  commandUrl: 'http://localhost:5100/commands/',
};
```

Der Wert muss auf die WebApi zeigen. Für HTTPS den Port aus
`Properties/launchSettings.json` nehmen (`7036`).

---

## 4. `AppConfig` bereitstellen

`AppConfig` liest `environment` und reicht die Werte als Service weiter. Damit hängt der
Feature-Code nicht direkt an der Konfigurationsdatei.

```ts
import { Injectable } from '@angular/core';
import { environment } from '../../../../../environments/environment';

@Injectable()
export class AppConfig {
  private readonly config = environment;

  get setting() {
    return this.config;
  }
}
```

---

## 5. Provider registrieren

`CommandProcessor` und `AppConfig` müssen in `app.config.ts` registriert werden. Ohne
`provideHttpClient()` scheitert die Injektion von `HttpClient`.

```ts
import { ApplicationConfig } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { AppConfig, BaseHttp, CommandProcessor } from './modules/shared';

export const appConfig: ApplicationConfig = {
  providers: [
    AppConfig,
    BaseHttp,
    CommandProcessor,
    provideHttpClient(withFetch())
  ]
};
```

> [!warning] `provideHttpClient()` nicht vergessen
> `CommandProcessor` injiziert `HttpClient` und `HttpBackend`. Fehlt der Provider,
> bricht Angular mit `NullInjectorError: No provider for HttpClient!` ab.

---

## 6. CommandProcessor injizieren

In einer Komponente oder einem Service:

```ts
import { Component, inject } from '@angular/core';
import { CommandProcessor, TestRequestCommand } from './modules/shared';

@Component({
  selector: 'app-beispiel',
  template: `<button (click)="senden()">Senden</button>`
})
export class BeispielComponent {
  private readonly commandProcessor = inject(CommandProcessor);

  async senden(): Promise<void> {
    const command = this.commandProcessor.init(TestRequestCommand, {
      testProp: 'hallo'
    });

    const result = await this.commandProcessor.executeAsync(command);
    console.log(result);
  }
}
```

### Warum `init()` statt `new`?

Die generierten Command-Klassen haben ein privates Feld `$type`. Ein Objekt-Literal
würde es verlieren – der Server kann den Command dann nicht zuordnen.
`Object.assign(new ctor(), props)` erhält es und setzt zusätzlich `createdTimeStamp`.

### Warum `JSON.stringify()`?

Damit werden auch private Felder wie `$type` mitserialisiert. Genau das braucht der
ModelBinder der WebApi.

### Warum `await`?

`executeAsync` gibt ein `Promise<T>` zurück. Ohne `await` hältst du das Promise in der
Hand, nicht das Ergebnis – `console.log` zeigt dann `Promise { <pending> }`. Deshalb ist
die aufrufende Methode `async` und das Ergebnis kommt mit `await`.

> [!warning] `ngOnInit` wartet nicht
> Angular rendert die Komponente, bevor ein Promise aus `ngOnInit` aufgelöst ist. Für
> Werte, die im Template erscheinen sollen, ein Signal setzen:
>
> ```ts
> protected readonly result = signal<TestResult | null>(null);
>
> async ngOnInit(): Promise<void> {
>   const command = this.commandProcessor.init(TestRequestCommand, { testProp: 'x' });
>   this.result.set(await this.commandProcessor.executeAsync(command));
> }
> ```

---

## 7. Die Methoden im Überblick

| Methode | Rückgabe | Zweck |
| --- | --- | --- |
| `executeAsync<T>(command)` | `Promise<T>` | Standardfall |
| `execute<TResult>(command)` | `Observable<TResult>` | wenn du in einer RxJS-Kette bleibst |
| `init<T>(ctor, props)` | `T` | Command-Instanz erzeugen |
| `executeAsyncBackend<T>(command, token)` | `Promise<T>` | ohne Interceptors, für Refresh-Token |
| `doDownload(fileModel)` | `void` | Base64-Datei speichern |
| `b64toBlob(...)` | `Blob` | Base64 → Blob |
| `fileToBase64(file)` | `Promise<string>` | Datei → Base64 |

---

## 8. Fehlerbehandlung

`BaseHttp.handleError` verpackt den Fehler in ein `Error`-Objekt. Für eine eigene
Behandlung kannst du `execute()` nehmen und selbst `catchError` anhängen:

```ts
this.commandProcessor
  .execute(command)
  .pipe(
    catchError(error => {
      console.error('Command fehlgeschlagen', error);
      return of(null);
    })
  )
  .subscribe(result => console.log(result));
```

---

## 9. Checkliste

- [ ] Dateien aus dem ZIP nach `src/app/modules/shared/services/` kopiert
- [ ] `environment.ts` zeigt auf die WebApi
- [ ] `AppConfig` und `CommandProcessor` in `app.config.ts` registriert
- [ ] `provideHttpClient()` in den Providern
- [ ] Barrel-Dateien (`index.ts`) angelegt
- [ ] Erster Testaufruf liefert `200 OK`

---

## 10. Bekannte Stolpersteine

| Symptom | Ursache | Lösung |
| --- | --- | --- |
| `NullInjectorError: No provider for HttpClient!` | `provideHttpClient()` fehlt | Provider ergänzen |
| `NullInjectorError: No provider for AppConfig!` | `AppConfig` nicht registriert | in `providers` aufnehmen |
| `404` beim Ausführen | `commandUrl` falsch | Port und Pfad prüfen |
| `400 Bad Request` | `$type` fehlt im Body | `init()` statt Objekt-Literal verwenden |
| `Cannot find module 'System/DateOnly'` | Generator-Import für `DateOnly` | Datei anlegen oder Typ auf `string` mappen |
| CORS-Fehler im Dev-Server | UI und API auf verschiedenen Ports | CORS-Policy in der WebApi oder `ServeUi` nutzen |
