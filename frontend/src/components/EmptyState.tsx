import type { ReactNode } from 'react';

/** Says a list is genuinely empty, so an empty page is never mistaken for one still loading. */
export function EmptyState({ children }: { children: ReactNode }) {
  return (
    <div className="state state--empty" role="status">
      {children}
    </div>
  );
}
