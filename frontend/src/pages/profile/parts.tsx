import type { CSSProperties, ReactNode } from 'react';

/** A card of the profile: a titled section, staggered into place by its `index` (profile.css). */
export function ProfileCard({
  id,
  title,
  index,
  className,
  aside,
  children,
}: {
  id: string;
  title: ReactNode;
  index: number;
  className: string;
  aside?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className={`rc-profile__card ${className}`} aria-labelledby={id} style={{ '--i': index } as CSSProperties}>
      <div className="rc-profile__card-head">
        <h2 id={id} className="rc-profile__card-title">
          {title}
        </h2>
        {aside}
      </div>
      {children}
    </section>
  );
}

/** Skeleton blocks of a card's final shape, with the words a screen reader hears meanwhile. */
export function CardLoading({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="rc-profile__loading" aria-busy="true">
      <span className="visually-hidden" role="status">
        {label}
      </span>
      {children}
    </div>
  );
}

