import { useSyncExternalStore } from 'react';
import { useApiClient } from '../api';
import { useT } from '../i18n';

/**
 * One sentence above the page while the API cannot be reached (#298): the last request failed with
 * no answer, or the edge answered for it with its 503 (deploy/Caddyfile). The panels below then
 * say only that the data did not load (`ErrorState`), so an outage is explained once rather than by
 * every card. It goes away by itself with the next request the API answers; *Обновить* reloads the
 * page, which asks again. Rendered to a string it is absent: the prerendered pages know no outage.
 */
export function ApiUnavailableBanner() {
  const t = useT();
  const availability = useApiClient().availability;
  const reachable = useSyncExternalStore(availability.subscribe, availability.getSnapshot, () => true);

  if (reachable) {
    return null;
  }

  return (
    <div className="notice notice--error" role="alert">
      <p>{t('state.unavailableBanner')}</p>
      <button
        type="button"
        className="button"
        onClick={() => {
          window.location.reload();
        }}
      >
        {t('state.reload')}
      </button>
    </div>
  );
}
