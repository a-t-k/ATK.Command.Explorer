import { HttpHeaders } from '@angular/common/http';
import { throwError } from 'rxjs';

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
   * @param error Der Fehler aus dem `HttpClient`.
   */
  public handleError(error: unknown): ReturnType<typeof throwError> {
    return throwError(() => new Error(error instanceof Error ? error.message : String(error)));
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
