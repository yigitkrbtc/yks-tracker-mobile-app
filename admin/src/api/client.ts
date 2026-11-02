import createClient, { type Middleware } from 'openapi-fetch'
import type { components, paths } from './schema'
import { session } from '../auth/session'

export type Schemas = components['schemas']

/** Same origin: the API serves the panel at /admin, Vite proxies /v1 in development. */
export const api = createClient<paths>({ baseUrl: globalThis.location?.origin ?? '' })

let refreshing: Promise<boolean> | null = null

/** One refresh for every request that hit 401 at the same time; refresh tokens rotate. */
export function refreshSession(): Promise<boolean> {
  refreshing ??= (async () => {
    const refreshToken = session.refreshToken
    if (!refreshToken) return false
    const { data } = await api.POST('/v1/auth/refresh', { body: { refreshToken } })
    if (!data) {
      session.clear()
      return false
    }
    session.set(data)
    return true
  })().finally(() => {
    refreshing = null
  })
  return refreshing
}

/** Untouched copies for the 401 retry: the sent request's body has already been read. */
const pristine = new WeakMap<Request, Request>()

const auth: Middleware = {
  async onRequest({ request }) {
    if (request.url.includes('/v1/auth/')) return request
    if (!session.accessToken && session.refreshToken) await refreshSession()
    const token = session.accessToken
    if (token) request.headers.set('Authorization', `Bearer ${token}`)
    pristine.set(request, request.clone())
    return request
  },
  async onResponse({ request, response }) {
    const copy = pristine.get(request)
    pristine.delete(request)
    if (response.status !== 401 || !copy) return response
    if (!(await refreshSession())) return response
    copy.headers.set('Authorization', `Bearer ${session.accessToken}`)
    return fetch(copy)
  },
}

api.use(auth)

export class ApiError extends Error {
  readonly status: number
  readonly type?: string
  readonly errors?: Record<string, string[]>

  constructor(status: number, problem: Schemas['HttpValidationProblemDetails'] | undefined) {
    super(problem?.title ?? `İstek başarısız (${status}).`)
    this.status = status
    this.type = problem?.type ?? undefined
    this.errors = problem?.errors
  }
}

/** Unwraps an openapi-fetch result: the data, or an {@link ApiError} carrying the ProblemDetails title. */
export async function call<T>(promise: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await promise
  if (!response.ok) throw new ApiError(response.status, error as never)
  return data as T
}

/** int32 fields are typed number | string by the OpenAPI generator; the API always sends numbers. */
export const n = (value: number | string): number => Number(value)
