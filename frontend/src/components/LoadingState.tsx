import { useT } from '../i18n';

/**
 * The one loading indicator. It is a live region rather than a spinner alone, so a screen
 * reader is told the page is working instead of being handed silence.
 */
export function LoadingState({ label }: { label?: string }) {
  const t = useT();

  return (
    <div className="state state--loading" role="status" aria-live="polite">
      <span className="state__spinner" aria-hidden="true" />
      <span>{label ?? t('state.loading')}</span>
    </div>
  );
}
