import { Injectable, inject } from '@angular/core';
import { HttpBackend, HttpClient, HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { lastValueFrom, Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { BaseHttp } from './base.http';
import { AppConfig } from '../config/config';
import { ICommand } from './';

/**
 * Führt Commands gegen die WebApi aus.
 *
 * Der Dienst kennt keine REST-Endpunkte – alles läuft über `POST /commands` mit einem
 * `$type`-Feld im Body. Welcher Command ausgeführt wird, entscheidet der Server anhand
 * dieses Feldes.
 *
 * ```ts
 * const command = this.commandProcessor.init(TestRequestCommand, { testProp: 'hallo' });
 * const result = await this.commandProcessor.executeAsync(command);
 * ```
 */
@Injectable()
export class CommandProcessor extends BaseHttp {
  private readonly http = inject(HttpClient);
  private readonly config = inject(AppConfig);
  private readonly handler = inject(HttpBackend);

  private readonly servicePath = this.config.setting.commandUrl;

  /**
   * Führt ein Command aus und liefert das Ergebnis als `Promise`.
   *
   * @param command Die Command-Instanz.
   */
  async executeAsync<T>(command: ICommand<T>): Promise<T> {
    const observable: Observable<T> = this.http
      .post<T>(this.servicePath, JSON.stringify(command), super.header())
      .pipe(catchError(super.handleError));

    return await lastValueFrom(observable);
  }

  /**
   * Führt ein Command aus und liefert das Ergebnis als `Observable`.
   *
   * @param command Die Command-Instanz.
   */
  execute<TResult>(command: ICommand<TResult>): Observable<TResult> {
    return this.http
      .post<TResult>(this.servicePath, JSON.stringify(command), super.header())
      .pipe(catchError(super.handleError));
  }

  /**
   * Erzeugt eine echte Instanz einer Command-Klasse.
   *
   * Ein Objekt-Literal würde das private Feld `$type` verlieren – genau das braucht der
   * Server aber für das ModelBinding. `Object.assign(new ctor(), props)` erhält es.
   *
   * @param ctor Die Command-Klasse.
   * @param props Die Eigenschaften.
   */
  init<T extends object>(ctor: new () => T, props: Partial<Omit<T, 'createdTimeStamp' | '_'>>): T {
    const obj = Object.assign(new ctor() as Record<string, unknown>, props);
    obj['createdTimeStamp'] = Date.now();
    return obj as T;
  }

  /**
   * Führt ein Command ohne Interceptors aus.
   *
   * Nötig für Refresh-Token-Flows: Der Aufruf darf nicht erneut in den Interceptor
   * laufen, der ihn ausgelöst hat.
   *
   * @param command Die Command-Instanz.
   * @param token Header-Name und Wert des Tokens.
   */
  async executeAsyncBackend<T>(
    command: ICommand<T>,
    token: { authHeader: string; refreshToken: string }
  ): Promise<T> {
    const httpClient = new HttpClient(this.handler);
    const headers = super.header();
    headers.headers = headers.headers.append(token.authHeader, token.refreshToken);

    const observable = httpClient
      .post<T>(this.servicePath, JSON.stringify(command), headers)
      .pipe(
        catchError((error: HttpErrorResponse): Observable<T> => {
          if (error.error instanceof Error) {
            console.error('An error occurred:', error.error.message);
          } else {
            console.error(
              `Backend returned code ${error.status}, body was: ${JSON.stringify(error.error)}`
            );
          }

          return of(new HttpResponse(undefined) as T);
        })
      );

    return await lastValueFrom(observable);
  }

  /**
   * Lädt eine Datei aus einem Base64-kodierten Modell herunter.
   *
   * @param fileModel Name, Inhalt und MIME-Typ der Datei.
   */
  doDownload(fileModel: {
    name: string;
    base64EncodedContent: string;
    contentType: string;
  }): void {
    const blob = this.b64toBlob(fileModel.base64EncodedContent, fileModel.contentType, 512);
    const blobUrl = window.URL.createObjectURL(blob);
    const anchor = window.document.createElement('a');
    anchor.href = blobUrl;
    anchor.download = fileModel.name;
    anchor.style.display = 'none';
    window.document.body.appendChild(anchor);
    anchor.click();
    URL.revokeObjectURL(blobUrl);
    anchor.remove();
  }

  /**
   * Wandelt einen Base64-String in einen `Blob`.
   *
   * @param b64Data Der Base64-String.
   * @param contentType Der MIME-Typ.
   * @param sliceSize Die Blockgröße.
   */
  b64toBlob(b64Data: string, contentType: string, sliceSize: number): Blob {
    const type = contentType || '';
    const size = sliceSize || 512;
    const byteCharacters = atob(b64Data);
    const byteArrays: Uint8Array[] = [];

    for (let offset = 0; offset < byteCharacters.length; offset += size) {
      const slice = byteCharacters.slice(offset, offset + size);
      const byteNumbers = new Array<number>(slice.length);

      for (let i = 0; i < slice.length; i++) {
        byteNumbers[i] = slice.charCodeAt(i);
      }

      byteArrays.push(new Uint8Array(byteNumbers));
    }

    return new Blob(byteArrays, { type });
  }

  /**
   * Liest eine Datei als Base64-String ohne Data-URL-Präfix.
   *
   * @param file Die Datei aus einem `<input type="file">`.
   */
  async fileToBase64(file: File): Promise<string> {
    return new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.readAsDataURL(file);

      reader.onload = () => {
        if (!reader.result) {
          return;
        }

        // Präfix `data:<type>;base64,` entfernen und Padding ergänzen.
        let encoded = reader.result.toString().replace(/^data:(.*,)?/, '');
        if (encoded.length % 4 > 0) {
          encoded += '='.repeat(4 - (encoded.length % 4));
        }

        resolve(encoded);
      };

      reader.onerror = error => reject(error);
    });
  }
}
