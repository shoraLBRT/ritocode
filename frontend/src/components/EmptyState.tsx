import type { ReactNode } from 'react';

export type EmptyIcon = 'search' | 'tasks' | 'inbox' | 'flag';

/**
 * Says a list is genuinely empty, so an empty page is never mistaken for one still loading: a
 * mark, a title, a line and one action (a link or a button), centred in its panel. Called with
 * only children it is the old one-line form.
 */
export function EmptyState({
  title,
  children,
  action,
  icon,
}: {
  title?: string;
  children?: ReactNode;
  action?: ReactNode;
  icon?: EmptyIcon;
}) {
  return (
    <div className={title === undefined ? 'state state--empty' : 'rc-empty'} role="status">
      {icon !== undefined && (
        <span className="rc-disc" aria-hidden="true">
          <Glyph icon={icon} />
        </span>
      )}
      {title !== undefined && <p className="rc-empty__title">{title}</p>}
      {children !== undefined && (title === undefined ? children : <p className="rc-empty__text">{children}</p>)}
      {action}
    </div>
  );
}

/** Line marks in `currentColor`, drawn on a 40-unit grid. */
export function Glyph({ icon }: { icon: EmptyIcon | 'warning' }) {
  return (
    <svg viewBox="0 0 40 40" width="40" height="40" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" focusable="false">
      {icon === 'search' && (
        <>
          <circle cx="18" cy="18" r="9" />
          <path d="M25 25l8 8" />
        </>
      )}
      {icon === 'tasks' && (
        <>
          <rect x="8" y="6" width="24" height="28" rx="4" />
          <path d="M14 15l2.5 2.5L21 13M14 25h12" />
        </>
      )}
      {icon === 'inbox' && (
        <>
          <path d="M6 22l4-12h20l4 12v8a3 3 0 0 1-3 3H9a3 3 0 0 1-3-3z" />
          <path d="M6 22h9a5 5 0 0 0 10 0h9" />
        </>
      )}
      {icon === 'flag' && (
        <>
          <path d="M11 34V7" />
          <path d="M11 9h18l-4 6 4 6H11" />
        </>
      )}
      {icon === 'warning' && (
        <>
          <path d="M20 6L5 32h30z" />
          <path d="M20 15v8M20 28v.5" />
        </>
      )}
    </svg>
  );
}
