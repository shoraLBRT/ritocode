import { useId, useState } from "react";
import { useNavigate } from "react-router";
import { exportUrl, useApiClient } from "../../api";
import { useT } from "../../i18n";
import { useSession } from "../../session";
import { ProfileCard } from "./parts";

/**
 * The learner's data (#299): download everything the site keeps about them as a JSON file, or delete
 * the account. Deleting asks for the username typed out — the button stays off until it matches — and
 * then the server erases every module's rows and clears the session; the page goes home signed out.
 */
export function AccountCard({ index }: { index: number }) {
  const t = useT();
  const client = useApiClient();
  const session = useSession();
  const navigate = useNavigate();
  const inputId = useId();
  const [confirming, setConfirming] = useState(false);
  const [typed, setTyped] = useState("");
  const [deleting, setDeleting] = useState(false);
  const [failed, setFailed] = useState(false);

  if (session.status !== "signedIn") {
    return null;
  }

  const { username } = session.user;
  // Usernames are stored lower-cased; the server compares without case, and so does this.
  const matches = typed.trim().toLowerCase() === username.toLowerCase();

  return (
    <ProfileCard
      id="profile-account"
      title={t("profile.account.title")}
      index={index}
      className="rc-profile__account"
    >
      <p className="rc-profile__account-text">
        {t("profile.account.exportText")}
      </p>
      <a
        className="button rc-profile__account-export"
        href={exportUrl(client)}
        download
      >
        {t("profile.account.export")}
      </a>
      {confirming ? (
        <form
          className="rc-profile__account-confirm"
          onSubmit={(event) => {
            event.preventDefault();
            if (!matches || deleting) {
              return;
            }
            setDeleting(true);
            setFailed(false);
            session.deleteAccount(typed.trim()).then(
              () => {
                void navigate("/", { replace: true });
              },
              () => {
                setDeleting(false);
                setFailed(true);
              },
            );
          }}
        >
          <p className="rc-profile__account-warning">
            {t("profile.account.deleteWarning")}
          </p>
          <label htmlFor={inputId} className="rc-profile__account-label">
            {t("profile.account.deleteLabel", { username })}
          </label>
          <input
            id={inputId}
            className="rc-profile__account-input"
            type="text"
            autoComplete="off"
            spellCheck={false}
            value={typed}
            onChange={(event) => {
              setTyped(event.target.value);
            }}
          />
          <div className="rc-profile__account-actions">
            <button
              type="submit"
              className="button rc-profile__account-delete"
              disabled={!matches || deleting}
            >
              {deleting
                ? t("profile.account.deleting")
                : t("profile.account.deleteConfirm")}
            </button>
            <button
              type="button"
              className="button"
              disabled={deleting}
              onClick={() => {
                setConfirming(false);
                setTyped("");
                setFailed(false);
              }}
            >
              {t("profile.account.cancel")}
            </button>
          </div>
          {failed && (
            <p className="app__session-error" role="alert">
              {t("profile.account.deleteFailed")}
            </p>
          )}
        </form>
      ) : (
        <button
          type="button"
          className="button rc-profile__account-start"
          onClick={() => {
            setConfirming(true);
          }}
        >
          {t("profile.account.delete")}
        </button>
      )}
    </ProfileCard>
  );
}
