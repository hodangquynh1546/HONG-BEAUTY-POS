import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Capacitor } from '@capacitor/core';

export * from './models';
import type { CurrentUser } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  readonly user = signal<CurrentUser | null>(null);
  private token = sessionStorage.getItem('hbpos_token') ?? '';
  readonly isNative = Capacitor.isNativePlatform();
  readonly serverUrl = signal(localStorage.getItem('hbpos_server_url') ?? '');
  constructor(private readonly http: HttpClient) {}
  setServerUrl(value: string): void {
    let parsed: URL;
    try { parsed = new URL(value.trim()); }
    catch { throw new Error('Địa chỉ máy chủ không hợp lệ.'); }
    if (parsed.protocol !== 'https:' || parsed.username || parsed.password || parsed.pathname !== '/' || parsed.search || parsed.hash) {
      throw new Error('Chỉ nhập địa chỉ HTTPS của máy chủ, không kèm đường dẫn.');
    }
    const url = parsed.origin;
    this.logout();
    localStorage.setItem('hbpos_server_url', url);
    this.serverUrl.set(url);
  }
  private get base(): string {
    if (this.isNative && !this.serverUrl()) throw new Error('Chưa cấu hình địa chỉ máy chủ.');
    return `${this.isNative ? this.serverUrl() : ''}/api/v1`;
  }
  async restore(): Promise<void> {
    if (!this.token) return;
    try { this.user.set(await this.get<CurrentUser>('/me')); }
    catch { this.logout(); }
  }
  async login(email: string, password: string): Promise<void> {
    const result = await this.post<{ accessToken: string }>('/auth/login', { email, password });
    this.token = result.accessToken;
    sessionStorage.setItem('hbpos_token', this.token);
    this.user.set(await this.get<CurrentUser>('/me'));
  }
  logout(): void { this.token = ''; sessionStorage.removeItem('hbpos_token'); this.user.set(null); }
  can(permission: string): boolean {
    const user = this.user();
    return user?.role === 'Owner' || !!user?.permissions.includes(permission);
  }
  private async request<T>(call: Promise<T>): Promise<T> {
    try { return await call; }
    catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401 && this.token) this.logout();
      throw error;
    }
  }
  get<T>(path: string): Promise<T> { return this.request(firstValueFrom(this.http.get<T>(this.base + path, { headers: this.headers() }))); }
  post<T>(path: string, body: unknown): Promise<T> { return this.request(firstValueFrom(this.http.post<T>(this.base + path, body, { headers: this.headers() }))); }
  put<T>(path: string, body: unknown): Promise<T> { return this.request(firstValueFrom(this.http.put<T>(this.base + path, body, { headers: this.headers() }))); }
  patch<T>(path: string, body: unknown): Promise<T> { return this.request(firstValueFrom(this.http.patch<T>(this.base + path, body, { headers: this.headers() }))); }
  delete(path: string): Promise<void> { return this.request(firstValueFrom(this.http.delete<void>(this.base + path, { headers: this.headers() }))); }
  private headers(): HttpHeaders { return this.token ? new HttpHeaders({ Authorization: `Bearer ${this.token}` }) : new HttpHeaders(); }
}
