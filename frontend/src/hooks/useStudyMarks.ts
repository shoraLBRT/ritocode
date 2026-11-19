import { useCallback, useEffect, useRef, useState } from 'react';
import { getStudiedCards, markStudied, unmarkStudied, useApiClient } from '../api';
import { useSession } from '../session';

/**
 * The marks «Изучено» on the problem cards (docs/SPEC.md §4.2 › Studying, ADR 0018), as `/problems`
 * shows and changes them.
 *
 * Signed in, they come from and go to the API. A guest's are kept in this browser only — counted
 * nowhere, and not carried over on sign-in: a signed-in learner sees their own marks and never the
 * browser's. Until the session is known, or when the marks could not be read, there are none and
 * nothing can be marked.
 */
export interface StudyMarks {
  /** Whether the marks are known, so the toggle can be offered. */
  readonly ready: boolean;
  /** The marks are this browser's, not an account's. */
  readonly guest: boolean;
  readonly studied: ReadonlySet<string>;
  readonly toggle: (card: string) => void;
}

export const GUEST_STUDY_KEY = 'ritocode:studied';

const NONE: ReadonlySet<string> = new Set();

export function useStudyMarks(): StudyMarks {
  const client = useApiClient();
  const session = useSession();
  const guest = session.status === 'signedOut';
  const account = session.status === 'signedIn' ? session.user.id : null;
  // The signed-in learner's marks, with whose they are, so another account never sees them.
  const [accountMarks, setAccountMarks] = useState<{ readonly of: string; readonly marks: ReadonlySet<string> } | null>(null);
  const [guestMarks, setGuestMarks] = useState<ReadonlySet<string>>(readGuestMarks);
  // A card whose change is on its way to the API takes no second one until it is answered.
  const pending = useRef(new Set<string>());

  useEffect(() => {
    if (account === null) {
      return;
    }

    const controller = new AbortController();
    getStudiedCards(client, controller.signal)
      .then((marks) => {
        setAccountMarks({ of: account, marks: new Set(marks.cards.map((mark) => mark.card)) });
      })
      .catch(() => {
        // Unread marks — refused, aborted or not in the shape asked for — are no marks: the
        // catalogue still reads as it is, without the toggle.
      });

    return () => {
      controller.abort();
    };
  }, [client, account]);

  const studied = guest ? guestMarks : account !== null && accountMarks?.of === account ? accountMarks.marks : null;

  const toggle = useCallback(
    (card: string) => {
      if (studied === null || pending.current.has(card)) {
        return;
      }

      const marking = !studied.has(card);

      if (guest) {
        const next = withMark(studied, card, marking);
        setGuestMarks(next);
        saveGuestMarks(next);
        return;
      }

      if (account === null) {
        return;
      }

      const change = (mark: boolean) => {
        setAccountMarks((current) => (current?.of === account ? { of: account, marks: withMark(current.marks, card, mark) } : current));
      };

      change(marking);
      pending.current.add(card);
      (marking ? markStudied(client, card) : unmarkStudied(client, card))
        .catch(() => {
          // Not changed on the server: the card shows what is stored.
          change(!marking);
        })
        .finally(() => {
          pending.current.delete(card);
        });
    },
    [client, guest, account, studied],
  );

  return { ready: studied !== null, guest, studied: studied ?? NONE, toggle };
}

function withMark(marks: ReadonlySet<string>, card: string, marked: boolean): ReadonlySet<string> {
  const next = new Set(marks);
  if (marked) {
    next.add(card);
  } else {
    next.delete(card);
  }
  return next;
}

function guestStorage(): Storage | null {
  try {
    return typeof window === 'undefined' ? null : window.localStorage;
  } catch {
    return null;
  }
}

/** The guest's marks in this browser; none when nothing, or nothing readable, is kept. */
export function readGuestMarks(): ReadonlySet<string> {
  try {
    const kept: unknown = JSON.parse(guestStorage()?.getItem(GUEST_STUDY_KEY) ?? '[]');
    return new Set(Array.isArray(kept) ? kept.filter((card): card is string => typeof card === 'string') : []);
  } catch {
    return new Set();
  }
}

/** Keeps the guest's marks; a browser that refuses simply forgets them. */
function saveGuestMarks(marks: ReadonlySet<string>): void {
  try {
    guestStorage()?.setItem(GUEST_STUDY_KEY, JSON.stringify([...marks].sort()));
  } catch {
    // Not kept — the page still shows the mark until it is left.
  }
}
