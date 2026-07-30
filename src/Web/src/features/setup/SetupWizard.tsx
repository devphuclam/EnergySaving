import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { EngineerCandidate, OperationalWorkspaceStatus, WorkspaceStep } from './setupTypes'

const stepLabels: Record<WorkspaceStep, string> = {
  SiteAndEngineer: 'Site và phân quyền Engineer',
  Area: 'Area',
  Asset: 'Asset',
  MeasurementPoint: 'Measurement Point',
  DataSource: 'Data Source',
  Mapping: 'Source Mapping',
  SimulatorConfiguration: 'Cấu hình Simulator',
  ValidateAndActivate: 'Kiểm tra và kích hoạt',
}

export function SetupWizard({ onSimulator }: { onSimulator: () => void }) {
  const gateway = useWebGateways().workspace
  const [status, setStatus] = useState<OperationalWorkspaceStatus>()
  const [engineers, setEngineers] = useState<EngineerCandidate[]>([])
  const [engineerId, setEngineerId] = useState('')
  const [name, setName] = useState('')
  const [fields, setFields] = useState<Record<string, string>>({})
  const [feedback, setFeedback] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [retryKey, setRetryKey] = useState(crypto.randomUUID())
  const [reviewStep, setReviewStep] = useState<WorkspaceStep>()
  const [metrics, setMetrics] = useState<Array<{ id: string; label: string }>>([])
  const [units, setUnits] = useState<Array<{ id: string; label: string }>>([])
  const nameInput = useRef<HTMLInputElement>(null)
  const pointSelect = useRef<HTMLSelectElement>(null)
  const activationKeys = useRef<Record<string, string>>({})

  const reload = useCallback(async () => {
    const next = await gateway.getStatus()
    setStatus(next)
    setReviewStep(undefined)
    if (next.roleMode === 'Administrator') {
      const candidates = await gateway.listEngineers()
      setEngineers(candidates)
      if (!engineerId && candidates[0]) setEngineerId(candidates[0].userId)
    }
    if (next.nextStep === 'MeasurementPoint') {
      const [metricOptions, unitOptions] = await Promise.all([
        gateway.listOptions('metrics'),
        gateway.listOptions('units'),
      ])
      setMetrics(metricOptions)
      setUnits(unitOptions)
      setFields(current => ({
        ...current,
        metricId: current.metricId || metricOptions[0]?.id || '',
        unitId: current.unitId || unitOptions[0]?.id || '',
        dataOwnerUserId: current.dataOwnerUserId ||
          next.currentUserId || engineerId,
      }))
    }
  }, [engineerId, gateway])

  useEffect(() => { void reload().catch(() => setFeedback('Không thể tải trạng thái thiết lập.')) }, [reload])

  const completed = useMemo(() => new Set(status?.completedSteps ?? []), [status])
  const step = status?.nextStep
  const displayStep = reviewStep ?? step
  const editingStep = reviewStep ? undefined : step

  async function run(action: () => Promise<{
    ok: boolean
    status: number
    errorCode?: string
  }>) {
    setSubmitting(true)
    setFeedback('')
    try {
      const result = await action()
      if (!result.ok) {
        setFeedback(`Không thể lưu: ${result.errorCode ?? 'RUNTIME_ERROR'}`)
        if (result.status === 409) {
          ;(nameInput.current ?? pointSelect.current)?.focus()
        }
        return
      }
      setRetryKey(crypto.randomUUID())
      setName('')
      await reload()
      setFeedback('Đã lưu trạng thái trên máy chủ.')
    } catch {
      setFeedback('Mất kết nối. Bạn có thể thử lại an toàn.')
    } finally { setSubmitting(false) }
  }

  async function saveCurrent() {
    if (!status || !step) return
    if (step !== 'ValidateAndActivate' && !name.trim() &&
      step !== 'MeasurementPoint') {
      setFeedback('Vui lòng nhập tên trước khi tiếp tục.')
      ;(nameInput.current ?? pointSelect.current)?.focus()
      return
    }
    const chain = status.chain ?? {}
    if (step === 'SiteAndEngineer' && !chain.siteId) {
      await run(() => gateway.mutate(`sites?name=${encodeURIComponent(name)}`, 'POST', undefined, undefined, retryKey))
      return
    }
    if (step === 'SiteAndEngineer' && chain.siteId && status.authorizedSites[0]?.status !== 'Active') {
      await run(() => gateway.mutate(`sites/${chain.siteId}/activate`, 'POST', undefined, chain.siteVersion, retryKey))
      return
    }
    if (step === 'SiteAndEngineer' && chain.siteId && engineerId) {
      await run(() => gateway.assignEngineer(chain.siteId!, engineerId, retryKey))
      return
    }
    if (step === 'Area' && chain.siteId) {
      await run(() => gateway.mutate(`sites/${chain.siteId}/areas`, 'POST', { name }, undefined, retryKey))
      return
    }
    if (step === 'Asset' && chain.areaId) {
      await run(() => gateway.mutate(`areas/${chain.areaId}/assets`, 'POST', { name }, undefined, retryKey))
      return
    }
    if (step === 'MeasurementPoint' && chain.assetId) {
      await run(() => gateway.mutate(`assets/${chain.assetId}/points`, 'POST', {
        name,
        metricId: fields.metricId,
        unitId: fields.unitId,
        dataOwnerUserId: fields.dataOwnerUserId,
        expectedIntervalSeconds: Number(fields.expectedIntervalSeconds || 10),
        noDataAfterSeconds: Number(fields.noDataAfterSeconds || 30),
      }, undefined, retryKey))
      return
    }
    if (step === 'DataSource' && chain.siteId) {
      await run(() => gateway.mutate('data-sources', 'POST', {
        name,
        siteId: chain.siteId,
      }, undefined, retryKey))
      return
    }
    if (step === 'Mapping' && chain.sourceId && chain.pointId) {
      await run(() => gateway.mutate('source-point-mappings', 'POST', {
        name: name || 'Simulator Mapping',
        sourceId: chain.sourceId,
        pointId: chain.pointId,
        effectiveFromUtc: new Date().toISOString(),
      }, undefined, retryKey))
      return
    }
    if (step === 'SimulatorConfiguration' && chain.sourceId) {
      await run(() => gateway.mutate('simulator-configurations', 'POST', {
        name: name || 'Simulator Configuration',
        sourceId: chain.sourceId,
        scenarioType: fields.scenarioType || 'Constant',
        intervalSeconds: Number(fields.intervalSeconds || 10),
        minimumValue: Number(fields.minimumValue || 10),
        maximumValue: Number(fields.maximumValue || 10),
        deterministicSeed: Number(fields.deterministicSeed || 42),
      }, undefined, retryKey))
    }
  }

  async function validateAndActivate() {
    if (!status?.chain) return
    setSubmitting(true)
    setFeedback('')
    try {
      const validation = await gateway.validate(status.chain)
      if (!validation.valid) {
        setFeedback(`Chuỗi chưa hợp lệ: ${validation.failures[0]?.errorCode ?? 'VALIDATION_FAILED'}`)
        return
      }
      const c = status.chain
      const definitions: Record<string, [string, number | undefined]> = {
        site: [`sites/${c.siteId}/activate`, validation.versions.site],
        area: [`areas/${c.areaId}/activate`, validation.versions.area],
        asset: [`assets/${c.assetId}/activate`, validation.versions.asset],
        'data-source': [`data-sources/${c.sourceId}/activate`, validation.versions.source],
        mapping: [`source-point-mappings/${c.mappingId}/activate`, validation.versions.mapping],
        'measurement-point': [`points/${c.pointId}/activate`, validation.versions.point],
      }
      for (const operation of validation.activationSteps) {
        const [path, version] = definitions[operation]
        const key = activationKeys.current[operation] ?? crypto.randomUUID()
        activationKeys.current[operation] = key
        const result = await gateway.mutate(path, 'POST', undefined, version, key)
        if (!result.ok) {
          setFeedback(`Kích hoạt dừng tại ${path}: ${result.errorCode ?? result.status}`)
          await reload()
          return
        }
        delete activationKeys.current[operation]
        await reload()
      }
      await reload()
      setFeedback('Thiết lập hoàn tất. Simulator chưa được khởi động.')
      onSimulator()
    } catch {
      setFeedback('Không thể hoàn tất kích hoạt. Không có trạng thái giả được hiển thị.')
    } finally { setSubmitting(false) }
  }

  if (!status) return <section className="setup-card"><p>Đang tải trạng thái thiết lập…</p></section>
  if (status.landing === 'DependencyError') return <section className="notice notice-warning"><strong>Không thể kết nối dịch vụ phụ thuộc.</strong><span>Không có dữ liệu mẫu thay thế.</span></section>
  if (status.landing === 'NoAuthorizedScope') return <section className="notice notice-warning"><strong>Bạn chưa được cấp phạm vi truy cập.</strong><span>Vui lòng liên hệ Administrator.</span></section>

  return <section className="setup-workspace">
    <header className="page-heading"><p className="eyebrow">THIẾT LẬP VẬN HÀNH</p><h1>Thiết lập cấu hình ban đầu</h1><p>Tiến độ được đọc từ máy chủ và không phụ thuộc trình duyệt.</p></header>
    <ol className="setup-steps" aria-label="Các bước thiết lập">
      {(Object.keys(stepLabels) as WorkspaceStep[]).map((item, index) =>
        <li key={item} className={completed.has(item) ? 'complete' : item === displayStep ? 'current' : ''}>
          <span>{index + 1}</span>{stepLabels[item]}
        </li>)}
    </ol>
    <div className="setup-grid">
      <div className="setup-card">
        <h2>{displayStep ? stepLabels[displayStep] : 'Hoàn tất'}</h2>
        {reviewStep && <p>Bước đã lưu được hiển thị chỉ đọc. Chọn Tải lại để quay về bước tiếp theo trên máy chủ.</p>}
        {editingStep === 'SiteAndEngineer' && status.roleMode === 'Engineer' && <p>Site được Administrator cấp và chỉ đọc tại bước này.</p>}
        {editingStep === 'SiteAndEngineer' && status.roleMode === 'Administrator' && status.chain?.siteId && status.authorizedSites[0]?.status === 'Active' &&
          <label>Engineer<select value={engineerId} onChange={event => setEngineerId(event.target.value)}>{engineers.map(item => <option key={item.userId} value={item.userId}>{item.username}</option>)}</select></label>}
        {editingStep && editingStep !== 'ValidateAndActivate' && !(editingStep === 'SiteAndEngineer' && status.roleMode === 'Engineer') &&
          <label>Tên hiển thị<input ref={nameInput} value={name} onChange={event => setName(event.target.value)} /></label>}
        {editingStep === 'MeasurementPoint' && <>
          <label>Metric<select ref={pointSelect} value={fields.metricId ?? ''} onChange={event => setFields(current => ({ ...current, metricId: event.target.value }))}>{metrics.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}</select></label>
          <label>Unit<select value={fields.unitId ?? ''} onChange={event => setFields(current => ({ ...current, unitId: event.target.value }))}>{units.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}</select></label>
          <label>Data Owner<select value={fields.dataOwnerUserId ?? status.currentUserId ?? ''} onChange={event => setFields(current => ({ ...current, dataOwnerUserId: event.target.value }))}>{status.currentUserId && <option value={status.currentUserId}>Tài khoản hiện tại</option>}{engineers.filter(item => item.userId !== status.currentUserId).map(item => <option key={item.userId} value={item.userId}>{item.username}</option>)}</select></label>
          {['expectedIntervalSeconds', 'noDataAfterSeconds'].map(field =>
            <label key={field}>{field}<input value={fields[field] ?? ''} onChange={event => setFields(current => ({ ...current, [field]: event.target.value }))} /></label>)}
        </>}
        {editingStep === 'SimulatorConfiguration' && ['scenarioType', 'intervalSeconds', 'minimumValue', 'maximumValue', 'deterministicSeed'].map(field =>
          <label key={field}>{field}<input value={fields[field] ?? ''} onChange={event => setFields(current => ({ ...current, [field]: event.target.value }))} /></label>)}
        <div className="setup-actions">
          <button className="button button-quiet" type="button" disabled={submitting || status.completedSteps.length === 0}
            onClick={() => {
              const currentIndex = reviewStep
                ? status.completedSteps.indexOf(reviewStep) - 1
                : status.completedSteps.length - 1
              setReviewStep(status.completedSteps[Math.max(0, currentIndex)])
              setFeedback('Đang xem bước đã lưu ở chế độ chỉ đọc.')
            }}>Quay lại</button>
          {editingStep && editingStep !== 'ValidateAndActivate' && <button className="button button-primary" disabled={submitting} onClick={() => void saveCurrent()}>Lưu và tiếp tục</button>}
          {editingStep === 'ValidateAndActivate' && <button className="button button-primary" disabled={submitting} onClick={() => void validateAndActivate()}>Kiểm tra và kích hoạt</button>}
          <button className="button button-quiet" type="button" onClick={() => void reload()}>Tải lại</button>
          <button className="button button-quiet" type="button" disabled={submitting} onClick={() => {
            setName('')
            setFields({})
            setRetryKey(crypto.randomUUID())
            setFeedback('Đã hủy các thay đổi chưa lưu.')
          }}>Hủy</button>
        </div>
        <p role="status" aria-live="polite">{submitting ? 'Đang xử lý…' : feedback}</p>
      </div>
      <aside className="setup-card setup-summary"><h2>Tóm tắt</h2><p>{status.completedSteps.length}/8 bước hoàn tất</p><p>Site: {status.authorizedSites[0]?.name ?? 'Chưa có'}</p><p>Simulator tự khởi động: Không</p></aside>
    </div>
  </section>
}
