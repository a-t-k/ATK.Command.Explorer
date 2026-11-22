import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { throwError } from 'rxjs';
import { describeApiResponse, isApiResponse } from './apiResponse';

/**
 * Basisklasse für HTTP-Services.
 *
 * Liefert die Standard-Header für Command-Requests und wandelt Fehler in ein
 * `Error`-Objekt um. Framework-neutral – funktioniert auch ohne Angular.
 */
export class BaseHttp {
  /**
   * Standard-Header für alle Command-Requests.
   */
  public header(): { headers: HttpHeaders } {
    return { headers: new HttpHeaders({ 'Content-Type': 'application/json' }) };
  }

  /**
   * Wandelt einen HTTP-Fehler in ein `Error`-Objekt um.
   *
   * Wichtig: `String(error)` auf einem `HttpErrorResponse` ergibt `[object Object]` –
   * der Benutzer sähe dann keine brauchbare Meldung. Deshalb wird die Meldung aus dem
   * Antwort-Body gelesen: Steckt dort eine `ApiResponse`-Hülle, liefert
   * `describeApiResponse` die Hinweise des Servers; sonst wird `message` oder `error`
   * des Bodys verwendet.
   *
   * @param error Der Fehler aus dem `HttpClient`.
   */
  public handleError(error: unknown): ReturnType<typeof throwError> {
    return throwError(() => new Error(BaseHttp.describeError(error)));
  }

  /**
   * Baut eine lesbare Meldung aus einem beliebigen Fehlerwert.
   *
   * @param error Der Fehler aus dem `HttpClient` oder ein anderer Wert.
   */
  public static describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const body: unknown = error.error;

      // Der Command-Endpunkt antwortet auch im Fehlerfall mit der Hülle.
      if (isApiResponse(body)) {
        return describeApiResponse(body);
      }

      // ProblemDetails oder eine andere JSON-Struktur.
      if (body !== null && typeof body === 'object') {
        const candidate = body as { message?: unknown; title?: unknown; detail?: unknown };

        for (const value of [candidate.message, candidate.detail, candidate.title]) {
          if (typeof value === 'string' && value.trim().length > 0) {
            return value;
          }
        }
      }

      if (typeof body === 'string' && body.trim().length > 0) {
        return body;
      }

      return error.status > 0
        ? `${error.status} ${error.statusText || 'Fehler'}`.trim()
        : 'Der Server ist nicht erreichbar.';
    }

    if (error instanceof Error) {
      return error.message;
    }

    if (typeof error === 'string') {
      return error;
    }

    if (error !== null && typeof error === 'object') {
      const candidate = error as { message?: unknown };

      if (typeof candidate.message === 'string' && candidate.message.trim().length > 0) {
        return candidate.message;
      }
    }

    return 'Unbekannter Fehler.';
  }

  /**
   * Liest den Body aus einer Antwort.
   *
   * Altlast aus der `Http`-API: dort war `res.json()` nötig. Mit `HttpClient` parst
   * Angular den Body bereits, deshalb ist die Methode nur noch für Altauftrufe da.
   */
  public extractData(res: { json?: () => unknown }): unknown {
    return typeof res.json === 'function' ? res.json() : res;
  }
}
