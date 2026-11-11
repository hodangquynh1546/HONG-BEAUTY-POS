import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { firstValueFrom, type Observable } from 'rxjs';
import { Capacitor } from '@capacitor/core';

export * from './models';
import type { CurrentUser } from './models';

interface SessionResponse { accessToken: string; refreshToken?: string | null; role: 'Owner' | 'Employee'; expiresIn: number; }

const ACCESS_KEY = 'hbpos_token';
const REFRESH_KEY = 'hbpos_refresh';      // native apps only
const SESSION_FLAG_KEY = 'hbpos_session';  // web: "a refresh cookie probably exists" (not a secret)
const SERVER_KEY = 'hbpos_server_url';

/** Tolerates blocked storage (private mode, restricted webviews). */
const store = {
  get: (storage: Storage, key: string): string => { try { return storage.getItem(key) ?? ''; } catch { return ''; } },
  set: (storage: Storage, key: string, value: string): void => { try { storage.setItem(key, value); } catch { /* ignore */ } },
  remove: (storage: Storage, key: string): void => { try { storage.removeItem(key); } catch { /* ignore */ } }
};

@Injectable({ providedIn: 'root' })
export class ApiService {
  readonly user = signal<CurrentUser | null>(null);
  /** Short-lived access token: tab-scoped. */
  private token = store.get(sessionStorage, ACCESS_KEY);
  /**
   * Long-lived rotating refresh token. In browsers it lives only in an HttpOnly cookie that scripts cannot read,
   * so XSS cannot steal it; native apps (another origin, no reliable cookies) keep it themselves.
   */
  private refreshToken = Capacitor.isNativePlatform() ? store.get(localStorage, REFRESH_KEY) : '';
  private webSession = !Capacitor.isNativePlatform() && store.get(localStorage, SESSION_FLAG_KEY) === '1';
  private refreshing: Promise<boolean> | null = null;
  readonly isNative = Capacitor.isNativePlatform();
  readonly serverUrl = signal(store.get(localStorage, SERVER_KEY));

  constructor(private readonly http: HttpClient) {
    // Remove the token an earlier build kept in localStorage.
    if (!this.isNative) store.remove(localStorage, REFRESH_KEY);
  }

  private get canRefresh(): boolean { return this.isNative ? !!this.refreshToken : this.webSession; }

  /** Headers for the auth endpoints: native opts out of cookies; browsers send the CSRF marker header. */
  private authHeaders(): HttpHeaders {
    return this.isNative ? new HttpHeaders({ 'X-Client': 'native' }) : new HttpHeaders({ 'X-Requested-With': 'mieu-web' });
  }
  private authBody(): { refreshToken?: string } { return this.isNative ? { refreshToken: this.refreshToken } : {}; }

  setServerUrl(value: string): void {
    let parsed: URL;
    try { parsed = new URL(value.trim()); }
    catch { throw new Error('Địa chỉ máy chủ không hợp lệ.'); }
    if (parsed.protocol !== 'https:' || parsed.username || parsed.password || parsed.pathname !== '/' || parsed.search || parsed.hash) {
      throw new Error('Chỉ nhập địa chỉ HTTPS của máy chủ, không kèm đường dẫn.');
    }
    const url = parsed.origin;
    this.logout();
    store.set(localStorage, SERVER_KEY, url);
    this.serverUrl.set(url);
  }

  private get base(): string {
    if (this.isNative && !this.serverUrl()) throw new Error('Chưa cấu hình địa chỉ máy chủ.');
    return `${this.isNative ? this.serverUrl() : ''}/api/v1`;
  }

  async restore(): Promise<void> {
    if (!this.token && this.canRefresh) await this.refreshSession();
    if (!this.token) return;
    try { this.user.set(await this.get<CurrentUser>('/me')); }
    catch { this.logout(); }
  }

  async login(email: string, password: string): Promise<void> {
    const session = await firstValueFrom(this.http.post<SessionResponse>(`${this.base}/auth/login`, { email, password }, { headers: this.authHeaders(), withCredentials: true }));
    this.storeSession(session);
    this.user.set(await this.get<CurrentUser>('/me'));
  }

  /** Ends the session locally at once and revokes the refresh token on the server (best effort). */
  logout(): void {
    const hadSession = this.canRefresh;
    const body = this.authBody();
    this.clearSession();
    if (hadSession) {
      try { void firstValueFrom(this.http.post(`${this.base}/auth/logout`, body, { headers: this.authHeaders(), withCredentials: true })).catch(() => undefined); }
      catch { /* server URL not configured */ }
    }
  }

  can(permission: string): boolean {
    const user = this.user();
    return user?.role === 'Owner' || !!user?.permissions.includes(permission);
  }

  get<T>(path: string): Promise<T> { return this.request(() => this.http.get<T>(this.base + path, { headers: this.headers() })); }
  post<T>(path: string, body: unknown): Promise<T> { return this.request(() => this.http.post<T>(this.base + path, body, { headers: this.headers() })); }
  put<T>(path: string, body: unknown): Promise<T> { return this.request(() => this.http.put<T>(this.base + path, body, { headers: this.headers() })); }
  patch<T>(path: string, body: unknown): Promise<T> { return this.request(() => this.http.patch<T>(this.base + path, body, { headers: this.headers() })); }
  delete(path: string): Promise<void> { return this.request(() => this.http.delete<void>(this.base + path, { headers: this.headers() })); }

  /**
   * Runs a request; on 401 it renews the access token once and replays the request.
   * A 401 is returned before any handler runs, so replaying a write cannot duplicate it.
   */
  private async request<T>(call: () => Observable<T>): Promise<T> {
    try { return await firstValueFrom(call()); }
    catch (error) {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !this.canRefresh) throw this.expire(error);
      if (!await this.refreshSession()) throw this.expire(error);
      try { return await firstValueFrom(call()); }
      catch (retryError) { throw this.expire(retryError); }
    }
  }

  private expire(error: unknown): unknown {
    if (error instanceof HttpErrorResponse && error.status === 401 && this.token) this.clearSession();
    return error;
  }

  /** Single-flight: concurrent 401s share one refresh, which matters because each refresh token is single-use. */
  private refreshSession(): Promise<boolean> {
    this.refreshing ??= (async () => {
      try {
        const session = await firstValueFrom(this.http.post<SessionResponse>(`${this.base}/auth/refresh`, this.authBody(), { headers: this.authHeaders(), withCredentials: true }));
        this.storeSession(session);
        return true;
      } catch (error) {
        // Only a definite rejection ends the session; network errors keep the token for the next attempt.
        if (error instanceof HttpErrorResponse && error.status === 401) this.clearSession();
        return false;
      } finally {
        this.refreshing = null;
      }
    })();
    return this.refreshing;
  }

  private storeSession(session: SessionResponse): void {
    this.token = session.accessToken;
    store.set(sessionStorage, ACCESS_KEY, this.token);
    if (this.isNative) {
      this.refreshToken = session.refreshToken ?? '';
      store.set(localStorage, REFRESH_KEY, this.refreshToken);
    } else {
      this.webSession = true;
      store.set(localStorage, SESSION_FLAG_KEY, '1');
    }
  }

  private clearSession(): void {
    this.token = '';
    this.refreshToken = '';
    this.webSession = false;
    store.remove(sessionStorage, ACCESS_KEY);
    store.remove(localStorage, REFRESH_KEY);
    store.remove(localStorage, SESSION_FLAG_KEY);
    this.user.set(null);
  }

  private headers(): HttpHeaders { return this.token ? new HttpHeaders({ Authorization: `Bearer ${this.token}` }) : new HttpHeaders(); }
}
