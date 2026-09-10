/**
 * The one loading indicator. It is a live region rather than a spinner alone, so a screen
 * reader is told the page is working instead of being handed silence.
 */
export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="state state--loading" role="status" aria-live="polite">
      <span className="state__spinner" aria-hidden="true" />
      <span>{label}</span>
    </div>
  );
}
