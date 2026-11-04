import { createContext } from 'react';

/**
 * The centre of the application's header, where a page may put its own controls — the task
 * screen puts its steps there (docs/DESIGN.md). Null outside the layout, or before it has mounted.
 */
export const HeaderSlotContext = createContext<HTMLElement | null>(null);
