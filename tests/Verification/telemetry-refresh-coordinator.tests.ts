import {
  LatestRefreshCoordinator,
  type RefreshRequestContext,
} from '../../src/Web/src/features/telemetry/telemetryRefreshCoordinator.ts'

type Deferred = {
  context: RefreshRequestContext
  resolve: () => void
}

const scheduled = new Map<number, () => void>()
const events: string[] = []
const pending: Deferred[] = []
let nextTimer = 0
let requests = 0

const coordinator = new LatestRefreshCoordinator(
  (callback) => {
    const handle = ++nextTimer
    scheduled.set(handle, callback)
    return handle
  },
  handle => { scheduled.delete(handle) },
  10_000,
  event => events.push(`${event.type}:${event.selectionKey}:${event.requestId}`),
)

function request(context: RefreshRequestContext): Promise<void> {
  requests++
  return new Promise(resolve => pending.push({ context, resolve }))
}

function check(condition: boolean, message: string) {
  if (!condition) throw new Error(message)
}

async function complete(index = 0) {
  const item = pending.splice(index, 1)[0]
  check(item !== undefined, 'expected an in-flight request')
  item.resolve()
  await Promise.resolve()
  await Promise.resolve()
}

coordinator.select('A', request)
check(requests === 1, 'initial selection must start one request')
coordinator.setAutoRefresh(false)
coordinator.setAutoRefresh(true)
check(requests === 1, 'auto toggle must not duplicate the in-flight request')
check(!coordinator.refresh(), 'manual refresh must not overlap the in-flight request')
await complete()
check(scheduled.size === 1, 'completion must schedule exactly one refresh')
const timer = scheduled.keys().next().value as number
const fireTimer = scheduled.get(timer)
scheduled.delete(timer)
fireTimer?.()
check(requests === 2, 'the configured timer must start one subsequent request')
check(!coordinator.refresh(), 'manual refresh must not overlap the timer request')
await complete()

coordinator.setAutoRefresh(false)
check(scheduled.size === 0, 'disabling auto refresh must cancel future timers')
check(coordinator.refresh(), 'manual refresh must remain available while auto refresh is disabled')
check(requests === 3, 'manual refresh must start exactly one request')
await complete()
check(scheduled.size === 0, 'disabled auto refresh must not schedule a timer')

coordinator.select('B', request)
check(requests === 4 && pending.length === 1, 'selection change must start only the new request')
check(pending[0].context.signal.aborted === false, 'new selection request must not start aborted')
coordinator.select('C', request)
check(pending[0].context.signal.aborted && pending[1].context.signal.aborted === false,
  'selection change must abort the old request and keep the latest request active')
check(events.filter(event => event.startsWith('started:')).length === 5, 'each selection/request start must be observable once')

coordinator.clear()
check(pending[0].context.signal.aborted, 'selection invalidation must abort the stale request')
check(pending[1].context.signal.aborted, 'clear/unmount path must abort the current request')
check(!pending[0].context.isCurrent() && !pending[1].context.isCurrent(),
  'clear/unmount path must invalidate stale and current request contexts')
await complete(1)
await complete(0)
check(scheduled.size === 0, 'clear/unmount path must leave no timer')

console.log(`PASS: coordinator requests=${requests}; events=${events.length}`)
