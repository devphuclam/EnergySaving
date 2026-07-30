export type WorkspaceLanding = 'SetupWizard' | 'ContinueSetup' | 'Dashboard' | 'NoAuthorizedScope' | 'DependencyError'
export type WorkspaceStep = 'SiteAndEngineer' | 'Area' | 'Asset' | 'MeasurementPoint' | 'DataSource' | 'Mapping' | 'SimulatorConfiguration' | 'ValidateAndActivate'

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
  getStatus(): Promise<OperationalWorkspaceStatus>
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
