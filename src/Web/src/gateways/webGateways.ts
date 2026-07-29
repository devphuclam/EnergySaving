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
}

export type SimulatorSnapshot = {
  state: GatewayState
  status: 'Stopped' | 'Running' | 'Paused'
  generated: number
  accepted: number
  rejected: number
}

export type LatestSnapshot = {
  state: GatewayState
  value: number | null
  unit?: string
  quality?: string
  health: string
}

export type AuditSnapshot = {
  state: GatewayState
  eventCount: number
}

export type AuthGateway = {
  getSession: () => Promise<AuthSession>
  signIn: () => Promise<AuthSession>
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

function stateFromError(error: unknown): GatewayState {
  return error instanceof Error && error.message === 'forbidden' ? 'forbidden' : error instanceof Error && error.message === 'expired' ? 'expired' : 'error'
}

export const webGateways: WebGateways = {
  auth: {
    getSession: async () => {
      try { return await request<AuthSession>('/api/v1/auth/session') } catch (error) { return { state: stateFromError(error) } }
    },
    signIn: async () => {
      try { return await request<AuthSession>('/api/v1/auth/session', { method: 'POST' }) } catch (error) { return { state: stateFromError(error) } }
    },
    signOut: async () => { await request('/api/v1/auth/session', { method: 'DELETE' }) },
  },
  configuration: {
    getSummary: async () => {
      try {
        const sites = await request<unknown[]>('/api/v1/sites')
        return { state: 'ready', siteCount: sites.length, areaCount: 0, pointCount: 0 }
      } catch (error) { return { state: stateFromError(error), siteCount: 0, areaCount: 0, pointCount: 0 } }
    },
    validate: async () => {
      try { await request('/api/v1/sites?validate=true'); return 'ready' } catch (error) { return stateFromError(error) }
    },
  },
  simulator: {
    getSnapshot: async () => {
      try { return await request<SimulatorSnapshot>('/api/v1/simulators/current') } catch (error) { return { state: stateFromError(error), status: 'Stopped', generated: 0, accepted: 0, rejected: 0 } }
    },
    mutate: async (operation) => {
      try { return await request<SimulatorSnapshot>(`/api/v1/simulators/current/${operation}`, { method: 'POST', headers: { 'Idempotency-Key': crypto.randomUUID() } }) } catch (error) { return { state: stateFromError(error), status: 'Stopped', generated: 0, accepted: 0, rejected: 0 } }
    },
  },
  latest: {
    getSnapshot: async () => {
      try { return await request<LatestSnapshot>('/api/v1/points/current/latest') } catch (error) { return { state: stateFromError(error), value: null, health: 'Unavailable' } }
    },
  },
  audit: {
    getSnapshot: async (cursor) => {
      try {
        const page = await request<{ items?: unknown[] }>(`/api/v1/audit-events?pageSize=50${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`)
        return { state: 'ready', eventCount: page.items?.length ?? 0 }
      } catch (error) { return { state: stateFromError(error), eventCount: 0 } }
    },
  },
}
