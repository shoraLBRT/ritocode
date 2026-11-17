/**
 * Opening the feedback window from elsewhere (the footer's *Обратная связь*): the widget listens for
 * this event on `window`, so the footer needs no reference to it.
 */
export const FEEDBACK_OPEN_EVENT = 'ritocode:feedback-open';

export function requestFeedback(): void {
  window.dispatchEvent(new Event(FEEDBACK_OPEN_EVENT));
}
