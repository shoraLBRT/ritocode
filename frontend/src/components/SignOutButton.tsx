import { useState } from 'react';
import { useT } from '../i18n';

/** *Выйти* with its own pending state and failure message; the avatar's menu and the phone menu use it. */
export function SignOutButton({ className, signOut }: { className: string; signOut: () => Promise<void> }) {
  const t = useT();
  const [signingOut, setSigningOut] = useState(false);
  const [failed, setFailed] = useState(false);

  return (
    <>
      <button
        type="button"
        className={className}
        disabled={signingOut}
        onClick={() => {
          setSigningOut(true);
          setFailed(false);
          signOut().then(
            () => {
              setSigningOut(false);
            },
            () => {
              setSigningOut(false);
              setFailed(true);
            },
          );
        }}
      >
        {signingOut ? t('session.signingOut') : t('session.signOut')}
      </button>
      {failed && (
        <p className="app__session-error" role="alert">
          {t('session.signOutFailed')}
        </p>
      )}
    </>
  );
}
