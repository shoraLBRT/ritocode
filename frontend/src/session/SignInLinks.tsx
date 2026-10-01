import { Link } from 'react-router';
import { noteSignInStarted } from '../analytics';
import { SIGN_IN_PROVIDERS, signInUrl, useApiClient } from '../api';
import { useT } from '../i18n';

/**
 * One link per provider (docs/SPEC.md §6.1), each returning the browser to `returnPath` once
 * signed in. Links, not buttons: the provider's pages are a navigation, and the server keeps the
 * state and PKCE of the round trip. A path that is not local is never sent (`signInUrl`). Following
 * one notes the provider in the tab, so the page that comes back can count a completed sign-in.
 * Below them, the notice SPEC §6.1 asks for: what signing in hands over, linking the privacy policy.
 */
export function SignInLinks({ returnPath }: { returnPath: string }) {
  const client = useApiClient();
  const t = useT();

  return (
    <>
      <ul className="sign-in-links">
        {SIGN_IN_PROVIDERS.map((provider) => (
          <li key={provider}>
            <a
              className="button"
              href={signInUrl(client, provider, returnPath)}
              onClick={() => {
                noteSignInStarted(provider);
              }}
            >
              {t(`session.signInWith.${provider}`)}
            </a>
          </li>
        ))}
      </ul>
      <p className="sign-in-notice">
        {t('session.privacyNotice')} <Link to="/privacy">{t('session.privacyLink')}</Link>
      </p>
    </>
  );
}
