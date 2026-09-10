import { useSearchParams } from 'react-router';
import { Link } from 'react-router';
import { listProblems, useApiClient } from '../api';
import { useApiResource } from '../hooks/useApiResource';
import { EmptyState } from '../components/EmptyState';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';

/**
 * The catalog list, wired but not designed.
 *
 * This is the shell's demonstration that the page envelope, the query parameters and the error
 * body all arrive intact — not the catalog screen, which is
 * [#27](https://github.com/shoraLBRT/ritocode/issues/27) in stage 6 and replaces this file.
 * The page number lives in the URL rather than in component state so a page is linkable, which
 * is the reason ADR 0003 chose offset pagination over cursors.
 */
export function ProblemsPage() {
  const client = useApiClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const page = readPage(searchParams.get('page'));

  const { state, reload } = useApiResource(
    (signal) => listProblems(client, { page }, signal),
    [client, page],
  );

  function goToPage(next: number) {
    setSearchParams({ page: String(next) });
  }

  return (
    <section className="page">
      <h1>Problems</h1>

      {state.status === 'loading' && <LoadingState label="Loading problems…" />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}

      {state.status === 'success' && (
        <>
          {state.data.items.length === 0 ? (
            <EmptyState>
              No published problems yet. A development host seeds the reference package on startup.
            </EmptyState>
          ) : (
            <ul className="problem-list">
              {state.data.items.map((problem) => (
                <li key={problem.id} className="problem-list__item">
                  <Link to={`/problems/${problem.slug}`}>{problem.title}</Link>
                  <span className={`badge badge--${problem.difficulty}`}>{problem.difficulty}</span>
                  <span className="problem-list__tags">{problem.tags.join(', ')}</span>
                </li>
              ))}
            </ul>
          )}

          <nav className="pager" aria-label="Pagination">
            <button
              type="button"
              className="button"
              disabled={!state.data.hasPreviousPage}
              onClick={() => { goToPage(state.data.pageNumber - 1); }}
            >
              Previous
            </button>
            <span>
              Page {state.data.pageNumber} of {Math.max(state.data.totalPages, 1)}
            </span>
            <button
              type="button"
              className="button"
              disabled={!state.data.hasNextPage}
              onClick={() => { goToPage(state.data.pageNumber + 1); }}
            >
              Next
            </button>
          </nav>
        </>
      )}
    </section>
  );
}

/**
 * A page number the API would accept, or 1.
 *
 * Junk in the query string is dropped here rather than forwarded, so a hand-edited URL renders
 * page one instead of a validation error the reader cannot act on. Out-of-range numbers a user
 * could plausibly have meant are still sent, because the API rejecting them is information.
 */
function readPage(raw: string | null): number {
  if (raw === null) {
    return 1;
  }

  const parsed = Number.parseInt(raw, 10);
  return Number.isInteger(parsed) && parsed >= 1 ? parsed : 1;
}
