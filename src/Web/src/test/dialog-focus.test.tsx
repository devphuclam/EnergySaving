import { ConfirmDialog } from '../components/dialogs/ConfirmDialog'
import { ReasonDialog, reasonConfirmationDecision, reasonRequiredValidation } from '../components/dialogs/ReasonDialog'

export function runDialogFocusChecks(): string[] {
  const failures: string[] = []
  if (typeof ConfirmDialog !== 'function' || typeof ReasonDialog !== 'function')
    failures.push('dialog primitives must be importable')
  if (reasonRequiredValidation('', true, true) === undefined)
    failures.push('an attempted empty required reason must produce a validation error')
  if (reasonRequiredValidation('reason', true, true) !== undefined)
    failures.push('a provided reason must pass required validation')
  if (reasonRequiredValidation('', true, false) !== undefined)
    failures.push('an unsubmitted empty reason must not be reported as invalid before the attempt')
  if (reasonRequiredValidation('', false, true) !== undefined)
    failures.push('an empty reason must be accepted when the reason is not required')
  const rejected = reasonConfirmationDecision('', true)
  if (rejected.valid || rejected.error !== 'Lý do là bắt buộc.')
    failures.push('an empty required reason must be rejected on the very first confirm attempt')
  if (reasonConfirmationDecision('   ', true).valid)
    failures.push('a whitespace-only required reason must be rejected on the confirm attempt')
  const confirmed = reasonConfirmationDecision('  lý do  ', true)
  if (!confirmed.valid || confirmed.value !== 'lý do')
    failures.push('a confirmed reason must be trimmed before submission')
  if (!reasonConfirmationDecision('', false).valid)
    failures.push('an empty reason must confirm when the reason is not required')
  return failures
}
