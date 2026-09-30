import type { CandidateCard, ProblemClass, TaskDetail, TreatmentTree } from '../../api';

// A task as `GET /tasks/{slug}` serves it, and a small treatment tree, for the task screen's tests.

export const classes: ProblemClass[] = [
  { id: 'hygiene', name: 'Гигиена и безопасность', description: null },
  { id: 'growth', name: 'Разрастание', description: null },
  { id: 'domain', name: 'Предметная область', description: null },
];

export const cards: CandidateCard[] = [
  { slug: 'secrets-in-repo', class: 'hygiene', name: 'Секреты в репозитории', summary: 'Пароли и токены прямо в коде.', keywords: ['пароль'] },
  { slug: 'hardcoded-config', class: 'hygiene', name: 'Хардкод конфигурации', summary: 'Адреса и пути в коде.', keywords: [] },
  { slug: 'god-class', class: 'growth', name: 'Класс-бог', summary: 'Один класс делает всё.', keywords: [] },
  { slug: 'money-in-float', class: 'domain', name: 'Деньги во float', summary: 'Суммы во float.', keywords: ['копейки'] },
];

export const tree: TreatmentTree = {
  branches: [
    { id: 'manual', name: 'Исправить руками сейчас', leaves: [
      { id: 'manual.split', label: 'разделить по ответственности' },
      { id: 'manual.representation', label: 'исправить представление данных' },
    ] },
    { id: 'accept', name: 'Оставить осознанно', leaves: [{ id: 'accept.fits-context', label: 'в этом контексте это нормально' }] },
  ],
};

export const task: TaskDetail = {
  slug: 'flower-shop-daily-revenue',
  title: 'Выручка цветочного магазина за день',
  difficulty: 'easy',
  context: 'Небольшой цветочный магазин.',
  brief: '«Напиши скрипт.»',
  material: {
    files: [
      { path: 'requirements.txt', content: 'psycopg[binary]==3.2.3\n' },
      { path: 'revenue.py', content: 'import sys\n\nTAX_RATE = 0.06\n' },
    ],
    overview: {
      files: [
        { path: 'requirements.txt', lines: 1 },
        { path: 'revenue.py', lines: 3 },
      ],
      totalLines: 4,
      fileCount: 2,
      dependencies: ['psycopg'],
    },
  },
  classes,
  cards,
  sameMaterial: [],
};
