import type { ProblemCard } from '../../api';
import { useT } from '../../i18n';
import { faceLeaves, lanes } from './lanes';
import type { LeafMark, Line } from './lanes';
import { classTint } from './tint';

const glyph: Record<LeafMark, string> = { right: '✓', extra: '+', unchosen: '○', plain: '·' };

/**
 * The centre of a revealed review (docs/DESIGN.md › The review, variant B of #242): the task's cards
 * in three lanes — found, missed, not in the answer — each lane headed by a mark, a word and a count,
 * each card with its treatment leaves marked on its face. *Подробнее* opens the card in full.
 */
export function ReviewLanes({
  lines,
  card,
  name,
  classOf,
  className,
  picked,
  label,
  onOpen,
}: {
  lines: readonly Line[];
  card: (slug: string) => ProblemCard | undefined;
  name: (slug: string) => string;
  classOf: (slug: string) => string | undefined;
  className: (slug: string) => string | undefined;
  picked: (slug: string) => readonly string[];
  label: (leaf: string) => string;
  onOpen: (slug: string) => void;
}) {
  const t = useT();

  return (
    <div className="review-lanes" role="group" aria-label={t('review.lanes')}>
      {lanes(lines).map(({ outcome, lines: inLane }) => (
        <section key={outcome} className={`review-lane review-lane--${outcome}`} aria-labelledby={`lane-${outcome}`}>
          <h3 id={`lane-${outcome}`} className="review-lane__head">
            <span className={`verdict-badge verdict-badge--${outcome}`}>
              <span aria-hidden="true">{t(`review.${outcome}Mark`)}</span> {t(`review.lane.${outcome}`)}
            </span>
            <span className="review-lane__count">{inLane.length}</span>
          </h3>
          {inLane.length === 0 ? (
            <p className="review-lane__empty">{t('review.lane.empty')}</p>
          ) : (
            inLane.map((line) => {
              const leaves = faceLeaves(line, picked(line.card));
              const summary = card(line.card)?.summary;
              const heading = line.outcome === 'found' ? 'treatment' : line.outcome === 'missed' ? 'treatmentAuthor' : 'treatmentYours';
              const right = leaves.filter((leaf) => leaf.mark === 'right').length;
              return (
                <article key={line.card} className={`lane-card${classTint(classOf(line.card))}`} aria-labelledby={`lane-card-${line.card}`}>
                  {className(line.card) !== undefined && (
                    <p className="lane-card__band">
                      <span className="lane-card__seal" aria-hidden="true" />
                      {className(line.card)}
                    </p>
                  )}
                  <div className="lane-card__face">
                    <h4 id={`lane-card-${line.card}`} className="lane-card__name">
                      {name(line.card)}
                    </h4>
                    {summary !== undefined && <p className="lane-card__summary">{summary}</p>}
                    <div className="lane-card__treat">
                      <p className="lane-card__treat-head">
                        <span>{t(`review.lane.${heading}`)}</span>
                        {line.outcome === 'found' && <b>{t('review.lane.matched', { count: right })}</b>}
                      </p>
                      {leaves.length === 0 ? (
                        <p className="compare__none">{t('review.noLeaves')}</p>
                      ) : (
                        <ul className="lane-card__leaves">
                          {leaves.map(({ leaf, mark }) => (
                            <li key={leaf} className={`face-leaf face-leaf--${mark}`}>
                              <span className="face-leaf__mark" aria-hidden="true">
                                {glyph[mark]}
                              </span>
                              <span className="visually-hidden">{t('review.leafSaid', { word: t(`review.leaf.${mark}`) })}</span>
                              {label(leaf)}
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                    <p className="lane-card__foot">
                      <span>{t('review.points', { count: line.points })}</span>
                      <button
                        type="button"
                        id={`open-${line.card}`}
                        className="lane-card__open"
                        aria-label={t('review.lane.openCard', { name: name(line.card) })}
                        onClick={() => {
                          onOpen(line.card);
                        }}
                      >
                        {t('review.lane.open')}
                      </button>
                    </p>
                  </div>
                </article>
              );
            })
          )}
        </section>
      ))}
    </div>
  );
}
