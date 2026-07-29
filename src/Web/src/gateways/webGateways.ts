export type GatewayState = 'loading' | 'ready' | 'forbidden' | 'expired' | 'no-data' | 'error'

export type AuthSession = {
  state: GatewayState
  username?: string
  scopeLabel?: string
  isAdministrator?: boolean
}

export type ConfigurationSummary = {
  state: GatewayState
  siteCount: number
  areaCount: number
  pointCount: number
  hierarchy: string
  catalog: string
  sources: string
  mappings: string
  activation: string
}

export type SimulatorSnapshot = {
  state: GatewayState
  status: 'Stopped' | 'Running' | 'Paused'
  generated: number
  accepted: number
  rejected: number
  sourceId?: string
  runId?: string
  errorCode?: string
  isReplay?: boolean
}

export type LatestSnapshot = {
  state: GatewayState
  value: number | null
  unit?: string
  quality?: string
  health: string
  pointId?: string
  sourceTimestamp?: string
  receivedTimestamp?: string
  reason?: string
  runStatus?: string
  generated?: number
  accepted?: number
  rejected?: number
}

export type AuditSnapshot = {
  state: GatewayState
  eventCount: number
  records?: Array<{ actor?: string; time?: string; object?: string; action?: string; summary?: string; before?: unknown; after?: unknown }>
  nextCursor?: string
}

export type AuthGateway = {
  getSession: () => Promise<AuthSession>
  signIn: (credentials?: { username: string; password: string }) => Promise<AuthSession>
  signOut: () => Promise<void>
}

export type ConfigurationGateway = {
  getSummary: () => Promise<ConfigurationSummary>
  validate: () => Promise<GatewayState>
}

export type SimulatorGateway = {
  getSnapshot: () => Promise<SimulatorSnapshot>
  mutate: (operation: 'start' | 'pause' | 'resume' | 'stop') => Promise<SimulatorSnapshot>
}

export type LatestGateway = { getSnapshot: () => Promise<LatestSnapshot> }
export type AuditGateway = { getSnapshot: (cursor?: string) => Promise<AuditSnapshot> }

export type WebGateways = {
  auth: AuthGateway
  configuration: ConfigurationGateway
  simulator: SimulatorGateway
  latest: LatestGateway
  audit: AuditGateway
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, { ...init, headers: { Accept: 'application/json', ...init?.headers } })
  if (response.status === 401) throw new Error('expired')
  if (response.status === 403) throw new Error('forbidden')
  if (!response.ok) throw new Error(`request-${response.status}`)
  return response.json() as Promise<T>
}

async function antiforgeryToken(): Promise<string> {
  const response = await fetch('/api/v1/auth/antiforgery', { headers: { Accept: 'application/json' } })
  if (!response.ok) throw new Error(`antiforgery-${response.status}`)
  const body = await response.json() as { token?: string }
  if (!body.token) throw new Error('antiforgery-missing')
  return body.token
}

function stateFromError(error: unknown): GatewayState {
  return error instanceof Error && error.message === 'forbidden' ? 'forbidden' : error instanceof Error && error.message === 'expired' ? 'expired' : 'error'
}

export const webGateways: WebGateways = {
  auth: {
    getSession: async () => {
      try {
        const me = await request<{ username?: string; scopes?: string[]; roles?: string[] }>('/api/v1/me')
        return { state: 'ready', username: me.username, scopeLabel: me.scopes?.join(', ') ?? 'Authorized scope', isAdministrator: me.roles?.includes('Administrator') }
      } catch (error) { return { state: stateFromError(error) } }
    },
    signIn: async (credentials = { username: '', password: '' }) => {
      try {
        const token = await antiforgeryToken()
        await request('/api/v1/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token }, body: JSON.stringify(credentials) })
        return await webGateways.auth.getSession()
      } catch (error) { return { state: stateFromError(error) } }
    },
    signOut: async () => { const token = await antiforgeryToken(); await request('/api/v1/auth/logout', { method: 'POST', headers: { 'X-XSRF-TOKEN': token } }) },
  },
  configuration: {
    getSummary: async () => {
      try {
        const [sites, areas, assets, points] = await Promise.all([
          request<unknown[]>('/api/v1/sites'), request<unknown[]>('/api/v1/areas'),
          request<unknown[]>('/api/v1/assets'), request<unknown[]>('/api/v1/points')
        ])
        return { state: 'ready', siteCount: sites.length, areaCount: areas.length, pointCount: assets.length + points.length, hierarchy: `${sites.length} Sites / ${areas.length} Areas / ${assets.length} Assets / ${points.length} Points`, catalog: 'Catalog gateway ready', sources: 'Source gateway ready', mappings: 'Mapping gateway ready', activation: 'Activation state supplied by server' }
      } catch (error) { return { state: stateFromError(error), siteCount: 0, areaCount: 0, pointCount: 0, hierarchy: 'Unavailable', catalog: 'Unavailable', sources: 'Unavailable', mappings: 'Unavailable', activation: 'Unavailable' } }
    },
    validate: async () => {
      try { await request('/api/v1/simulator-configurations/validate', { method: 'POST', headers: { 'Idempotency-Key': crypto.randomUUID() } }); return 'ready' } catch (error) { return stateFromError(error) }
    },
  },
  simulator: {
    getSnapshot: async () => {
      try {
        const sources = await request<Array<{ id?: string; sourceId?: string }>>('/api/v1/data-sources')
        const sourceId = sources[0]?.sourceId ?? sources[0]?.id
        if (!sourceId) return { state: 'no-data', status: 'Stopped', generated: 0, accepted: 0, rejected: 0 }
        const snapshot = await request<SimulatorSnapshot>(`/api/v1/simulators/${sourceId}/run`)
        return { ...snapshot, sourceId, runId: snapshot.runId }
      } catch (error) { return { state: stateFromError(error), status: 'Stopped', generated: 0, accepted: 0, rejected: 0 } }
    },
    mutate: async (operation) => {
      try {
        const current = await webGateways.simulator.getSnapshot()
        const id = operation === 'start' ? current.sourceId : current.runId
        if (!id) return { ...current, state: 'no-data' }
        return await request<SimulatorSnapshot>(`/api/v1/simulators/${id}/${operation}`, { method: 'POST', headers: { 'Idempotency-Key': crypto.randomUUID() } })
      } catch (error) { return { state: stateFromError(error), status: 'Stopped', generated: 0, accepted: 0, rejected: 0 } }
    },
  },
  latest: {
    getSnapshot: async () => {
      try {
        const points = await request<Array<{ id?: string; pointId?: string }>>('/api/v1/points')
        const pointId = points[0]?.pointId ?? points[0]?.id
        if (!pointId) return { state: 'no-data', value: null, health: 'No Data' }
        const latest = await request<LatestSnapshot>(`/api/v1/points/${pointId}/latest`)
        const health = await request<{ status?: string }>(`/api/v1/points/${pointId}/source-health`)
        return { ...latest, pointId, health: health.status ?? latest.health }
      } catch (error) { return { state: stateFromError(error), value: null, health: 'Unavailable' } }
    },
  },
  audit: {
    getSnapshot: async (cursor) => {
      try {
        const page = await request<{ items?: AuditSnapshot['records']; nextCursor?: string }>(`/api/v1/audit-events?pageSize=50${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`)
        return { state: 'ready', eventCount: page.items?.length ?? 0, records: page.items, nextCursor: page.nextCursor }
      } catch (error) { return { state: stateFromError(error), eventCount: 0 } }
    },
  },
}
