import { listModules, useApiClient } from '../api';
import { useApiResource } from '../hooks/useApiResource';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';

/**
 * The landing route, and the shell's own proof of life: it calls `/meta/modules` through the
 * client and renders whichever of the three states comes back. That endpoint is diagnostics
 * rather than product, which is the point — it exercises the client, the loading view and the
 * failure view without pre-empting the catalog screens in #27.
 */
export function HomePage() {
  const client = useApiClient();
  const { state, reload } = useApiResource((signal) => listModules(client, signal), [client]);

  return (
    <section className="page">
      <h1>Ritocode</h1>
      <p className="page__lead">
        Solve tasks by improving existing code. Solutions are graded by deterministic validators,
        not by opinion.
      </p>

      <h2>Backend</h2>
      <p className="page__note">
        Talking to <code>{client.baseUrl}</code>.
      </p>

      {state.status === 'loading' && <LoadingState label="Checking the API…" />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && (
        <ul className="module-list">
          {state.data.map((module) => (
            <li key={module.name} className="module-list__item">
              <span className="module-list__name">{module.name}</span>
              <code className="module-list__prefix">{module.routePrefix}</code>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
