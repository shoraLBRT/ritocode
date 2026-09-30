import { describe, expect, it } from 'vitest';
import { DEFAULT_DEMO_TASK, resolveSiteConfig } from './SiteConfig';

describe('resolveSiteConfig', () => {
  it('takes the configured demo task', () => {
    expect(resolveSiteConfig({ VITE_DEMO_TASK: ' invoice-mailer-monthly ' }).demoTask).toBe('invoice-mailer-monthly');
  });

  it.each([[undefined], [''], ['   ']])('falls back to the default demo task for %p', (value) => {
    expect(resolveSiteConfig({ VITE_DEMO_TASK: value }).demoTask).toBe(DEFAULT_DEMO_TASK);
  });
});
