import { useEffect, type ReactNode } from 'react'

export function UnsavedChangesGuard({ when, message = 'Bạn có thay đổi chưa lưu. Hãy lưu hoặc hủy trước khi rời trang.', children }: { when: boolean; message?: string; children?: ReactNode }) {
  useEffect(() => {
    if (!when) return
    const handleBeforeUnload = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = message }
    window.addEventListener('beforeunload', handleBeforeUnload)
    return () => window.removeEventListener('beforeunload', handleBeforeUnload)
  }, [message, when])
  return <>{when && <p className="unsaved-warning" role="status"><span aria-hidden="true">!</span>{message}</p>}{children}</>
}
