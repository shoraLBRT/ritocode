import { createContext, useContext } from 'react';

/**
 * What the site is configured with beyond the API: build-time values, read from the environment in
 * `main.tsx` alone and passed down, as the API client is.
 */
export interface SiteConfig {
  /** The easy task the landing page offers as a demo (docs/SPEC.md §4.1). */
  readonly demoTask: string;
}

/** The one easy task the content holds today. */
export const DEFAULT_DEMO_TASK = 'flower-shop-daily-revenue';

export function resolveSiteConfig(env: Readonly<Record<string, string | undefined>>): SiteConfig {
  const demoTask = env.VITE_DEMO_TASK?.trim();
  return { demoTask: demoTask === undefined || demoTask === '' ? DEFAULT_DEMO_TASK : demoTask };
}

export const SiteConfigContext = createContext<SiteConfig | null>(null);

export function useSiteConfig(): SiteConfig {
  const config = useContext(SiteConfigContext);

  if (config === null) {
    throw new Error('useSiteConfig was called outside a <SiteConfigContext>.');
  }

  return config;
}
