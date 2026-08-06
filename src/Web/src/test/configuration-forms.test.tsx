import { Field } from '../components/forms/Field'
import { FieldErrorSummary, fieldErrorSummaryFocusTarget, firstErrorFieldId, type FieldError } from '../components/forms/FieldErrorSummary'
import { FormSection } from '../components/forms/FormSection'
import { UnsavedChangesGuard, clearUnsavedChange, hasUnsavedChanges, registerUnsavedChange } from '../components/forms/UnsavedChangesGuard'
import { configurationValidationErrors } from '../features/configuration/ConfigurationManagementComponents'

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
  return failures
}

export function configurationFormFailures(): string[] { return runConfigurationFormChecks() }
