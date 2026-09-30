/**
 * The Russian catalogue — the default locale, and for now the only one (docs/SPEC.md §3.6).
 *
 * Every string a user can see lives here, under a key that says where it is shown. A string with
 * `{name}` takes that value; an entry with plural forms takes a `count` and picks its form by the
 * locale's plural rules. Adding English is adding `en.ts` of the same shape — the type below makes
 * a missing key a compile error — and nothing in a component changes.
 */
export const ru = {
  app: {
    name: 'Ritocode',
    tagline: 'Что ломается в коде, написанном ИИ, и как это увидеть.',
    skipToContent: 'К содержимому',
  },
  nav: {
    label: 'Основная навигация',
    home: 'Главная',
  },
  session: {
    signedInAs: 'Вы вошли как {username}',
    signedOut: 'Вы не вошли',
    requiredTitle: 'Нужно войти',
    requiredText: 'Эта страница открывается только после входа.',
  },
  home: {
    lead: 'Каталог того, что ломается в коде, написанном ИИ, и тренажёр, который учит это видеть.',
    backendTitle: 'Сервер',
    backendAt: 'Адрес API: {url}',
    checking: 'Проверяем API…',
  },
  notFound: {
    title: 'Страница не найдена',
    text: 'По этому адресу ничего нет.',
    home: 'На главную',
  },
  state: {
    loading: 'Загрузка…',
    retry: 'Попробовать снова',
    requestId: 'Идентификатор запроса: {id}',
    fieldError: '{field}: {messages}',
    unreachable: 'Не удалось связаться с API. Проверьте, что сервер запущен.',
    unexpectedStatus: 'API ответил неожиданным статусом ({status}).',
    unexpectedResponse: 'API ответил неожиданным статусом.',
    failed: 'Что-то пошло не так.',
  },
} as const;
