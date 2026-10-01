import { SIGN_IN_PROVIDERS, signInUrl, useApiClient } from '../api';
import { useT } from '../i18n';

/**
 * One link per provider (docs/SPEC.md §6.1), each returning the browser to `returnPath` once
 * signed in. Links, not buttons: the provider's pages are a navigation, and the server keeps the
 * state and PKCE of the round trip. A path that is not local is never sent (`signInUrl`).
 */
export function SignInLinks({ returnPath }: { returnPath: string }) {
  const client = useApiClient();
  const t = useT();

  return (
    <ul className="sign-in-links">
      {SIGN_IN_PROVIDERS.map((provider) => (
        <li key={provider}>
          <a className="button" href={signInUrl(client, provider, returnPath)}>
            {t(`session.signInWith.${provider}`)}
          </a>
        </li>
      ))}
    </ul>
  );
}
