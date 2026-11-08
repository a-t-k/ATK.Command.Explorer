import { Injectable } from '@angular/core';
import { environment } from '../../../../../environments/environment';

/**
 * Reicht die Werte aus `environment.ts` als Service weiter.
 *
 * Dadurch hängt der Feature-Code nicht direkt an der Konfigurationsdatei und lässt sich
 * in Tests leichter ersetzen.
 */
@Injectable()
export class AppConfig {
  private readonly config = environment;

  get setting() {
    return this.config;
  }
}
