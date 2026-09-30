import { Link } from 'react-router';
import { getProgress, useApiClient } from '../api';
import type { CardProgress, ClassProgress, Progress } from '../api';
import { EmptyState } from '../components/EmptyState';
import { ErrorState } from '../components/ErrorState';
import { LoadingState } from '../components/LoadingState';
import { useApiResource } from '../hooks/useApiResource';
import { useT } from '../i18n';

/**
 * `/progress` — the learner's progress (docs/SPEC.md §4.7), from first attempts only: the six
 * classes with their counts, then a table of the cards met or picked, grouped by class. No XP, no
 * levels. The API names the classes and cards, retired ones included, so the page reads nothing else.
 *
 * Signed in only: it sits under `RequireSignIn`.
 */
export function ProgressPage() {
  const client = useApiClient();
  const t = useT();
  const { state, reload } = useApiResource((signal) => getProgress(client, signal), [client]);

  return (
    <section className="page">
      <h1>{t('progress.title')}</h1>
      <p className="page__lead">{t('progress.lead')}</p>

      {state.status === 'loading' && <LoadingState label={t('progress.loading')} />}
      {state.status === 'error' && <ErrorState error={state.error} onRetry={reload} />}
      {state.status === 'success' && state.data.tasks === 0 && (
        <EmptyState>
          <p>{t('progress.empty')}</p>
          <Link to="/tasks">{t('progress.toTasks')}</Link>
        </EmptyState>
      )}
      {state.status === 'success' && state.data.tasks > 0 && <ProgressView progress={state.data} />}
    </section>
  );
}

function ProgressView({ progress }: { progress: Progress }) {
  const t = useT();

  return (
    <>
      <p>{t('progress.tasks', { count: progress.tasks })}</p>

      <h2 id="progress-classes">{t('progress.classes')}</h2>
      <ul className="progress-classes" aria-labelledby="progress-classes">
        {progress.classes.map((item) => (
          <ClassItem key={item.class} item={item} />
        ))}
      </ul>

      {progress.cards.length > 0 && <CardTable progress={progress} />}
    </>
  );
}

function ClassItem({ item }: { item: ClassProgress }) {
  const t = useT();

  return (
    <li className="progress-classes__item">
      <span className="progress-classes__name">{item.name}</span>
      {item.met === 0 ? (
        <span className="progress-classes__counts progress-classes__counts--none">{t('progress.classNotMet')}</span>
      ) : (
        <span className="progress-classes__counts">
          {t('progress.classCounts', { found: item.found, met: item.met, treatedRight: item.treatedRight })}
        </span>
      )}
    </li>
  );
}

/**
 * The cards, one `tbody` per class in the taxonomy's order, the class named in a header row. A
 * card Content no longer knows comes last, under a group of its own, and links nowhere.
 */
function CardTable({ progress }: { progress: Progress }) {
  const t = useT();

  const groups = [
    ...progress.classes.map((item) => ({ key: item.class, name: item.name, cards: progress.cards.filter((card) => card.class === item.class) })),
    { key: '', name: t('progress.noClass'), cards: progress.cards.filter((card) => card.class === null) },
  ].filter((group) => group.cards.length > 0);

  return (
    <>
      <h2 id="progress-cards">{t('progress.cards')}</h2>
      <p className="page__note">{t('progress.cardsHint')}</p>
      {/* A region that scrolls sideways on a phone, so the page itself never does. */}
      <div className="table-scroll" role="region" aria-labelledby="progress-cards" tabIndex={0}>
        <table className="progress-table">
          <thead>
            <tr>
              <th scope="col">{t('progress.card')}</th>
              <th scope="col">{t('progress.met')}</th>
              <th scope="col">{t('progress.found')}</th>
              <th scope="col">{t('progress.missed')}</th>
              <th scope="col">{t('progress.pickedWhenAbsent')}</th>
              <th scope="col">{t('progress.treatedRight')}</th>
            </tr>
          </thead>
          {groups.map((group) => (
            <tbody key={group.key}>
              <tr className="progress-table__group">
                <th scope="colgroup" colSpan={6}>
                  {group.name}
                </th>
              </tr>
              {group.cards.map((card) => (
                <CardRow key={card.card} card={card} />
              ))}
            </tbody>
          ))}
        </table>
      </div>
    </>
  );
}

function CardRow({ card }: { card: CardProgress }) {
  return (
    <tr>
      <th scope="row">{card.class === null ? card.name : <Link to={`/problems#${card.card}`}>{card.name}</Link>}</th>
      <td>{card.met}</td>
      <td>{card.found}</td>
      <td>{card.missed}</td>
      <td>{card.pickedWhenAbsent}</td>
      <td>{card.treatedRight}</td>
    </tr>
  );
}
