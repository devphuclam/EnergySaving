import type { ReactNode } from 'react'
import type { ManagementFilter } from '../../gateways/webGateways'

export type ManagementItem = Record<string, unknown> & { id?: string }
export type { ManagementFilter }
export type ManagementState = 'loading' | 'ready' | 'forbidden' | 'expired' | 'no-data' | 'validation' | 'conflict' | 'not-found' | 'dependency' | 'runtime' | 'error'
export type ManagementFeedback = { tone: 'success' | 'warning' | 'error' | 'info'; message: string; action?: ReactNode } | null
export type ManagementColumn = { key: string; label: string; render?: (item: ManagementItem) => ReactNode }
export type SortDirection = 'ascending' | 'descending'
export type OptionState = 'loading' | 'ready' | 'empty' | 'forbidden' | 'dependency' | 'runtime' | 'expired'
export type OptionName = 'sites' | 'areas' | 'assets' | 'sources' | 'points'
export type ManagementMutationKind = 'create' | 'update' | 'remove' | 'lifecycle' | 'validate' | 'review' | 'duplicate' | 'activate'
export type PendingManagementMutation = {
  resource: string
  kind: ManagementMutationKind
  entityId?: string
  expectedVersion?: number
  payload: Record<string, unknown>
  targetSourceId?: string
  draftVersion?: number
  retryKey: string
}
export type DetailRequestOwner = { token: number; resource: string; entityId: string }
export type ConfigurationFormNormalization = { body: Record<string, unknown>; canonical: Record<string, string>; errors: Array<{ key: string; label: string; message: string }> }

export const configurationEntityKeys = ['sites', 'areas', 'assets', 'points', 'data-sources', 'source-point-mappings', 'simulator-configurations'] as const
export const MANAGEMENT_RESOURCES = [
  { key: 'sites', label: 'Địa điểm' },
  { key: 'areas', label: 'Khu vực' },
  { key: 'assets', label: 'Tài sản' },
  { key: 'points', label: 'Điểm đo' },
  { key: 'data-sources', label: 'Nguồn dữ liệu' },
  { key: 'source-point-mappings', label: 'Ánh xạ nguồn' },
  { key: 'simulator-configurations', label: 'Cấu hình mô phỏng' },
] as const

const SORT_KEYS: Record<string, string[]> = {
  sites: ['code', 'name', 'timezone', 'status', 'version'],
  areas: ['code', 'name', 'status', 'version'],
  assets: ['code', 'name', 'status', 'version'],
  points: ['code', 'metricId', 'unitId', 'dataOwnerUserId', 'status', 'version'],
  'data-sources': ['code', 'name', 'sourceType', 'status', 'version'],
  'source-point-mappings': ['pointId', 'status', 'effectiveFrom', 'effectiveTo', 'version'],
  'simulator-configurations': ['configurationId', 'sourceId', 'currentConfigurationVersion', 'version'],
}

const DEFAULT_SORT: Record<string, { key: string; direction: SortDirection }> = {
  sites: { key: 'code', direction: 'ascending' },
  areas: { key: 'code', direction: 'ascending' },
  assets: { key: 'code', direction: 'ascending' },
  points: { key: 'code', direction: 'ascending' },
  'data-sources': { key: 'code', direction: 'ascending' },
  'source-point-mappings': { key: 'pointId', direction: 'ascending' },
  'simulator-configurations': { key: 'configurationId', direction: 'ascending' },
}

const DETAIL_FIELDS: Record<string, Array<{ key: string; label: string }>> = {
  sites: [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'timezone', label: 'Múi giờ' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }],
  areas: [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }],
  assets: [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }],
  points: [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'description', label: 'Mô tả' }, { key: 'metricId', label: 'Chỉ số' }, { key: 'unitId', label: 'Đơn vị' }, { key: 'dataOwnerUserId', label: 'Chủ dữ liệu' }, { key: 'expectedIntervalSeconds', label: 'Chu kỳ (giây)' }, { key: 'noDataAfterSeconds', label: 'No Data sau (giây)' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }],
  'data-sources': [{ key: 'code', label: 'Mã' }, { key: 'name', label: 'Tên' }, { key: 'sourceType', label: 'Loại nguồn' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }],
  'source-point-mappings': [{ key: 'dataSourceId', label: 'Nguồn dữ liệu' }, { key: 'pointId', label: 'Điểm đo' }, { key: 'effectiveFrom', label: 'Hiệu lực từ' }, { key: 'effectiveTo', label: 'Hiệu lực đến' }, { key: 'status', label: 'Trạng thái' }, { key: 'version', label: 'Phiên bản' }],
  'simulator-configurations': [{ key: 'configurationId', label: 'Mã cấu hình' }, { key: 'sourceId', label: 'Nguồn dữ liệu' }, { key: 'scenarioType', label: 'Kịch bản' }, { key: 'minimumValue', label: 'Giá trị nhỏ nhất' }, { key: 'maximumValue', label: 'Giá trị lớn nhất' }, { key: 'intervalSeconds', label: 'Chu kỳ (giây)' }, { key: 'deterministicSeed', label: 'Hạt giống xác định' }, { key: 'currentConfigurationVersion', label: 'Bản hiện hành' }, { key: 'draftConfigurationVersion', label: 'Bản nháp' }, { key: 'version', label: 'Phiên bản tổng hợp' }],
}

const FIELD_LABELS: Record<string, string> = {
  name: 'Tên',
  siteId: 'Địa điểm cha',
  areaId: 'Khu vực cha',
  assetId: 'Tài sản cha',
  sourceId: 'Nguồn dữ liệu',
  pointId: 'Điểm đo',
  expectedIntervalSeconds: 'Chu kỳ (giây)',
  noDataAfterSeconds: 'No Data sau (giây)',
  minimumValue: 'Giá trị nhỏ nhất',
  maximumValue: 'Giá trị lớn nhất',
  intervalSeconds: 'Chu kỳ (giây)',
  deterministicSeed: 'Hạt giống xác định',
  effectiveFromUtc: 'Hiệu lực từ',
  effectiveToUtc: 'Hiệu lực đến',
}

export type NumericFieldKind = 'positive-int' | 'unsigned-int' | 'finite-decimal'
export const NUMERIC_FIELDS: Record<string, Array<{ key: string; label: string; kind: NumericFieldKind }>> = {
  points: [
    { key: 'expectedIntervalSeconds', label: 'Chu kỳ (giây)', kind: 'positive-int' },
    { key: 'noDataAfterSeconds', label: 'No Data sau (giây)', kind: 'positive-int' },
  ],
  'simulator-configurations': [
    { key: 'minimumValue', label: 'Giá trị nhỏ nhất', kind: 'finite-decimal' },
    { key: 'maximumValue', label: 'Giá trị lớn nhất', kind: 'finite-decimal' },
    { key: 'intervalSeconds', label: 'Chu kỳ (giây)', kind: 'positive-int' },
    { key: 'deterministicSeed', label: 'Hạt giống xác định', kind: 'unsigned-int' },
  ],
}

export function configurationContractChecks(): string[] {
  const failures: string[] = []
  if (configurationEntityKeys.length !== 7) failures.push('configuration hub must expose exactly seven entities')
  if (!configurationEntityKeys.includes('simulator-configurations')) failures.push('Simulator Configurations must remain a management entity')
  return failures
}

export function resourceLabel(resource: string): string {
  return MANAGEMENT_RESOURCES.find(value => value.key === resource)?.label ?? resource
}

export function textValue(value: unknown): string {
  if (value === null || value === undefined) return ''
  if (Array.isArray(value)) return value.map(item => textValue(item)).filter(Boolean).join(', ')
  if (typeof value === 'object') {
    const raw = (value as Record<string, unknown>).value
    return raw === null || raw === undefined ? '' : String(raw)
  }
  return String(value)
}

export function configurationValidationErrors(resource: string, mode: 'create' | 'edit', body: Record<string, unknown>): Array<{ key: string; label: string; message: string }> {
  const errors: Array<{ key: string; label: string; message: string }> = []
  const required = (key: string, message: string) => { if (!String(body[key] ?? '').trim()) errors.push({ key, label: FIELD_LABELS[key] ?? key, message }) }
  if (['sites', 'areas', 'assets', 'points', 'data-sources'].includes(resource)) required('name', 'Tên là bắt buộc.')
  if (mode === 'create' && (resource === 'areas' || resource === 'data-sources')) required('siteId', 'Vui lòng chọn Địa điểm cha.')
  if (mode === 'create' && resource === 'assets') required('areaId', 'Vui lòng chọn Khu vực cha.')
  if (mode === 'create' && resource === 'points') required('assetId', 'Vui lòng chọn Tài sản cha.')
  if (mode === 'create' && resource === 'source-point-mappings') { required('sourceId', 'Vui lòng chọn Nguồn dữ liệu.'); required('pointId', 'Vui lòng chọn Điểm đo.') }
  if (mode === 'create' && resource === 'simulator-configurations') required('sourceId', 'Vui lòng chọn Nguồn dữ liệu.')
  return errors
}

export function canonicalFormValues(form: Record<string, string>): Record<string, string> {
  const canonical: Record<string, string> = {}
  for (const key of Object.keys(form)) canonical[key] = String(form[key] ?? '').trim()
  return canonical
}

export function configurationFormDirty(form: Record<string, string>, initialForm: Record<string, string>): boolean {
  return JSON.stringify(canonicalFormValues(form)) !== JSON.stringify(canonicalFormValues(initialForm))
}

export function normalizeConfigurationForm(resource: string, mode: 'create' | 'edit', form: Record<string, string>): ConfigurationFormNormalization {
  const body: Record<string, unknown> = {}
  const canonical: Record<string, string> = {}
  const errors: Array<{ key: string; label: string; message: string }> = []
  const numericFields = NUMERIC_FIELDS[resource] ?? []
  const numericKeys = new Set(numericFields.map(field => field.key))
  for (const key of Object.keys(form)) {
    const raw = String(form[key] ?? '').trim()
    canonical[key] = raw
    if (!numericKeys.has(key)) body[key] = raw
  }
  for (const field of numericFields) {
    const raw = canonical[field.key]
    if (raw === '') {
      delete body[field.key]
      continue
    }
    const numeric = Number(raw)
    if (!Number.isFinite(numeric)) { errors.push({ key: field.key, label: field.label, message: `${field.label} phải là một số hợp lệ.` }); continue }
    if (field.kind === 'finite-decimal') { body[field.key] = numeric; continue }
    if (!Number.isInteger(numeric)) { errors.push({ key: field.key, label: field.label, message: `${field.label} phải là một số nguyên hợp lệ.` }); continue }
    if (field.kind === 'unsigned-int') {
      if (numeric < 0) { errors.push({ key: field.key, label: field.label, message: `${field.label} phải là một số nguyên không âm.` }); continue }
      if (numeric > Number.MAX_SAFE_INTEGER) { errors.push({ key: field.key, label: field.label, message: `${field.label} phải nằm trong phạm vi biểu diễn an toàn.` }); continue }
      body[field.key] = numeric
      continue
    }
    if (numeric <= 0) { errors.push({ key: field.key, label: field.label, message: `${field.label} phải là một số nguyên dương.` }); continue }
    body[field.key] = numeric
  }
  if (resource === 'simulator-configurations') {
    const minimum = typeof body.minimumValue === 'number' ? body.minimumValue : undefined
    const maximum = typeof body.maximumValue === 'number' ? body.maximumValue : undefined
    if (typeof minimum === 'number' && typeof maximum === 'number' && minimum > maximum) errors.push({ key: 'minimumValue', label: 'Giá trị nhỏ nhất', message: 'Giá trị nhỏ nhất phải nhỏ hơn hoặc bằng Giá trị lớn nhất.' })
  }
  if (resource === 'points') {
    const expectedInterval = typeof body.expectedIntervalSeconds === 'number' ? body.expectedIntervalSeconds : undefined
    const noDataAfter = typeof body.noDataAfterSeconds === 'number' ? body.noDataAfterSeconds : undefined
    if (typeof expectedInterval === 'number' && typeof noDataAfter === 'number' && noDataAfter <= expectedInterval) errors.push({ key: 'noDataAfterSeconds', label: 'No Data sau (giây)', message: 'No Data sau (giây) phải lớn hơn Chu kỳ (giây).' })
  }
  if (resource === 'source-point-mappings') {
    const fromRaw = canonical.effectiveFromUtc
    const toRaw = canonical.effectiveToUtc
    const fromTime = fromRaw ? new Date(fromRaw).getTime() : Number.NaN
    const toTime = toRaw ? new Date(toRaw).getTime() : Number.NaN
    if (fromRaw && Number.isNaN(fromTime)) errors.push({ key: 'effectiveFromUtc', label: 'Hiệu lực từ', message: 'Hiệu lực từ phải là một ngày giờ hợp lệ.' })
    if (toRaw && Number.isNaN(toTime)) errors.push({ key: 'effectiveToUtc', label: 'Hiệu lực đến', message: 'Hiệu lực đến phải là một ngày giờ hợp lệ.' })
    if (fromRaw && toRaw && !Number.isNaN(fromTime) && !Number.isNaN(toTime) && fromTime > toTime) errors.push({ key: 'effectiveToUtc', label: 'Hiệu lực đến', message: 'Hiệu lực đến phải không sớm hơn Hiệu lực từ.' })
    if (fromRaw) body.effectiveFrom = fromRaw
    if (toRaw) body.effectiveTo = toRaw
    delete body.effectiveFromUtc
    delete body.effectiveToUtc
  }
  errors.push(...configurationValidationErrors(resource, mode, body))
  return { body, canonical, errors }
}

export function canonicalJson(value: unknown): string {
  if (Array.isArray(value)) return `[${value.map(item => canonicalJson(item)).join(',')}]`
  if (value !== null && typeof value === 'object') {
    const record = value as Record<string, unknown>
    return `{${Object.keys(record).sort().map(key => `${JSON.stringify(key)}:${canonicalJson(record[key])}`).join(',')}}`
  }
  if (value === null) return 'null'
  if (typeof value === 'number') return Number.isFinite(value) ? (Object.is(value, -0) ? '0' : String(value)) : 'null'
  return JSON.stringify(value)
}

export function pendingManagementMutationFingerprint(descriptor: PendingManagementMutation): string {
  return canonicalJson({
    resource: descriptor.resource,
    kind: descriptor.kind,
    entityId: descriptor.entityId ?? null,
    expectedVersion: descriptor.expectedVersion ?? null,
    payload: descriptor.payload,
    targetSourceId: descriptor.targetSourceId ?? null,
    draftVersion: descriptor.draftVersion ?? null,
  })
}

export function samePendingManagementMutation(left: PendingManagementMutation, right: PendingManagementMutation): boolean {
  return pendingManagementMutationFingerprint(left) === pendingManagementMutationFingerprint(right)
}

export function managementMutationDisposition(result: { ok: boolean; status: number; errorCode?: string }): 'success' | 'retryable' | 'expired' | 'definitive' {
  if (result.ok) return 'success'
  if (result.status === 401 || result.errorCode === 'expired' || result.errorCode === 'EXPIRED') return 'expired'
  if (result.status === 503 || result.errorCode === 'RUNTIME_FAILURE' || result.errorCode === 'DEPENDENCY_UNAVAILABLE') return 'retryable'
  return 'definitive'
}

export function isRetryableManagementMutationResult(result: { ok: boolean; status: number; errorCode?: string }): boolean {
  return managementMutationDisposition(result) === 'retryable'
}

export function mutationActionLabel(kind: ManagementMutationKind): string {
  return ({ create: 'Tạo mới', update: 'Cập nhật', remove: 'Xóa', lifecycle: 'Chuyển trạng thái', validate: 'Kiểm tra', review: 'Xem xét quan hệ', duplicate: 'Nhân bản', activate: 'Kích hoạt' } as Record<ManagementMutationKind, string>)[kind]
}

export function detailRequestOwner(token: number, resource: string, entityId: string): DetailRequestOwner {
  return { token, resource, entityId }
}

export function detailResponseApplies(owner: DetailRequestOwner | null, response: { token: number; resource: string; entityId: string }): boolean {
  return owner !== null && owner.token === response.token && owner.resource === response.resource && owner.entityId === response.entityId
}

export function duplicateIdentityFromResult(result: { ok: boolean; body?: Record<string, unknown> }): string {
  if (!result.ok) return ''
  const body = result.body ?? {}
  return textValue(body.id ?? body.configurationId)
}

export function simulatorActivationReadiness(item: ManagementItem): { ready: boolean; reason?: string } {
  const id = textValue(item.configurationId ?? item.id)
  const draft = Number(item.draftConfigurationVersion ?? 0)
  const current = Number(item.currentConfigurationVersion ?? 0)
  if (!id || draft <= current) return { ready: false, reason: 'Không có bản nháp để kích hoạt' }
  if (!item.relationshipReviewed || item.relationshipReceiptStale) return { ready: false, reason: 'Cần xem xét quan hệ không hết hạn trên máy chủ trước khi kích hoạt' }
  if (!item.validationRecorded || item.validationReceiptStale) return { ready: false, reason: 'Cần kiểm tra bản nháp không hết hạn trước khi kích hoạt' }
  return { ready: true }
}

export function configurationSortKeys(resource: string): string[] {
  return SORT_KEYS[resource] ?? []
}

export function configurationDefaultSort(resource: string): { key: string; direction: SortDirection } {
  return DEFAULT_SORT[resource] ?? { key: 'name', direction: 'ascending' }
}

export function effectiveConfigurationSort(resource: string, requested: { key: string; direction: SortDirection }): { key: string; direction: SortDirection } {
  const direction: SortDirection = requested.direction === 'descending' ? 'descending' : 'ascending'
  if (configurationSortKeys(resource).includes(requested.key)) return { key: requested.key, direction }
  return configurationDefaultSort(resource)
}

export function detailFieldsFor(resource: string): Array<{ key: string; label: string }> {
  return DETAIL_FIELDS[resource] ?? []
}

export function safeConfigurationDate(value: unknown): string {
  const raw = textValue(value)
  if (!raw) return '—'
  const parsed = new Date(raw)
  if (Number.isNaN(parsed.getTime())) return 'Không hợp lệ'
  return parsed.toLocaleString('vi-VN')
}

export function sortManagementItems(items: readonly ManagementItem[], key: string, direction: SortDirection): ManagementItem[] {
  const sign = direction === 'ascending' ? 1 : -1
  return [...items].sort((left, right) => {
    const a = textValue(left[key]); const b = textValue(right[key])
    const numberA = Number(a); const numberB = Number(b)
    const comparison = a !== '' && b !== '' && Number.isFinite(numberA) && Number.isFinite(numberB)
      ? numberA - numberB : a.localeCompare(b, 'vi', { numeric: true, sensitivity: 'base' })
    return comparison * sign
  })
}

export function configurationLifecyclePresentation(status: string): { label: string; cue: string; tone: 'success' | 'warning' | 'neutral' | 'danger' } {
  if (status === 'Active') return { label: 'Đang hoạt động', cue: '●', tone: 'success' }
  if (status === 'Draft') return { label: 'Bản nháp', cue: '◌', tone: 'warning' }
  if (status === 'Suspended' || status === 'Inactive') return { label: status === 'Suspended' ? 'Tạm dừng' : 'Không hoạt động', cue: '!', tone: 'warning' }
  if (status === 'Decommissioned' || status === 'Superseded') return { label: status === 'Decommissioned' ? 'Ngừng sử dụng' : 'Đã thay thế', cue: '×', tone: 'neutral' }
  return { label: status || 'Chưa xác định', cue: '?', tone: 'neutral' }
}

export function managementStateMessage(state: ManagementState, resource: string, emptyMessage: string): { title: string; message: string; tone: 'loading' | 'empty' | 'forbidden' | 'error' | 'conflict' | 'blocked' | 'info' } | null {
  if (state === 'ready') return null
  if (state === 'loading') return { title: 'Đang tải', message: `Đang tải ${resourceLabel(resource)}…`, tone: 'loading' }
  if (state === 'forbidden') return { title: 'Không được phép', message: `Bạn không có quyền xem ${resourceLabel(resource)} trong phạm vi này.`, tone: 'forbidden' }
  if (state === 'expired') return { title: 'Phiên đã hết hạn', message: 'Vui lòng đăng nhập lại để tiếp tục.', tone: 'error' }
  if (state === 'conflict') return { title: 'Có xung đột', message: 'Dữ liệu đã thay đổi bởi người khác. Hãy tải lại trước khi lưu.', tone: 'conflict' }
  if (state === 'dependency' || state === 'runtime') return { title: 'Dịch vụ chưa sẵn sàng', message: 'Không hiển thị dữ liệu dự phòng. Hãy thử lại sau.', tone: 'blocked' }
  if (state === 'validation') return { title: 'Cần kiểm tra', message: 'Dữ liệu chưa hợp lệ; hãy sửa trường được đánh dấu.', tone: 'error' }
  if (state === 'not-found') return { title: 'Không tìm thấy', message: 'Thực thể không còn trong phạm vi được cấp quyền.', tone: 'info' }
  return { title: 'Chưa có dữ liệu', message: emptyMessage, tone: 'empty' }
}

export function lifecycleActionsFor(resource: string, status: string): string[] {
  if (resource === 'simulator-configurations') return []
  if (status === 'Draft') return resource === 'data-sources' ? ['activate', 'decommission'] : ['activate']
  if (status === 'Active') return resource === 'data-sources' ? ['suspend', 'decommission'] : resource === 'source-point-mappings' ? ['inactivate', 'supersede'] : ['deactivate']
  if (status === 'Suspended') return resource === 'data-sources' ? ['activate', 'decommission'] : []
  if (status === 'Inactive' && resource === 'source-point-mappings') return ['supersede']
  return []
}

export function actionLabelFor(action: string): string {
  return ({ activate: 'Kích hoạt', deactivate: 'Tắt hoạt động', decommission: 'Ngừng sử dụng', suspend: 'Tạm dừng', inactivate: 'Đặt không hoạt động', supersede: 'Thay thế' } as Record<string, string>)[action] ?? action
}

export function canDeleteResource(resource: string, status: string): boolean {
  return (resource === 'data-sources' || resource === 'source-point-mappings') && status === 'Draft'
}

export function statusesForResource(resource: string): string[] {
  if (resource === 'sites' || resource === 'areas') return ['Draft', 'Active', 'Inactive']
  if (resource === 'assets' || resource === 'points') return ['Draft', 'Active', 'Inactive', 'Decommissioned']
  if (resource === 'data-sources') return ['Draft', 'Active', 'Suspended', 'Decommissioned']
  if (resource === 'source-point-mappings') return ['Draft', 'Active', 'Inactive', 'Superseded']
  return []
}

export function DuplicateButton({ item, busyItem, mutationPending, onDuplicate }: { item: ManagementItem; busyItem?: string | null; mutationPending?: boolean; onDuplicate: (item: ManagementItem) => void }) {
  const id = textValue(item.id ?? item.configurationId)
  return <button className="button button-secondary" type="button" disabled={!id || busyItem === id || mutationPending} title={mutationPending ? 'Đang xử lý yêu cầu; hãy chờ hoàn tất.' : undefined} onClick={() => onDuplicate(item)}>{busyItem === id ? 'Đang nhân bản…' : 'Nhân bản'}</button>
}

export function ActivateVersionButton({ item, busyItem, mutationPending, onActivate, readyForActivation = true }: { item: ManagementItem; busyItem?: string | null; mutationPending?: boolean; onActivate: (item: ManagementItem) => void; readyForActivation?: boolean }) {
  const id = textValue(item.configurationId)
  const current = Number(item.currentConfigurationVersion ?? 0)
  const draft = Number(item.draftConfigurationVersion ?? 0)
  const hasDraft = draft > current
  return <button className="button button-primary" type="button" disabled={!id || !hasDraft || busyItem === id || mutationPending || !readyForActivation} title={mutationPending ? 'Đang xử lý yêu cầu; hãy chờ hoàn tất.' : !hasDraft ? 'Không có bản nháp để kích hoạt' : readyForActivation ? `Kích hoạt bản ${draft}` : 'Cần xem xét quan hệ và kiểm tra trước khi kích hoạt'} onClick={() => onActivate(item)}>{busyItem === id ? 'Đang kích hoạt…' : 'Kích hoạt'}</button>
}

export function ManagementActionButton({ label, onClick, disabled, tone = 'secondary', title }: { label: string; onClick: () => void; disabled?: boolean; tone?: 'primary' | 'secondary' | 'quiet' | 'danger'; title?: string }) {
  return <button className={`button button-${tone}`} type="button" disabled={disabled} title={title} onClick={onClick}>{label}</button>
}
