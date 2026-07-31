export type WorkspaceLanding = 'SetupWizard' | 'ContinueSetup' | 'Dashboard' | 'NoAuthorizedScope' | 'DependencyError'
export type WorkspaceStep = 'SiteAndEngineer' | 'Area' | 'Asset' | 'MeasurementPoint' | 'DataSource' | 'Mapping' | 'SimulatorConfiguration' | 'ValidateAndActivate'
export type SiteAndEngineerState = 'NoSite' | 'DraftSite' | 'ActiveWithoutEngineer' | 'EngineerAssigned'

export type WorkspaceStatusRequest =
  | { mode: 'new'; selectedSiteId?: never }
  | { selectedSiteId: string; mode?: never }
  | { invalidSearch: string }

export type WorkspaceGatewayErrorKind = 'validation' | 'forbidden' | 'notFound' | 'dependency' | 'runtime'

export class WorkspaceGatewayError extends Error {
  readonly status: number
  readonly errorCode?: string
  readonly body?: Record<string, unknown>

  constructor(status: number, body?: Record<string, unknown>) {
    super(`WORKSPACE_${status}`)
    this.name = 'WorkspaceGatewayError'
    this.status = status
    this.errorCode = typeof body?.errorCode === 'string' ? body.errorCode : undefined
    this.body = body
  }
}

export function workspaceGatewayErrorKind(error: unknown): WorkspaceGatewayErrorKind {
  if (!(error instanceof WorkspaceGatewayError)) return 'runtime'
  if (error.status === 400 || error.status === 422) return 'validation'
  if (error.status === 403) return 'forbidden'
  if (error.status === 404) return 'notFound'
  if (error.status === 503) return 'dependency'
  return 'runtime'
}

export function workspaceStatusRequestFromSearch(search: string): WorkspaceStatusRequest | undefined {
  const params = new URLSearchParams(search)
  const hasMode = params.has('mode')
  const hasSelectedSiteId = params.has('selectedSiteId')
  const mode = params.get('mode')
  const selectedSiteId = params.get('selectedSiteId')
  if ((hasMode && mode !== 'new') || (hasSelectedSiteId && !selectedSiteId) ||
    (hasMode && hasSelectedSiteId)) {
    return { invalidSearch: search }
  }
  if (mode === 'new') return { mode: 'new' }
  return selectedSiteId ? { selectedSiteId } : undefined
}

export function selectedSetupPath(siteId: string): string {
  return `/setup?selectedSiteId=${encodeURIComponent(siteId)}`
}

export type WorkspaceChain = {
  siteId?: string
  siteVersion?: number
  areaId?: string
  areaVersion?: number
  assetId?: string
  assetVersion?: number
  pointId?: string
  pointVersion?: number
  sourceId?: string
  sourceVersion?: number
  mappingId?: string
  mappingVersion?: number
  configurationId?: string
  configurationVersion?: number
}

export type OperationalWorkspaceStatus = {
  landing: WorkspaceLanding
  roleMode: 'Administrator' | 'Engineer' | 'ReadOnly'
  authorizedSites: Array<{ siteId: string; code: string; name: string; status: string; version: number }>
  selectedSiteId?: string
  completedSteps: WorkspaceStep[]
  nextStep?: WorkspaceStep
  validationFailures: Array<{ step: WorkspaceStep; field?: string; errorCode: string; messageKey: string }>
  operationalChainCount: number
  incompleteChainCount: number
  simulatorAutoStart: false
  dependencyStatus: string
  errorCode?: string
  chain?: WorkspaceChain
  activationSteps?: string[]
  currentUserId?: string
}

export function deriveSiteAndEngineerState(
  status: OperationalWorkspaceStatus,
): SiteAndEngineerState {
  const siteId = status.chain?.siteId ?? status.selectedSiteId
  const site = status.authorizedSites.find(value => value.siteId === siteId)
  if (!site) return 'NoSite'
  if (site.status !== 'Active') return 'DraftSite'
  return status.nextStep === 'SiteAndEngineer'
    ? 'ActiveWithoutEngineer'
    : 'EngineerAssigned'
}

export type EngineerCandidate = {
  userId: string
  username: string
  status: string
  assignedSiteIds: string[]
}

export type MutationResult = {
  ok: boolean
  status: number
  body?: Record<string, unknown>
  etag?: string
  errorCode?: string
}

export type WorkspaceGateway = {
  getStatus(request?: WorkspaceStatusRequest): Promise<OperationalWorkspaceStatus>
  listEngineers(): Promise<EngineerCandidate[]>
  assignEngineer(siteId: string, engineerId: string, retryKey?: string): Promise<MutationResult>
  mutate(path: string, method: 'POST' | 'PUT' | 'DELETE', body?: Record<string, unknown>, version?: number, retryKey?: string): Promise<MutationResult>
  listOptions(resource: 'metrics' | 'units'): Promise<Array<{ id: string; label: string }>>
  validate(chain: WorkspaceChain): Promise<{
    valid: boolean
    failures: Array<{ step?: string; errorCode: string }>
    versions: Record<string, number>
    activationSteps: string[]
    simulatorAutoStart: false
  }>
}
