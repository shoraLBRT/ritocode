import { describe, expect, it } from 'vitest';
import { verificationTags } from './verification';

describe('verificationTags', () => {
  it('adds no tag when neither console is set', () => {
    expect(verificationTags({})).toEqual([]);
    expect(verificationTags({ VITE_YANDEX_VERIFICATION: '', VITE_GOOGLE_SITE_VERIFICATION: '   ' })).toEqual([]);
  });

  it('adds the tag of each console that is set, trimmed', () => {
    expect(verificationTags({ VITE_YANDEX_VERIFICATION: ' 0123abcd ' })).toEqual([{ name: 'yandex-verification', content: '0123abcd' }]);
    expect(verificationTags({ VITE_GOOGLE_SITE_VERIFICATION: 'Ab-c_9' })).toEqual([{ name: 'google-site-verification', content: 'Ab-c_9' }]);
    expect(verificationTags({ VITE_YANDEX_VERIFICATION: 'y1', VITE_GOOGLE_SITE_VERIFICATION: 'g1' })).toEqual([
      { name: 'yandex-verification', content: 'y1' },
      { name: 'google-site-verification', content: 'g1' },
    ]);
  });

  it('fails the build on a value that is not a code, rather than shipping a broken tag', () => {
    expect(() => verificationTags({ VITE_YANDEX_VERIFICATION: '<meta name="yandex-verification" content="x" />' })).toThrow(/VITE_YANDEX_VERIFICATION/);
    expect(() => verificationTags({ VITE_GOOGLE_SITE_VERIFICATION: 'a"b' })).toThrow(/VITE_GOOGLE_SITE_VERIFICATION/);
  });
});
