import { FeedbackBanner } from './FeedbackBanner'

export function LoadingState({ message = 'Đang tải dữ liệu được cấp quyền…' }: { message?: string }) {
  return <FeedbackBanner title="Đang tải" message={message} live />
}
