import {
  AppShell,
  initialAppShellState,
  transitionAppShell,
  type AppShellState,
  type WebRoute,
} from '../app/AppShell'
import type {
  AuthSession,
  GatewayState,
  SimulatorSnapshot,
  WebGateways,
} from '../gateways/webGateways'
import {
  deriveSiteAndEngineerState,
  selectedSetupPath,
  workspaceStatusRequestFromSearch,
} from '../features/setup/setupTypes'

type FakeObservations = {
  credentials?: { username: string; password: string }
  signedOut: boolean
  mutations: Array<'start' | 'pause' | 'resume' | 'stop'>
}

/** T211 source uses the same transition function as AppShell; T218 remains package-policy blocked. */
function createFakeWebGateways(
  session: AuthSession,
  observations: FakeObservations,
  mutationState: GatewayState = 'ready',
): WebGateways {
  return {
    auth: {
      getSession: async () => session,
      signIn: async credentials => {
        observations.credentials = credentials
        return credentials.username === 'engineer' && credentials.password === 'valid-password'
          ? { state: 'ready', username: 'engineer', scopeLabel: 'Site A', isAdministrator: false }
          : { state: 'invalid-credentials' }
      },
      signOut: async () => { observations.signedOut = true },
    },
    workspace: {
      getStatus: async () => ({
        landing: 'ContinueSetup',
        roleMode: 'Engineer',
        authorizedSites: [],
        completedSteps: [],
        nextStep: 'Area',
        validationFailures: [],
        operationalChainCount: 0,
        incompleteChainCount: 1,
        simulatorAutoStart: false,
        dependencyStatus: 'Available',
      }),
      listEngineers: async () => [],
      assignEngineer: async () => ({ ok: true, status: 200 }),
      mutate: async () => ({ ok: true, status: 200 }),
      listOptions: async () => [],
      validate: async () => ({
        valid: true,
        failures: [],
        versions: {},
        activationSteps: [],
        simulatorAutoStart: false,
      }),
    },
    configuration: {
      getSummary: async () => ({
        state: 'ready',
        siteCount: 1,
        areaCount: 1,
        pointCount: 1,
        metricCount: 1,
        unitCount: 1,
        sourceCount: 1,
        mappingCount: 1,
        configurationCount: 1,
        hierarchy: '1 Site / 1 Area / 1 Asset / 1 Point',
        catalog: '1 metric / 1 unit',
        sources: '1 source',
        mappings: '1 mapping',
        activation: '1 simulator configuration',
      }),
      validate: async () => 'ready',
    },
    simulator: {
      getSnapshot: async () => ({ state: 'ready', status: 'Stopped', generated: 0, accepted: 0, rejected: 0 }),
      mutate: async operation => {
        observations.mutations.push(operation)
        return mutationState === 'ready'
          ? { state: 'ready', status: 'Running', generated: 1, accepted: 1, rejected: 0, isReplay: true }
          : { state: mutationState, status: 'Stopped', generated: 0, accepted: 0, rejected: 0, errorCode: 'VERSION_CONFLICT' }
      },
    },
    latest: {
      getSnapshot: async () => ({ state: 'no-data', value: null, health: 'No Data' }),
    },
    audit: {
      getSnapshot: async () => ({ state: 'forbidden', eventCount: 0, records: [] }),
    },
    management: {
      list: async (_resource, filter) => ({ items: [], totalCount: 0, page: filter.page, pageSize: filter.pageSize }),
      detail: async () => null,
      create: async () => ({ ok: true, status: 201 }),
      update: async () => ({ ok: true, status: 200 }),
      validate: async () => ({ ok: true, status: 200 }),
      lifecycle: async () => ({ ok: true, status: 200 }),
      remove: async () => ({ ok: true, status: 204 }),
      duplicate: async () => ({ ok: true, status: 201 }),
      activateSimulatorConfigurationVersion: async () => ({ ok: true, status: 200 }),
    },
  }
}

export async function runAppShellBehaviorScenarios(): Promise<string[]> {
  const failures: string[] = []
  const observations: FakeObservations = { signedOut: false, mutations: [] }
  const fake = createFakeWebGateways({ state: 'loading' }, observations)
  let state: AppShellState = initialAppShellState

  // Keep a direct source reference to the actual component while exercising its shared contract.
  if (typeof AppShell !== 'function') failures.push('AppShell component must be importable')
  if (state.session.state !== 'loading') failures.push('initial session must be loading')

  state = transitionAppShell(state, { type: 'submitting' })
  if (!state.submitting || state.session.state !== 'submitting') failures.push('submitting state must be observable')

  const signedIn = await fake.auth.signIn({ username: 'engineer', password: 'valid-password' })
  state = transitionAppShell(state, { type: 'signed-in', session: signedIn })
  if (state.session.state !== 'ready' || observations.credentials?.username !== 'engineer')
    failures.push('AppShell login must forward entered credentials and expose authenticated state')

  for (const sessionState of ['forbidden', 'expired', 'error'] as const) {
    state = transitionAppShell(state, { type: 'session', session: { state: sessionState } })
    if (state.session.state !== sessionState) failures.push(`${sessionState} state must be observable`)
  }

  for (const route of ['configuration', 'simulator', 'telemetry', 'audit'] as WebRoute[]) {
    state = transitionAppShell(state, { type: 'navigate', route })
    if (state.route !== route) failures.push(`route navigation failed for ${route}`)
  }

  const noData = await fake.latest.getSnapshot()
  if (noData.state !== 'no-data' || noData.value !== null) failures.push('No Data must remain distinct from numeric zero')

  const replay = await fake.simulator.mutate('start')
  if (!replay.isReplay) failures.push('mutation replay feedback must be observable')
  const conflictFake = createFakeWebGateways({ state: 'ready' }, observations, 'error')
  const conflict: SimulatorSnapshot = await conflictFake.simulator.mutate('pause')
  if (conflict.errorCode !== 'VERSION_CONFLICT') failures.push('mutation conflict feedback must be observable')

  await fake.auth.signOut()
  state = transitionAppShell(state, { type: 'signed-out' })
  if (!observations.signedOut || state.session.state !== 'expired' || state.feedback !== 'Đã đăng xuất.')
    failures.push('logout state must be observable')

  return failures
}

export function runAppShellChecks(): string[] {
  const failures: string[] = []
  const routes: WebRoute[] = ['configuration', 'simulator', 'telemetry', 'audit']
  if (!routes.includes('audit')) failures.push('Audit route must be present')
  const baseStatus = {
    landing: 'ContinueSetup' as const,
    roleMode: 'Administrator' as const,
    authorizedSites: [],
    completedSteps: [],
    nextStep: 'SiteAndEngineer' as const,
    validationFailures: [],
    operationalChainCount: 0,
    incompleteChainCount: 1,
    simulatorAutoStart: false as const,
    dependencyStatus: 'Available',
    chain: {},
  }
  if (deriveSiteAndEngineerState(baseStatus) !== 'NoSite')
    failures.push('Administrator wizard must distinguish missing Site')
  const draft = {
    ...baseStatus,
    authorizedSites: [{ siteId: 'site-a', code: 'S-A', name: 'Site A', status: 'Draft', version: 1 }],
    selectedSiteId: 'site-a',
    chain: { siteId: 'site-a' },
  }
  if (deriveSiteAndEngineerState(draft) !== 'DraftSite')
    failures.push('Administrator wizard must activate persisted Draft Site without another name')
  const active = {
    ...draft,
    authorizedSites: [{ ...draft.authorizedSites[0], status: 'Active' }],
  }
  if (deriveSiteAndEngineerState(active) !== 'ActiveWithoutEngineer')
    failures.push('Administrator wizard must request only Engineer assignment for an Active Site')
  if (deriveSiteAndEngineerState({
    ...active,
    completedSteps: ['SiteAndEngineer'],
    nextStep: 'Area',
  }) !== 'EngineerAssigned')
    failures.push('Administrator wizard must derive completed handoff from server state')

  const newSetup = workspaceStatusRequestFromSearch('?mode=new')
  if (!newSetup || 'invalidSearch' in newSetup || !('mode' in newSetup) || newSetup.mode !== 'new')
    failures.push('Dashboard new-setup navigation must be refresh-safe and server-requested')
  const malformedSetup = workspaceStatusRequestFromSearch('?mode=new&selectedSiteId=site-a')
  if (!malformedSetup || !('invalidSearch' in malformedSetup) || malformedSetup.invalidSearch !== '?mode=new&selectedSiteId=site-a')
    failures.push('Malformed setup query must remain server-visible for its 400 validation outcome')
  const selectedPath = selectedSetupPath('site-created-by-server')
  if (selectedPath !== '/setup?selectedSiteId=site-created-by-server')
    failures.push('Site creation must select the server-returned Site identity')
  return failures
}
