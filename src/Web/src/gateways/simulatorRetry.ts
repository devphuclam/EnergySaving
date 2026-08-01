export type SimulatorRetryOperation = 'start' | 'pause' | 'resume' | 'stop'

export type SimulatorRetrySelection = {
  siteId: string
  areaId?: string | null
  assetId?: string | null
  sourceId: string
  configurationId: string
  configurationVersion: number
}

export type PendingSimulatorMutation = {
  operation: SimulatorRetryOperation
  selection: SimulatorRetrySelection
  runId?: string
  expectedVersion?: number
  idempotencyKey: string
}

export type SimulatorErrorKind = 'dependency' | 'runtime-error'

const simulatorDependencyErrorCodes = new Set([
  'RUNTIME_DEPENDENCY_UNAVAILABLE',
  'DEPENDENCY_UNAVAILABLE',
  'DEPENDENCY_NOT_READY',
  'SERVICE_UNAVAILABLE',
  'DATABASE_UNAVAILABLE',
  'request-503',
  'antiforgery-503',
])

export function simulatorErrorKind(errorCode?: string | null, status?: number): SimulatorErrorKind {
  if (status === 503 || (errorCode && simulatorDependencyErrorCodes.has(errorCode))) return 'dependency'
  return 'runtime-error'
}

export function selectionFingerprint(selection: SimulatorRetrySelection): string {
  return [selection.siteId, selection.areaId ?? '', selection.assetId ?? '', selection.sourceId,
    selection.configurationId, selection.configurationVersion].join('|')
}

export function mutationIdentityMatches(
  pending: PendingSimulatorMutation | undefined,
  operation: SimulatorRetryOperation,
  selection: SimulatorRetrySelection,
  runId?: string,
  expectedVersion?: number,
): boolean {
  return Boolean(pending && pending.operation === operation &&
    selectionFingerprint(pending.selection) === selectionFingerprint(selection) &&
    pending.runId === runId && pending.expectedVersion === expectedVersion)
}

export function createPendingSimulatorMutation(
  operation: SimulatorRetryOperation,
  selection: SimulatorRetrySelection,
  runId: string | undefined,
  expectedVersion: number | undefined,
  idempotencyKey: string,
): PendingSimulatorMutation {
  return { operation, selection: { ...selection }, runId, expectedVersion, idempotencyKey }
}
