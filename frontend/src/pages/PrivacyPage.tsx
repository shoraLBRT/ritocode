import { Markdown } from '../components/Markdown';
import { useT } from '../i18n';
import policy from '../site/privacy.ru.md?raw';

/**
 * `/privacy`: the privacy policy (docs/SPEC.md §10.4, #128), the Markdown of `src/site/privacy.ru.md`
 * built into the bundle and prerendered with `/` and `/problems`. The maintainer's text replaces the
 * placeholder in that file (#134); nothing here changes when it does.
 */
export function PrivacyPage() {
  const t = useT();

  return (
    <article className="page privacy">
      <h1>{t('privacy.title')}</h1>
      <Markdown source={policy} />
    </article>
  );
}
