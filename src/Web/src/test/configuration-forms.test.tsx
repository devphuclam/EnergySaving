import { Field } from '../components/forms/Field'
import { FieldErrorSummary, firstErrorFieldId, type FieldError } from '../components/forms/FieldErrorSummary'
import { FormSection } from '../components/forms/FormSection'
import {
  UnsavedChangesGuard,
  clearUnsavedChange,
  hasUnsavedChanges,
  registerUnsavedChange,
} from '../components/forms/UnsavedChangesGuard'

export function runConfigurationFormChecks(): string[] {
  const failures: string[] = []
  if ([Field, FieldErrorSummary, FormSection, UnsavedChangesGuard].some(component => typeof component !== 'function'))
    failures.push('form primitives must be importable')
  const errors: readonly FieldError[] = [
    { id: 'first-field', label: 'Đầu tiên', message: 'Sai' },
    { id: 'second-field', label: 'Thứ hai', message: 'Sai' },
  ]
  if (firstErrorFieldId(errors) !== 'first-field') failures.push('error focus must target the first invalid field')
  if (firstErrorFieldId([]) !== undefined) failures.push('no invalid field must not produce a focus target')
  registerUnsavedChange('check-field', 'Có thay đổi chưa lưu.')
  if (!hasUnsavedChanges()) failures.push('a registered unsaved change must block shell navigation')
  clearUnsavedChange('check-field')
  if (hasUnsavedChanges()) failures.push('a cleared unsaved change must release shell navigation')
  return failures
}
