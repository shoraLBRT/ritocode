import { useT } from '../i18n';

export interface Section {
  readonly to: string;
  readonly label: string;
}

/**
 * The arena's sections (docs/DESIGN.md › The arena); the admin area only for an admin. The header's
 * row, the phone menu and the footer list the same ones.
 */
export function useSections(admin: boolean): readonly Section[] {
  const t = useT();
  return [
    { to: '/tasks', label: t('nav.tasks') },
    { to: '/problems', label: t('nav.problems') },
    ...(admin ? [{ to: '/admin', label: t('nav.admin') }] : []),
  ];
}
