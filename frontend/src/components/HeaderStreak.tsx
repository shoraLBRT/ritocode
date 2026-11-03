import { useEffect, useState } from 'react';
import { useLocation } from 'react-router';
import { listAttemptsSince, useApiClient } from '../api';
import { useT } from '../i18n';
import { activity, activitySince } from '../pages/home/arena';

/**
 * The signed-in learner's streak in the header (docs/SPEC.md §4.7, DESIGN.md › The arena): days in
 * a row with a submitted attempt, by the home page's own rule. Read again on every page change, so
 * the review that follows a submit already counts it; the last number stays on screen meanwhile,
 * and a failed read leaves it as it was — the header never shows an error of its own.
 */
export function HeaderStreak() {
  const client = useApiClient();
  const t = useT();
  const { pathname } = useLocation();
  const [streak, setStreak] = useState<number | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    const now = new Date();

    listAttemptsSince(client, activitySince(now), controller.signal).then(
      (attempts) => {
        if (!controller.signal.aborted) {
          setStreak(activity(attempts, now).streak);
        }
      },
      () => undefined,
    );

    return () => {
      controller.abort();
    };
  }, [client, pathname]);

  if (streak === null) {
    return null;
  }

  const label = t('header.streakLabel', { count: streak });

  return (
    <span className="rc-streak" role="img" aria-label={label} title={label}>
      <svg viewBox="0 0 24 24" aria-hidden="true" className="icon">
        <path d="M12 2c1 4 5 6 5 11a5 5 0 0 1-10 0c0-3 2-4 2-7 1.5 1 3 2.5 3-4z" />
      </svg>
      {t('header.streak', { count: streak })}
    </span>
  );
}
