export type GatewayState = 'loading' | 'submitting' | 'ready' | 'invalid-credentials' | 'forbidden' | 'expired' | 'no-data' | 'error'

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
  metricCount: number
  unitCount: number
  sourceCount: number
  mappingCount: number
  configurationCount: number
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
  version?: number
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

export type ManagementFilter = {
  search?: string
  status?: string
  siteId?: string
  areaId?: string
  page: number
  pageSize: number
}

export type ManagementPage = {
  items: Array<Record<string, unknown>>
  totalCount: number
  page: number
  pageSize: number
}

export type ManagementMutation = {
  ok: boolean
  status: number
  body?: Record<string, unknown>
  errorCode?: string
}

export type ManagementGateway = {
  list(resource: string, filter: ManagementFilter): Promise<ManagementPage>
  detail(resource: string, id: string): Promise<Record<string, unknown> | null>
  duplicate(resource: string, id: string, retryKey?: string): Promise<ManagementMutation>
  activateSimulatorConfigurationVersion(
    configurationId: string,
    expectedHeadVersion: number,
    draftConfigurationVersion: number,
    retryKey?: string,
  ): Promise<ManagementMutation>
}

export type AuthGateway = {
  getSession: () => Promise<AuthSession>
  signIn: (credentials: { username: string; password: string }) => Promise<AuthSession>
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
  workspace: WorkspaceGateway
  management: ManagementGateway
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

async function managementMutation(
  path: string,
  method: 'POST',
  body?: Record<string, unknown>,
  retryKey: string = crypto.randomUUID(),
): Promise<ManagementMutation> {
  const token = await antiforgeryToken()
  const response = await fetch(`/api/v1/${path}`, {
    method,
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      'Idempotency-Key': retryKey,
      'X-XSRF-TOKEN': token,
    },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await response.text()
  const parsed = (text ? JSON.parse(text) : {}) as Record<string, unknown>
  return {
    ok: response.ok,
    status: response.status,
    body: parsed,
    errorCode: typeof parsed.errorCode === 'string' ? parsed.errorCode : undefined,
  }
}

export const webGateways: WebGateways = {
  workspace: setupGateway,
  auth: {
    getSession: async () => {
      try {
        const me = await request<{ username?: string; scopes?: string[]; roles?: string[] }>('/api/v1/me')
        return { state: 'ready', username: me.username, scopeLabel: me.scopes?.join(', ') ?? 'Authorized scope', isAdministrator: me.roles?.includes('Administrator') }
      } catch (error) { return { state: stateFromError(error) } }
    },
    signIn: async (credentials) => {
      if (!credentials.username.trim() || !credentials.password) return { state: 'invalid-credentials' }
      try {
        const token = await antiforgeryToken()
        await request('/api/v1/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token }, body: JSON.stringify(credentials) })
        return await webGateways.auth.getSession()
      } catch (error) {
        return { state: error instanceof Error && error.message === 'expired' ? 'invalid-credentials' : stateFromError(error) }
      }
    },
    signOut: async () => { const token = await antiforgeryToken(); await request('/api/v1/auth/logout', { method: 'POST', headers: { 'X-XSRF-TOKEN': token } }) },
  },
  configuration: {
    getSummary: async () => {
      try {
        const [sites, areas, assets, points, metrics, units, sources, mappings, configurations] = await Promise.all([
          request<unknown[]>('/api/v1/sites'), request<unknown[]>('/api/v1/areas'),
          request<unknown[]>('/api/v1/assets'), request<unknown[]>('/api/v1/points'),
          request<unknown[]>('/api/v1/metrics'), request<unknown[]>('/api/v1/units'),
          request<unknown[]>('/api/v1/data-sources'), request<unknown[]>('/api/v1/source-point-mappings'),
          request<unknown[]>('/api/v1/simulator-configurations')
        ])
        return { state: 'ready', siteCount: sites.length, areaCount: areas.length, pointCount: assets.length + points.length, metricCount: metrics.length, unitCount: units.length, sourceCount: sources.length, mappingCount: mappings.length, configurationCount: configurations.length, hierarchy: `${sites.length} Sites / ${areas.length} Areas / ${assets.length} Assets / ${points.length} Points`, catalog: `${metrics.length} metrics / ${units.length} units`, sources: `${sources.length} sources`, mappings: `${mappings.length} mappings`, activation: `${configurations.length} simulator configurations` }
      } catch (error) { return { state: stateFromError(error), siteCount: 0, areaCount: 0, pointCount: 0, metricCount: 0, unitCount: 0, sourceCount: 0, mappingCount: 0, configurationCount: 0, hierarchy: 'Unavailable', catalog: 'Unavailable', sources: 'Unavailable', mappings: 'Unavailable', activation: 'Unavailable' } }
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
        if (operation !== 'start' && !current.version) return { ...current, state: 'error', errorCode: 'EXPECTED_VERSION_REQUIRED' }
        const headers: Record<string, string> = { 'Idempotency-Key': crypto.randomUUID() }
        if (operation !== 'start') headers['If-Match'] = `"${current.version}"`
        return await request<SimulatorSnapshot>(`/api/v1/simulators/${id}/${operation}`, { method: 'POST', headers })
      } catch (error) { return { state: stateFromError(error), status: 'Stopped', generated: 0, accepted: 0, rejected: 0 } }
    },
  },
  latest: {
    getSnapshot: async () => {
      try {
        const points = await request<Array<{ id?: string; pointId?: string }>>('/api/v1/points')
        const pointId = points[0]?.pointId ?? points[0]?.id
        if (!pointId) return { state: 'no-data', value: null, health: 'No Data' }
        const latest = await request<{
          numericValue: number | null
          unitCode?: string
          status: string
          isNoData: boolean
          reasonCode?: string
          sourceTimestampUtc?: string
          receivedAtUtc?: string
          runStatus?: string
          generated?: number
          accepted?: number
          rejected?: number
        }>(`/api/v1/points/${pointId}/latest`)
        const health = await request<{ status?: string }>(`/api/v1/points/${pointId}/source-health`)
        return {
          state: latest.isNoData ? 'no-data' : 'ready',
          value: latest.numericValue,
          unit: latest.unitCode,
          quality: latest.status,
          health: health.status ?? (latest.isNoData ? 'No Data' : 'Unavailable'),
          pointId,
          sourceTimestamp: latest.sourceTimestampUtc,
          receivedTimestamp: latest.receivedAtUtc,
          reason: latest.reasonCode,
          runStatus: latest.runStatus,
          generated: latest.generated,
          accepted: latest.accepted,
          rejected: latest.rejected,
        } satisfies LatestSnapshot
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
  management: {
    async list(resource, filter) {
      const query = new URLSearchParams()
      if (filter.search) query.set('search', filter.search)
      if (filter.status) query.set('status', filter.status)
      if (filter.siteId) query.set('siteId', filter.siteId)
      if (filter.areaId) query.set('areaId', filter.areaId)
      query.set('page', String(filter.page))
      query.set('pageSize', String(filter.pageSize))
      return request<ManagementPage>(`/api/v1/configuration-management/${resource}?${query}`)
    },
    async detail(resource, id) {
      try {
        return await request<Record<string, unknown>>(`/api/v1/configuration-management/${resource}/${id}`)
      } catch (error) {
        if (error instanceof Error && error.message.startsWith('request-404')) return null
        throw error
      }
    },
    async duplicate(resource, id, retryKey = crypto.randomUUID()) {
      return managementMutation(`configuration-management/${resource}/${id}/duplicate`, 'POST', undefined, retryKey)
    },
    async activateSimulatorConfigurationVersion(configurationId, expectedHeadVersion, draftConfigurationVersion, retryKey = crypto.randomUUID()) {
      return managementMutation(`configuration-management/simulator-configurations/${configurationId}/activate`, 'POST', { expectedHeadVersion, draftConfigurationVersion }, retryKey)
    },
  },
}
import { setupGateway } from '../features/setup/setupGateway'
import type { WorkspaceGateway } from '../features/setup/setupTypes'
