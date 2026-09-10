import { describe, expect, it } from 'vitest';
import { DEFAULT_API_BASE_URL, resolveApiBaseUrl } from './config';

describe('resolveApiBaseUrl', () => {
  it('uses the configured value', () => {
    expect(resolveApiBaseUrl({ VITE_API_BASE_URL: 'https://api.example.com/api/v1' })).toBe(
      'https://api.example.com/api/v1',
    );
  });

  it.each([[undefined], [''], ['   ']])('falls back to the local default for %p', (value) => {
    expect(resolveApiBaseUrl({ VITE_API_BASE_URL: value })).toBe(DEFAULT_API_BASE_URL);
  });
});
