import { Link, useParams } from 'react-router';
import { getProblem, useApiClient } from '../api';
import { useApiResource } from '../hooks/useApiResource';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';

/**
 * One problem, wired but not designed — replaced by
 * [#27](https://github.com/shoraLBRT/ritocode/issues/27) along with {@link ProblemsPage}.
 *
 * The `problem_not_found` branch is the shell's proof that a client can branch on `code` rather
 * than on a status: 404 also covers a resource the caller may not see, per ADR 0003, so the
 * code is the only member that says which of the two this was.
 */
export function ProblemDetailPage() {
  const client = useApiClient();
  const { slug = '' } = useParams<{ slug: string }>();
  const { state, reload } = useApiResource((signal) => getProblem(client, slug, signal), [client, slug]);

  if (state.status === 'loading') {
    return <LoadingState label="Loading problem…" />;
  }

  if (state.status === 'error') {
    return state.error.code === 'problem_not_found' ? (
      <section className="page">
        <h1>No such problem</h1>
        <p>
          There is no published problem with the slug <code>{slug}</code>.
        </p>
        <Link to="/problems">Back to the catalog</Link>
      </section>
    ) : (
      <ErrorState error={state.error} onRetry={reload} />
    );
  }

  const problem = state.data;

  return (
    <section className="page">
      <h1>{problem.title}</h1>
      <p className="page__note">
        <span className={`badge badge--${problem.difficulty}`}>{problem.difficulty}</span> version{' '}
        {problem.version} · published {new Date(problem.publishedAt).toISOString().slice(0, 10)}
      </p>
      {problem.tags.length > 0 && <p className="page__note">{problem.tags.join(', ')}</p>}
      {/* Rendered as text, not Markdown. The renderer arrives with the designed screen in #27,
          and rendering untrusted content is a decision that deserves to be made there. */}
      <pre className="problem-detail__description">{problem.description}</pre>
    </section>
  );
}
