import { createContext, useContext } from 'react';

/** What the layout lends its pages: the number of open signals, and a way to read it again. */
export interface AdminContext {
  /** The open signals, or `null` while the first read is out or when it failed. */
  readonly openSignals: number | null;
  readonly reloadOpenSignals: () => void;
}

export const AdminOutletContext = createContext<AdminContext>({ openSignals: null, reloadOpenSignals: () => undefined });

export function useAdmin(): AdminContext {
  return useContext(AdminOutletContext);
}
