/**
 * The search consoles' ownership checks (#303): Yandex Webmaster's and Google Search Console's meta
 * tags, from build-time values, in the head of every page the build writes (`vite.config.ts` adds them
 * to the shell, which every prerendered page is made from). A console reads the tag from the page's
 * HTML, so it has to be there before any script runs. Absent when the value is unset or blank.
 */
export interface VerificationEnv {
  readonly VITE_YANDEX_VERIFICATION?: string;
  readonly VITE_GOOGLE_SITE_VERIFICATION?: string;
}

export interface VerificationTag {
  readonly name: 'yandex-verification' | 'google-site-verification';
  readonly content: string;
}

// The consoles' codes are letters, digits, `-` and `_`; anything else is a mistake in the variable,
// and failing the build says so instead of shipping a tag that never verifies (or breaks the head).
const CODE = /^[A-Za-z0-9_-]+$/;

export function verificationTags(env: VerificationEnv): VerificationTag[] {
  const tags: VerificationTag[] = [];
  const add = (name: VerificationTag['name'], variable: keyof VerificationEnv) => {
    const content = env[variable]?.trim() ?? '';
    if (content === '') {
      return;
    }
    if (!CODE.test(content)) {
      throw new Error(`${variable} '${content}' is not a verification code (letters, digits, '-' and '_'); copy the content of the console's meta tag, or leave it unset.`);
    }
    tags.push({ name, content });
  };

  add('yandex-verification', 'VITE_YANDEX_VERIFICATION');
  add('google-site-verification', 'VITE_GOOGLE_SITE_VERIFICATION');
  return tags;
}
