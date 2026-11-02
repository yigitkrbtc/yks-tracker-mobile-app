import type { components } from '../api/schema'

export type AuthTokens = components['schemas']['AuthTokens']

const refreshKey = 'nirengi.admin.refresh'

/**
 * Access token only in memory; the refresh token in sessionStorage so a reload keeps the session but closing the
 * tab ends it (ADR-0019). Nothing goes to localStorage or cookies.
 */
class Session {
  private access: { token: string; expiresAt: number } | null = null
  private listeners = new Set<() => void>()

  get accessToken(): string | null {
    return this.access && this.access.expiresAt > Date.now() ? this.access.token : null
  }

  get refreshToken(): string | null {
    try {
      return sessionStorage.getItem(refreshKey)
    } catch {
      return null
    }
  }

  set(tokens: AuthTokens) {
    this.access = { token: tokens.accessToken, expiresAt: Date.parse(tokens.accessTokenExpiresAt) }
    try {
      sessionStorage.setItem(refreshKey, tokens.refreshToken)
    } catch {
      // Private mode: the session lasts until reload.
    }
    this.emit()
  }

  clear() {
    this.access = null
    try {
      sessionStorage.removeItem(refreshKey)
    } catch {
      // ignore
    }
    this.emit()
  }

  subscribe(listener: () => void): () => void {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  private emit() {
    this.listeners.forEach((l) => l())
  }
}

export const session = new Session()

/** Role claim of the current access token; the API enforces it, the panel only uses it to explain a 403. */
export function roleOf(token: string): string | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')))
    return typeof payload.role === 'string' ? payload.role : null
  } catch {
    return null
  }
}
