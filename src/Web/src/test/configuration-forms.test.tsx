import { Field } from '../components/forms/Field'
import { FieldErrorSummary, fieldErrorSummaryFocusTarget, firstErrorFieldId, type FieldError } from '../components/forms/FieldErrorSummary'
import { FormSection } from '../components/forms/FormSection'
import { UnsavedChangesGuard, clearUnsavedChange, hasUnsavedChanges, registerUnsavedChange } from '../components/forms/UnsavedChangesGuard'
import { configurationFormDirty, configurationValidationErrors, normalizeConfigurationForm } from '../features/configuration/ConfigurationManagementComponents'

export const CONFIGURATION_FORM_EXPECTED_FAILURES = 0

export function runConfigurationFormChecks(): string[] {
  const failures: string[] = []
  if ([Field, FieldErrorSummary, FormSection, UnsavedChangesGuard].some(component => typeof component !== 'function')) failures.push('form primitives must be importable')
  const errors: readonly FieldError[] = [{ id: 'first-field', label: 'Đầu tiên', message: 'Sai' }, { id: 'second-field', label: 'Thứ hai', message: 'Sai' }]
  if (firstErrorFieldId(errors) !== 'first-field') failures.push('error focus must target the first invalid field')
  if (firstErrorFieldId([]) !== undefined) failures.push('no invalid field must not produce a focus target')
  if (fieldErrorSummaryFocusTarget(errors) !== 'first-field') failures.push('a failed submit must focus the first invalid field')
  if (fieldErrorSummaryFocusTarget([{ id: '', label: 'x', message: 'y' }]) !== 'summary') failures.push('a failed submit without a field id must fall back to the summary')
  if (fieldErrorSummaryFocusTarget([]) !== undefined) failures.push('a submit with no invalid field must not force focus')
  registerUnsavedChange('check-field', 'Có thay đổi chưa lưu.')
  if (!hasUnsavedChanges()) failures.push('a registered unsaved change must block shell navigation')
  clearUnsavedChange('check-field')
  if (hasUnsavedChanges()) failures.push('a cleared unsaved change must release shell navigation')
  if (configurationValidationErrors('sites', 'create', {})[0]?.key !== 'name') failures.push('first invalid configuration field must be deterministic')

  const requiredName = normalizeConfigurationForm('sites', 'create', { name: '  ' })
  if (requiredName.errors[0]?.key !== 'name') failures.push('a whitespace-only name must be rejected as required')
  const zeroInterval = normalizeConfigurationForm('points', 'edit', { name: 'P', expectedIntervalSeconds: '0' })
  if (zeroInterval.errors.some(error => error.key === 'expectedIntervalSeconds')) failures.push('zero must be a valid numeric input, never dropped as falsy')
  if (zeroInterval.body.expectedIntervalSeconds !== 0) failures.push('zero must be transmitted as the number zero')
  if (normalizeConfigurationForm('points', 'edit', { expectedIntervalSeconds: 'Infinity' }).errors[0]?.key !== 'expectedIntervalSeconds') failures.push('Infinity must be rejected')
  if (normalizeConfigurationForm('simulator-configurations', 'edit', { minimumValue: '50', maximumValue: '20' }).errors.some(error => error.key === 'minimumValue')) failures.push('minimum above maximum must be rejected')
  if (normalizeConfigurationForm('points', 'edit', { expectedIntervalSeconds: '30', noDataAfterSeconds: '' }).errors.length) failures.push('an empty optional numeric must not block a valid submit')

  if (configurationFormDirty({ name: 'A' }, { name: 'A' })) failures.push('unchanged forms must not be dirty')
  if (configurationFormDirty({ name: 'A' }, { name: 'A ' })) failures.push('a trailing space restored to the original must not count as dirty (canonical comparison)')
  if (!configurationFormDirty({ name: 'A' }, { name: 'B' })) failures.push('changed forms must be dirty')
  if (!configurationFormDirty({ name: 'A', expectedIntervalSeconds: 'invalid-text' }, { name: 'A', expectedIntervalSeconds: '60' })) failures.push('invalid text must keep the form dirty until restored')

  return failures
}

export function configurationFormFailures(): string[] { return runConfigurationFormChecks() }
