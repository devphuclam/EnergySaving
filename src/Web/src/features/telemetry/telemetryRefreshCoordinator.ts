export type RefreshRequestContext = {
  signal: AbortSignal
  requestId: number
  isCurrent: () => boolean
}

export type RefreshRequest = (context: RefreshRequestContext) => Promise<void>
export type RefreshScheduler = (callback: () => void, delayMs: number) => number
export type RefreshCanceller = (handle: number) => void
export type RefreshEvent = {
  type: 'started' | 'completed'
  selectionKey: string
  requestId: number
}

/**
 * Pure request/timer coordination for the selected Latest/Health view.
 * It owns no React state and never starts a Simulator operation.
 */
export class LatestRefreshCoordinator {
  private readonly schedule: RefreshScheduler
  private readonly cancel: RefreshCanceller
  private readonly intervalMs: number
  private readonly onEvent?: (event: RefreshEvent) => void
  private generation = 0
  private nextRequestId = 0
  private selectionKey?: string
  private request?: RefreshRequest
  private inFlight?: { generation: number; requestId: number; controller: AbortController }
  private timer?: number
  private autoRefresh = true
  private disposed = false

  constructor(
    schedule: RefreshScheduler,
    cancel: RefreshCanceller,
    intervalMs = 10_000,
    onEvent?: (event: RefreshEvent) => void,
  ) {
    this.schedule = schedule
    this.cancel = cancel
    this.intervalMs = intervalMs
    this.onEvent = onEvent
  }

  select(selectionKey: string, request: RefreshRequest) {
    if (this.disposed) return
    this.invalidate()
    this.selectionKey = selectionKey
    this.request = request
    this.start()
  }

  clear() {
    if (this.disposed) return
    this.invalidate()
    this.selectionKey = undefined
    this.request = undefined
  }

  setAutoRefresh(enabled: boolean) {
    if (this.disposed) return
    this.autoRefresh = enabled
    if (!enabled) {
      this.clearTimer()
      return
    }
    if (!this.inFlight && !this.timer && this.request) this.start()
  }

  refresh(): boolean {
    if (this.disposed || this.inFlight || !this.request) return false
    this.clearTimer()
    this.start()
    return true
  }

  dispose() {
    if (this.disposed) return
    this.disposed = true
    this.invalidate()
    this.selectionKey = undefined
    this.request = undefined
  }

  private start() {
    if (this.disposed || this.inFlight || !this.request || !this.selectionKey) return
    this.clearTimer()
    const generation = this.generation
    const requestId = ++this.nextRequestId
    const selectionKey = this.selectionKey
    const controller = new AbortController()
    this.inFlight = { generation, requestId, controller }
    this.onEvent?.({ type: 'started', selectionKey, requestId })
    const context: RefreshRequestContext = {
      signal: controller.signal,
      requestId,
      isCurrent: () => !this.disposed && this.inFlight?.requestId === requestId &&
        this.generation === generation && this.selectionKey === selectionKey,
    }
    void this.execute(context, selectionKey, generation, requestId)
  }

  private async execute(
    context: RefreshRequestContext,
    selectionKey: string,
    generation: number,
    requestId: number,
  ) {
    try {
      await this.request?.(context)
    } catch {
      // The request callback owns user-visible error mapping.
    } finally {
      if (this.inFlight?.requestId === requestId) {
        const isCurrent = !this.disposed && this.generation === generation && this.selectionKey === selectionKey
        this.inFlight = undefined
        this.onEvent?.({ type: 'completed', selectionKey, requestId })
        if (isCurrent && this.autoRefresh) {
          this.timer = this.schedule(() => {
            this.timer = undefined
            if (this.generation === generation && this.selectionKey === selectionKey) this.start()
          }, this.intervalMs)
        }
      }
    }
  }

  private invalidate() {
    this.generation++
    this.clearTimer()
    this.inFlight?.controller.abort()
    this.inFlight = undefined
  }

  private clearTimer() {
    if (this.timer === undefined) return
    this.cancel(this.timer)
    this.timer = undefined
  }
}

export type SelectedPointOption = {
  pointId: string
  siteId: string
  areaId: string
  assetId: string
  code: string
  name: string
  metric: string
  unit: string
}

/** Merge only a point returned by the authorized current-data response. */
export function mergeSelectedPointOption<T extends SelectedPointOption>(
  points: T[],
  selected: T | undefined,
): T[] {
  if (!selected) return points
  return [...points.filter(point => point.pointId !== selected.pointId), selected]
}
