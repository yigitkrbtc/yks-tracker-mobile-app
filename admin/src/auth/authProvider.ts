import type { AuthProvider } from '@refinedev/core'
import { api, refreshSession } from '../api/client'
import { roleOf, session, type AuthTokens } from './session'

/**
 * Refine sees only the end of sign-in: the login page runs password → TOTP (or enrolment) itself and hands over
 * the tokens. Admin role and TOTP are enforced by the API; a non-admin token is dropped here.
 */
export const authProvider: AuthProvider = {
  async login({ tokens }: { tokens: AuthTokens }) {
    if (roleOf(tokens.accessToken) !== 'admin') {
      return { success: false, error: { name: 'Yetki yok', message: 'Bu hesap yönetici değil.' } }
    }
    session.set(tokens)
    return { success: true, redirectTo: '/' }
  },

  async logout() {
    const refreshToken = session.refreshToken
    if (refreshToken) await api.POST('/v1/auth/logout', { body: { refreshToken } }).catch(() => undefined)
    session.clear()
    return { success: true, redirectTo: '/login' }
  },

  async check() {
    if (session.accessToken || (session.refreshToken && (await refreshSession()))) {
      return { authenticated: true }
    }
    return { authenticated: false, redirectTo: '/login', logout: true }
  },

  async onError(error: { status?: number }) {
    if (error?.status === 401) return { logout: true, redirectTo: '/login' }
    return {}
  },

  async getIdentity() {
    const { data } = await api.GET('/v1/me')
    return data ? { id: data.id, name: `${data.firstName} ${data.lastName}`, email: data.email } : null
  },
}
