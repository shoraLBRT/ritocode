import type { ReactNode } from 'react';
import { useT } from '../i18n';

/**
 * The one loading indicator. It is a live region rather than a picture alone, so a screen reader
 * is told the page is working instead of being handed silence.
 *
 * With a `skeleton` — blocks in the shape of what is loading (`components/skeletons.tsx`) — the
 * label is visually hidden and the blocks show; they appear only after 150 ms, so a quick answer
 * never flashes them. Without one, the label is the whole indicator (the workspace's loading).
 */
export function LoadingState({ label, skeleton, className }: { label?: string; skeleton?: ReactNode; className?: string }) {
  const t = useT();
  const text = label ?? t('state.loading');

  if (skeleton === undefined) {
    return (
      <div className="state state--loading" role="status" aria-live="polite">
        <span className="state__spinner" aria-hidden="true" />
        <span>{text}</span>
      </div>
    );
  }

  return (
    <div className={className ? `rc-loading ${className}` : 'rc-loading'} role="status" aria-live="polite" aria-busy="true">
      <span className="visually-hidden">{text}</span>
      {skeleton}
    </div>
  );
}
