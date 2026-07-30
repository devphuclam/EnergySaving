import type { EngineerCandidate, MutationResult, OperationalWorkspaceStatus, WorkspaceGateway } from './setupTypes'

async function json<T>(response: Response): Promise<T> {
  const text = await response.text()
  return (text ? JSON.parse(text) : {}) as T
}

async function antiforgeryToken(): Promise<string> {
  const response = await fetch('/api/v1/auth/antiforgery', { headers: { Accept: 'application/json' } })
  if (!response.ok) throw new Error('ANTIFORGERY_UNAVAILABLE')
  const body = await json<{ token?: string }>(response)
  if (!body.token) throw new Error('ANTIFORGERY_UNAVAILABLE')
  return body.token
}

async function mutation(path: string, method: 'POST' | 'PUT' | 'DELETE',
  body?: Record<string, unknown>, version?: number, retryKey: string = crypto.randomUUID()): Promise<MutationResult> {
  const token = await antiforgeryToken()
  const headers: Record<string, string> = {
    Accept: 'application/json',
    'Content-Type': 'application/json',
    'Idempotency-Key': retryKey,
    'X-XSRF-TOKEN': token,
  }
  if (version) headers['If-Match'] = `"${version}"`
  const response = await fetch(`/api/v1/${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  })
  const result = await json<Record<string, unknown>>(response)
  return {
    ok: response.ok,
    status: response.status,
    body: result,
    etag: response.headers.get('ETag') ?? undefined,
    errorCode: typeof result.errorCode === 'string' ? result.errorCode : undefined,
  }
}

export const setupGateway: WorkspaceGateway = {
  async getStatus() {
    const response = await fetch('/api/v1/operational-workspace/status', { headers: { Accept: 'application/json' } })
    if (!response.ok && response.status !== 503) throw new Error(`WORKSPACE_${response.status}`)
    return json<OperationalWorkspaceStatus>(response)
  },
  async listEngineers() {
    const response = await fetch('/api/v1/operational-workspace/engineers', { headers: { Accept: 'application/json' } })
    if (!response.ok) return []
    return (await json<{ items: EngineerCandidate[] }>(response)).items
  },
  assignEngineer(siteId, engineerId, retryKey) {
    return mutation(`operational-workspace/sites/${siteId}/engineers/${engineerId}`, 'POST', undefined, undefined, retryKey)
  },
  mutate: mutation,
  async listOptions(resource) {
    const response = await fetch(`/api/v1/${resource}`, { headers: { Accept: 'application/json' } })
    if (!response.ok) return []
    const values = await json<Array<Record<string, unknown>>>(response)
    return values.map(value => ({
      id: String(value.id ?? value.metricId ?? value.unitId ?? ''),
      label: String(value.name ?? value.symbol ?? value.code ?? value.id ?? ''),
    })).filter(value => value.id)
  },
  async validate(chain) {
    const names = ['siteId', 'areaId', 'assetId', 'pointId', 'sourceId', 'mappingId', 'configurationId'] as const
    const query = new URLSearchParams()
    for (const name of names) {
      const value = chain[name]
      if (value) query.set(name, value)
    }
    const response = await fetch(`/api/v1/operational-workspace/chains/validate?${query}`, { headers: { Accept: 'application/json' } })
    if (!response.ok) throw new Error(`VALIDATION_${response.status}`)
    return json(response)
  },
}
