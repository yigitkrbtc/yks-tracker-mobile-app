import { describe, expect, it, vi } from 'vitest'

const store = new Map<string, string>()
vi.stubGlobal('sessionStorage', {
  getItem: (k: string) => store.get(k) ?? null,
  setItem: (k: string, v: string) => void store.set(k, v),
  removeItem: (k: string) => void store.delete(k),
})

vi.stubGlobal('location', { origin: 'http://panel.test' })

// openapi-fetch keeps the fetch it saw at creation; the 401 retry calls the global one.
let handler: (request: Request) => Promise<Response> = async () => new Response(null, { status: 500 })
vi.stubGlobal('fetch', (input: Request) => handler(input))

const { api } = await import('./client')
const { session } = await import('../auth/session')

const tokens = (access: string) => ({
  accessToken: access,
  accessTokenExpiresAt: new Date(Date.now() + 60_000).toISOString(),
  refreshToken: `r-${access}`,
  refreshTokenExpiresAt: new Date(Date.now() + 600_000).toISOString(),
})

describe('api client', () => {
  it('refreshes once on 401 and replays the request with its body', async () => {
    session.set(tokens('old'))
    const seen: { auth: string | null; body: string }[] = []
    handler = async (request) => {
      if (request.url.endsWith('/v1/auth/refresh')) return Response.json(tokens('new'))
      seen.push({ auth: request.headers.get('Authorization'), body: await request.text() })
      return seen.length === 1 ? new Response(null, { status: 401 }) : Response.json({ ok: true })
    }

    const { response } = await api.POST('/v1/admin/curriculum/drafts', { body: { fromVersion: 3 } })

    expect(response.status).toBe(200)
    expect(seen).toEqual([
      { auth: 'Bearer old', body: '{"fromVersion":3}' },
      { auth: 'Bearer new', body: '{"fromVersion":3}' },
    ])
  })
})
