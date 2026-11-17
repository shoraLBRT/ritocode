import { useT } from '../../i18n';
import { faceLine, lanes } from './lanes';
import type { Line } from './lanes';

/**
 * The centre of a revealed review (docs/DESIGN.md › The review, variant B of #242): the task's cards
 * in three lanes — found, missed, not in the answer — each lane headed by a mark, a word and a count.
 * A card's face is its name and one conclusion (#256); the whole card opens it in full.
 */
export function ReviewLanes({
  lines,
  name,
  picked,
  label,
  onOpen,
}: {
  lines: readonly Line[];
  name: (slug: string) => string;
  picked: (slug: string) => readonly string[];
  label: (leaf: string) => string;
  onOpen: (slug: string) => void;
}) {
  const t = useT();

  const conclusion = (line: Line) => {
    const face = faceLine(line, picked(line.card));
    switch (face.kind) {
      case 'treated':
        return (
          <p className="lane-card__line lane-card__line--good">
            <span aria-hidden="true">{t('review.lane.treatedMark')}</span>
            {t('review.lane.treated')}
          </p>
        );
      case 'mistreated':
        return (
          <p className="lane-card__line lane-card__line--bad">
            <span aria-hidden="true">{t('review.lane.mistreatedMark')}</span>
            {t('review.lane.mistreated')}
          </p>
        );
      case 'untreated':
        return <p className="lane-card__line">{t('review.lane.untreated')}</p>;
      case 'absent':
        return <p className="lane-card__line">{t('review.lane.absent')}</p>;
      case 'cure':
        return face.leaf === undefined ? null : (
          <p className="lane-card__line">
            {t('review.lane.cure', { leaf: label(face.leaf) })}
            {face.more > 0 && <span className="lane-card__more">{t('review.lane.more', { count: face.more })}</span>}
          </p>
        );
    }
  };

  return (
    <div className="review-lanes" role="group" aria-label={t('review.lanes')}>
      {lanes(lines).map(({ outcome, lines: inLane }, position) => (
        <section
          key={outcome}
          className={`review-lane review-lane--${outcome}`}
          style={{ ['--lane' as string]: position }}
          aria-labelledby={`lane-${outcome}`}
        >
          <h3 id={`lane-${outcome}`} className="review-lane__head">
            <span className={`verdict-badge verdict-badge--${outcome}`}>
              <span aria-hidden="true">{t(`review.${outcome}Mark`)}</span> {t(`review.lane.${outcome}`)}
            </span>
            <span className="review-lane__count">{inLane.length}</span>
          </h3>
          {inLane.length === 0 ? (
            <p className="review-lane__empty">{t('review.lane.empty')}</p>
          ) : (
            inLane.map((line, index) => (
              <article key={line.card} className="lane-card" style={{ ['--i' as string]: index }} aria-labelledby={`lane-card-${line.card}`}>
                <h4 id={`lane-card-${line.card}`} className="lane-card__name">
                  <button
                    type="button"
                    id={`open-${line.card}`}
                    className="lane-card__open"
                    onClick={() => {
                      onOpen(line.card);
                    }}
                  >
                    {name(line.card)}
                  </button>
                </h4>
                {conclusion(line)}
              </article>
            ))
          )}
        </section>
      ))}
    </div>
  );
}
