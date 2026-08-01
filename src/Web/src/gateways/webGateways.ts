import {
  createPendingSimulatorMutation,
  mutationIdentityMatches,
  simulatorErrorKind,
  type PendingSimulatorMutation,
  type SimulatorRetryOperation,
} from './simulatorRetry'

export type GatewayState = 'loading' | 'submitting' | 'success' | 'ready' | 'invalid-credentials' | 'forbidden' | 'expired' | 'no-data' | 'no-selection' | 'validation' | 'conflict' | 'not-found' | 'dependency' | 'runtime-error' | 'error'

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
  selection?: SimulatorSelection
  options?: SimulatorSelectionOption[]
  history?: SimulatorRunHistoryItem[]
  historyTotal?: number
  configurationVersion?: number
  intervalSeconds?: number
  lastProductionAtUtc?: string
}

export type SimulatorSelection = {
  siteId: string
  areaId?: string | null
  assetId?: string | null
  sourceId: string
  configurationId: string
  configurationVersion: number
}

export type SimulatorSelectionOption = {
  siteId: string
  siteCode: string
  siteName: string
  areaId?: string | null
  areaCode?: string | null
  areaName?: string | null
  assetId?: string | null
  assetCode?: string | null
  assetName?: string | null
  sourceId: string
  sourceCode: string
  sourceName: string
  sourceVersion: number
  configurationId: string
  configurationVersion: number
  intervalSeconds: number
  isEligible: boolean
  eligibilityCode?: string | null
}

export type SimulatorRunHistoryItem = {
  runId: string
  sourceId: string
  configurationId: string
  configurationVersion: number
  status: 'Stopped' | 'Running' | 'Paused'
  version: number
  generated: number
  accepted: number
  rejected: number
  lastProductionAtUtc?: string | null
  intervalSeconds: number
  createdAtUtc: string
}

type SimulatorWorkspaceResponse = {
  options?: SimulatorSelectionOption[]
  selection?: SimulatorSelection | null
  currentRun?: SimulatorRunHistoryWire | null
  history?: { items?: SimulatorRunHistoryWire[]; totalCount?: number }
  state?: string
  errorCode?: string | null
}

type SimulatorRunHistoryWire = Record<string, unknown> & {
  runId?: string
  sourceId?: string
  configurationId?: string
  configurationVersion?: number
  status?: 'Stopped' | 'Running' | 'Paused'
  version?: number
  lastProductionAtUtc?: string | null
  intervalSeconds?: number
  createdAtUtc?: string
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
  runId?: string
  generated?: number
  accepted?: number
  rejected?: number
  dataState?: 'NoSelection' | 'Data' | 'NoData' | 'NotConfigured' | 'Ambiguous' | 'HierarchyConflict'
  pointCode?: string
  pointName?: string
  metric?: string
  source?: { sourceId: string; code: string; name: string }
  lastProductionAtUtc?: string
  lastRefreshAt?: string
  expectedIntervalSeconds?: number
  noDataAfterSeconds?: number
  errorCode?: string
}

export type TelemetrySelection = {
  siteId: string
  areaId?: string
  assetId?: string
  pointId: string
}

export type TelemetryOptionSnapshot = {
  state: GatewayState
  sites: Array<{ siteId: string; code: string; name: string }>
  areas: Array<{ areaId: string; siteId: string; code: string; name: string }>
  assets: Array<{ assetId: string; siteId: string; areaId: string; code: string; name: string }>
  points: Array<{ pointId: string; siteId: string; areaId: string; assetId: string; code: string; name: string; metric: string; unit: string }>
  scopedCount?: number
  page?: number
  pageSize?: number
  errorCode?: string
}

export type TelemetryOptionQuery = {
  level: 'sites' | 'areas' | 'assets' | 'points'
  siteId?: string
  areaId?: string
  assetId?: string
  page?: number
  pageSize?: number
  search?: string
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
  create(resource: string, body: Record<string, unknown>, retryKey?: string): Promise<ManagementMutation>
  update(resource: string, id: string, expectedVersion: number, body: Record<string, unknown>, retryKey?: string): Promise<ManagementMutation>
  validate(resource: string, id: string, retryKey?: string): Promise<ManagementMutation>
  reviewSimulatorConfiguration(configurationId: string, draftConfigurationVersion: number, retryKey?: string): Promise<ManagementMutation>
  lifecycle(resource: string, id: string, action: string, expectedVersion: number, retryKey?: string): Promise<ManagementMutation>
  remove(resource: string, id: string, expectedVersion: number, retryKey?: string): Promise<ManagementMutation>
  duplicate(resource: string, id: string, targetSourceId?: string, retryKey?: string): Promise<ManagementMutation>
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
  getSnapshot: (selection?: SimulatorSelection) => Promise<SimulatorSnapshot>
  mutate: (operation: 'start' | 'pause' | 'resume' | 'stop', selection?: SimulatorSelection) => Promise<SimulatorSnapshot>
  clearPendingMutation: () => void
}

export type LatestGateway = {
  getSnapshot: (selection?: TelemetrySelection, signal?: AbortSignal) => Promise<LatestSnapshot>
  getOptions?: (query: TelemetryOptionQuery) => Promise<TelemetryOptionSnapshot>
}
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

function telemetryStateFromError(error: unknown): GatewayState {
  if (!(error instanceof Error)) return 'runtime-error'
  if (error.message === 'forbidden') return 'forbidden'
  if (error.message === 'expired') return 'expired'
  if (error.message === 'request-404') return 'not-found'
  if (error.message === 'request-422') return 'validation'
  if (error.message === 'request-503') return 'dependency'
  return 'runtime-error'
}

function simulatorState(value: string | undefined, errorCode?: string | null): GatewayState {
  if (value === 'success') return 'success'
  if (simulatorErrorKind(errorCode) === 'dependency') return 'dependency'
  if (errorCode === 'VERSION_CONFLICT' || errorCode === 'RUN_VERSION_CONFLICT' || value === 'conflict') return 'conflict'
  if (errorCode === 'SIMULATOR_SELECTION_REQUIRED' || value === 'no-selection') return 'no-selection'
  if (errorCode === 'SIMULATOR_SELECTION_NOT_FOUND' || value === 'not-found') return 'not-found'
  if (errorCode === 'SIMULATOR_SELECTION_INELIGIBLE' || value === 'validation') return 'validation'
  if (errorCode === 'FORBIDDEN' || value === 'forbidden') return 'forbidden'
  if (value === 'runtime-error') return 'runtime-error'
  if (value === 'empty') return 'no-data'
  if (value === 'dependency') return 'dependency'
  if (value === 'validation') return 'validation'
  return value === 'ready' ? 'ready' : 'error'
}

function historyItemFromWire(raw: SimulatorRunHistoryWire): SimulatorRunHistoryItem {
  const numberValue = (name: string) => typeof raw[name] === 'number' ? raw[name] as number : 0
  return {
    runId: raw.runId ?? '', sourceId: raw.sourceId ?? '', configurationId: raw.configurationId ?? '',
    configurationVersion: raw.configurationVersion ?? 0, status: raw.status ?? 'Stopped',
    version: raw.version ?? 0, generated: numberValue('generated' + 'Count'),
    accepted: numberValue('accepted' + 'Count'), rejected: numberValue('rejected' + 'Count'),
    lastProductionAtUtc: raw.lastProductionAtUtc, intervalSeconds: raw.intervalSeconds ?? 0,
    createdAtUtc: raw.createdAtUtc ?? '',
  }
}

function snapshotFromWorkspace(body: SimulatorWorkspaceResponse): SimulatorSnapshot {
  const run = body.currentRun ? historyItemFromWire(body.currentRun) : undefined
  const history = (body.history?.items ?? []).map(historyItemFromWire)
  return {
    state: simulatorState(body.state, body.errorCode),
    status: run?.status ?? 'Stopped',
    generated: run?.generated ?? 0,
    accepted: run?.accepted ?? 0,
    rejected: run?.rejected ?? 0,
    sourceId: body.selection?.sourceId,
    runId: run?.runId,
    version: run?.version,
    selection: body.selection ?? undefined,
    options: body.options ?? [],
    history,
    historyTotal: body.history?.totalCount ?? 0,
    configurationVersion: body.selection?.configurationVersion,
    intervalSeconds: run?.intervalSeconds,
    lastProductionAtUtc: run?.lastProductionAtUtc ?? undefined,
    errorCode: body.errorCode ?? undefined,
  }
}

function selectionQuery(selection: SimulatorSelection): string {
  const query = new URLSearchParams({
    siteId: selection.siteId,
    sourceId: selection.sourceId,
    configurationId: selection.configurationId,
    configurationVersion: String(selection.configurationVersion),
  })
  if (selection.areaId) query.set('areaId', selection.areaId)
  if (selection.assetId) query.set('assetId', selection.assetId)
  return query.toString()
}

async function simulatorRequest<T>(url: string, init?: RequestInit): Promise<{ payload: T; replayed: boolean }> {
  const response = await fetch(url, { ...init, headers: { Accept: 'application/json', ...init?.headers } })
  const text = await response.text()
  let payload: Record<string, unknown> = {}
  let malformed = false
  if (text) {
    try { payload = JSON.parse(text) as Record<string, unknown> } catch { malformed = true }
  }
  if (malformed || !text) throw new Error('MALFORMED_RESPONSE')
  if (response.status === 401) throw new SimulatorHttpError(response.status, 'expired')
  if (response.status === 403) throw new SimulatorHttpError(response.status, 'forbidden')
  if (!response.ok) {
    const code = typeof payload.errorCode === 'string' ? payload.errorCode : `request-${response.status}`
    throw new SimulatorHttpError(response.status, code)
  }
  return { payload: payload as unknown as T, replayed: response.headers.get('X-Idempotency-Replay') === 'true' }
}

class SimulatorHttpError extends Error {
  readonly status: number
  readonly errorCode: string

  constructor(status: number, errorCode: string) {
    super(errorCode)
    this.name = 'SimulatorHttpError'
    this.status = status
    this.errorCode = errorCode
  }
}

let pendingSimulatorMutation: PendingSimulatorMutation | undefined

function isRetryableSimulatorError(error: unknown): boolean {
  if (!(error instanceof Error)) return true
  const status = error instanceof SimulatorHttpError ? error.status : undefined
  return simulatorErrorKind(error.message, status) === 'dependency' || error.name === 'TypeError' ||
    error.message === 'MALFORMED_RESPONSE' || error.message === 'TRANSIENT_DATABASE_CONFLICT' ||
    error.message === 'request-503' || error.message.startsWith('request-5') ||
    error.message.startsWith('antiforgery-5')
}

function mutationStateFromError(errorCode: string, status?: number): GatewayState {
  if (simulatorErrorKind(errorCode, status) === 'dependency') return 'dependency'
  return errorCode.includes('CONFLICT') ? 'conflict' :
    errorCode.includes('NOT_FOUND') ? 'not-found' :
      errorCode.includes('REQUIRED') || errorCode.includes('INELIGIBLE') ? 'validation' :
        errorCode === 'forbidden' ? 'forbidden' : errorCode === 'expired' ? 'expired' : 'runtime-error'
}

async function managementMutation(
  path: string,
  method: 'POST' | 'PUT' | 'DELETE',
  body?: Record<string, unknown>,
  retryKey: string = crypto.randomUUID(),
  expectedVersion?: number,
): Promise<ManagementMutation> {
  try {
    const token = await antiforgeryToken()
    const response = await fetch(`/api/v1/${path}`, {
      method,
      headers: {
        Accept: 'application/json',
        'Idempotency-Key': retryKey,
        'X-XSRF-TOKEN': token,
        ...(body ? { 'Content-Type': 'application/json' } : {}),
        ...(expectedVersion ? { 'If-Match': `"${expectedVersion}"` } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    })
    const text = await response.text()
    let parsed: Record<string, unknown> = {}
    if (text) {
      try { parsed = JSON.parse(text) as Record<string, unknown> } catch { parsed = {} }
    }
    return {
      ok: response.ok,
      status: response.status,
      body: parsed,
      errorCode: typeof parsed.errorCode === 'string' ? parsed.errorCode : undefined,
    }
  } catch {
    return { ok: false, status: 503, errorCode: 'RUNTIME_FAILURE' }
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
    signOut: async () => {
      const token = await antiforgeryToken()
      await request('/api/v1/auth/logout', { method: 'POST', headers: { 'X-XSRF-TOKEN': token } })
      pendingSimulatorMutation = undefined
    },
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
    getSnapshot: async (selection) => {
      try {
        const url = selection
          ? `/api/v1/simulators/workspace?${selectionQuery(selection)}`
          : '/api/v1/simulators/workspace/selectors'
        const workspace = await simulatorRequest<SimulatorWorkspaceResponse>(url)
        return snapshotFromWorkspace(workspace.payload)
      } catch (error) {
        const errorCode = error instanceof Error ? error.message : 'RUNTIME_FAILURE'
        const state: GatewayState = errorCode === 'forbidden' ? 'forbidden' : errorCode === 'expired' ? 'expired' :
          simulatorErrorKind(errorCode, error instanceof SimulatorHttpError ? error.status : undefined) === 'dependency' ? 'dependency' :
            errorCode === 'SIMULATOR_SELECTION_NOT_FOUND' ? 'not-found' :
            errorCode === 'SIMULATOR_SELECTION_INELIGIBLE' ? 'validation' :
              errorCode.includes('CONFLICT') ? 'conflict' : 'runtime-error'
        return { state, status: 'Stopped', generated: 0, accepted: 0, rejected: 0, errorCode }
      }
    },
    mutate: async (operation, selection) => {
      if (!selection) return { state: 'no-selection', status: 'Stopped', generated: 0, accepted: 0, rejected: 0, errorCode: 'SIMULATOR_SELECTION_REQUIRED' }
      try {
        const current = await webGateways.simulator.getSnapshot(selection)
        if (!['ready', 'success'].includes(current.state)) return current

        const retryOperation = operation as SimulatorRetryOperation
        const samePendingRequest = mutationIdentityMatches(pendingSimulatorMutation, retryOperation,
          selection, pendingSimulatorMutation?.runId, pendingSimulatorMutation?.expectedVersion)
        const retryRunId = samePendingRequest ? pendingSimulatorMutation?.runId : current.runId
        const retryVersion = samePendingRequest ? pendingSimulatorMutation?.expectedVersion : current.version
        if (operation !== 'start' && !retryRunId) return { ...current, state: 'not-found', errorCode: 'SIMULATOR_RUN_NOT_FOUND' }
        if (operation !== 'start' && !retryVersion) return { ...current, state: 'validation', errorCode: 'EXPECTED_VERSION_REQUIRED' }
        const pending = samePendingRequest
          ? pendingSimulatorMutation!
          : createPendingSimulatorMutation(retryOperation, selection,
            operation === 'start' ? undefined : retryRunId,
            operation === 'start' ? undefined : retryVersion,
            crypto.randomUUID())
        pendingSimulatorMutation = pending
        const expectedVersion = pending.expectedVersion
        const runId = pending.runId
        const token = await antiforgeryToken()
        const headers: Record<string, string> = {
          'Idempotency-Key': pending.idempotencyKey,
          'Content-Type': 'application/json',
          'X-XSRF-TOKEN': token,
        }
        if (operation !== 'start' && expectedVersion) headers['If-Match'] = `"${expectedVersion}"`
        const path = operation === 'start'
          ? '/api/v1/simulators/workspace/start'
          : `/api/v1/simulators/workspace/runs/${runId}/${operation}`
        const mutation = await simulatorRequest<Record<string, unknown>>(path, {
          method: 'POST', headers, body: JSON.stringify(selection),
        })
        const refreshed = await webGateways.simulator.getSnapshot(selection)
        if (!['ready', 'success'].includes(refreshed.state)) {
          if (!['runtime-error', 'dependency'].includes(refreshed.state)) pendingSimulatorMutation = undefined
          return { ...refreshed, isReplay: mutation.replayed }
        }
        pendingSimulatorMutation = undefined
        return { ...refreshed, state: 'success', isReplay: mutation.replayed,
          errorCode: mutation.replayed ? 'IDEMPOTENT_REPLAY' : undefined }
      } catch (error) {
        const errorCode = error instanceof Error ? error.message : 'RUNTIME_FAILURE'
        if (!isRetryableSimulatorError(error)) pendingSimulatorMutation = undefined
        return { state: mutationStateFromError(errorCode, error instanceof SimulatorHttpError ? error.status : undefined), status: 'Stopped', generated: 0, accepted: 0, rejected: 0, errorCode }
      }
    },
    clearPendingMutation: () => { pendingSimulatorMutation = undefined },
  },
  latest: {
    getOptions: async (query) => {
      try {
        const parameters = new URLSearchParams({ level: query.level })
        if (query.siteId) parameters.set('siteId', query.siteId)
        if (query.areaId) parameters.set('areaId', query.areaId)
        if (query.assetId) parameters.set('assetId', query.assetId)
        if (query.page !== undefined) parameters.set('page', String(query.page))
        if (query.pageSize !== undefined) parameters.set('pageSize', String(query.pageSize))
        if (query.search) parameters.set('search', query.search)
        const body = await request<{
          sites?: TelemetryOptionSnapshot['sites']
          areas?: TelemetryOptionSnapshot['areas']
          assets?: TelemetryOptionSnapshot['assets']
          points?: TelemetryOptionSnapshot['points']
          scopedCount?: number
          page?: number
          pageSize?: number
        }>(`/api/v1/telemetry/workspace/options?${parameters}`)
        return { state: 'ready', sites: body.sites ?? [], areas: body.areas ?? [], assets: body.assets ?? [], points: body.points ?? [], scopedCount: body.scopedCount, page: body.page, pageSize: body.pageSize }
      } catch (error) {
        return { state: telemetryStateFromError(error), sites: [], areas: [], assets: [], points: [], errorCode: error instanceof Error ? error.message : 'RUNTIME_FAILURE' }
      }
    },
    getSnapshot: async (selection, signal) => {
      if (!selection?.siteId || !selection.areaId || !selection.assetId || !selection.pointId)
        return { state: 'no-selection', value: null, health: 'Chưa chọn điểm', dataState: 'NoSelection' }
      try {
        const query = new URLSearchParams({ siteId: selection.siteId, pointId: selection.pointId })
        if (selection.areaId) query.set('areaId', selection.areaId)
        if (selection.assetId) query.set('assetId', selection.assetId)
        const current = await request<{
          point?: { pointId?: string; code?: string; name?: string; metric?: string; unit?: string }
          dataState?: LatestSnapshot['dataState']
          hasData?: boolean
          value?: number | null
          quality?: string
          reasonCode?: string
          sourceTimestampUtc?: string
          receivedAtUtc?: string
          source?: LatestSnapshot['source']
          health?: { status?: string; lastAcceptedReceivedAtUtc?: string; runStatus?: string; generated?: number; accepted?: number; rejected?: number; evaluatedAtUtc?: string; expectedIntervalSeconds?: number; noDataAfterSeconds?: number }
          run?: { runId?: string; status?: string; generated?: number; accepted?: number; rejected?: number; lastProductionAtUtc?: string }
          queriedAtUtc?: string
          errorCode?: string
        }>(`/api/v1/telemetry/workspace/current?${query}`, { signal })
        const noData = current.dataState === 'NoData' || current.dataState === 'NotConfigured'
        const state: GatewayState = current.dataState === 'Data' ? 'ready' : noData ? 'no-data' : 'conflict'
        return {
          state,
          value: current.value ?? null,
          unit: current.point?.unit,
          quality: current.quality,
          health: current.health?.status ?? (noData ? 'Chưa có dữ liệu' : 'Unavailable'),
          pointId: current.point?.pointId ?? selection.pointId,
          pointCode: current.point?.code,
          pointName: current.point?.name,
          metric: current.point?.metric,
          dataState: current.dataState,
          sourceTimestamp: current.sourceTimestampUtc,
          receivedTimestamp: current.receivedAtUtc,
          reason: current.reasonCode,
          source: current.source,
          runStatus: current.run?.status ?? current.health?.runStatus,
          runId: current.run?.runId,
          generated: current.run?.generated ?? current.health?.generated,
          accepted: current.run?.accepted ?? current.health?.accepted,
          rejected: current.run?.rejected ?? current.health?.rejected,
          lastProductionAtUtc: current.run?.lastProductionAtUtc,
          lastRefreshAt: current.queriedAtUtc ?? new Date().toISOString(),
          expectedIntervalSeconds: current.health?.expectedIntervalSeconds,
          noDataAfterSeconds: current.health?.noDataAfterSeconds,
          errorCode: current.errorCode,
        } satisfies LatestSnapshot
      } catch (error) { return { state: telemetryStateFromError(error), value: null, health: 'Unavailable', errorCode: error instanceof Error ? error.message : 'RUNTIME_FAILURE' } }
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
    async create(resource, body, retryKey = crypto.randomUUID()) {
      return managementMutation(`configuration-management/${resource}`, 'POST', body, retryKey)
    },
    async update(resource, id, expectedVersion, body, retryKey = crypto.randomUUID()) {
      return managementMutation(`configuration-management/${resource}/${id}`, 'PUT', body, retryKey, expectedVersion)
    },
    async validate(resource, id, retryKey = crypto.randomUUID()) {
      return managementMutation(`configuration-management/${resource}/${id}/validate`, 'POST', undefined, retryKey)
    },
    async reviewSimulatorConfiguration(configurationId, draftConfigurationVersion, retryKey = crypto.randomUUID()) {
      return managementMutation(`configuration-management/simulator-configurations/${configurationId}/drafts/${draftConfigurationVersion}/review`, 'POST', undefined, retryKey)
    },
    async lifecycle(resource, id, action, expectedVersion, retryKey = crypto.randomUUID()) {
      return managementMutation(`configuration-management/${resource}/${id}/${action}`, 'POST', undefined, retryKey, expectedVersion)
    },
    async remove(resource, id, expectedVersion, retryKey = crypto.randomUUID()) {
      return managementMutation(`configuration-management/${resource}/${id}`, 'DELETE', undefined, retryKey, expectedVersion)
    },
    async duplicate(resource, id, targetSourceId, retryKey = crypto.randomUUID()) {
      return managementMutation(`configuration-management/${resource}/${id}/duplicate`, 'POST',
        targetSourceId ? { targetSourceId } : undefined, retryKey)
    },
    async activateSimulatorConfigurationVersion(configurationId, expectedHeadVersion, draftConfigurationVersion, retryKey = crypto.randomUUID()) {
      return managementMutation(`configuration-management/simulator-configurations/${configurationId}/activate`, 'POST', { expectedHeadVersion, draftConfigurationVersion }, retryKey)
    },
  },
}
import { setupGateway } from '../features/setup/setupGateway'
import type { WorkspaceGateway } from '../features/setup/setupTypes'
