// The learning flow end to end (#39, docs/SPEC.md §4.6): a visitor opens a task signed out, picks a
// card and a treatment, presses Check, signs in with GitHub — the fake one — and lands on the review
// of that very answer, which then shows in their progress. Each step waits for what the learner would
// see, so a break anywhere in the flow fails the step where it breaks.
//
// The texts come from the pages' own catalogue, so a reworded label does not break the test; the task
// is the demo task, whose key is in content/tasks/flower-shop-daily-revenue/task.yaml.
import { expect, test } from '@playwright/test';
import { ru } from '../frontend/src/i18n/ru';
import { translate } from '../frontend/src/i18n/translate';
import type { MessageKey, MessageParams } from '../frontend/src/i18n/translate';

const t = (key: MessageKey, params?: MessageParams) => translate(ru, 'ru', key, params);

const task = 'flower-shop-daily-revenue';
// One of the task's two findings, with one of the author's leaves for it.
const card = 'Секреты в репозитории';
const branch = 'Автоматическая проверка';
const leaf = 'сканер секретов';
// The other finding, which this answer misses.
const missedCard = 'Деньги во float';

test('a visitor solves a task signed out, signs in on Check, and sees the review and the progress', async ({ page }) => {
  await test.step('opens the task signed out', async () => {
    await page.goto(`/tasks/${task}`);
    await expect(page.getByText(t('session.signedOut'))).toBeVisible();
  });

  await test.step('picks a card at step 1', async () => {
    // A checkbox is named by the card's name and then its summary, and another card's summary may
    // name this card, so the name is matched from its start.
    await page.getByRole('checkbox', { name: new RegExp(`^${card}`) }).check();
    await page.getByRole('button', { name: t('task.toStep2') }).click();
  });

  await test.step('picks a treatment at step 2', async () => {
    const pick = page.getByRole('group', { name: card });
    await pick.getByText(branch, { exact: true }).click();
    await pick.getByRole('checkbox', { name: leaf }).check();
  });

  await test.step('presses Check and is asked to sign in', async () => {
    await page.getByRole('button', { name: t('task.check') }).click();
    const prompt = page.getByRole('region', { name: t('task.signInTitle') });
    await expect(prompt).toBeVisible();
    await prompt.getByRole('link', { name: t('session.signInWith.github') }).click();
  });

  await test.step('comes back signed in, to the review of that answer', async () => {
    await expect(page).toHaveURL(new RegExp(`/tasks/${task}/attempts/[0-9a-f-]{36}$`));
    await expect(page.getByText(/^Вы вошли как e2e-learner-/)).toBeVisible();
    await expect(page.getByText(t('review.composition', { found: 1, present: 2, extra: 0, matched: 1 }))).toBeVisible();
    await expect(page.getByText(card).first()).toBeVisible();
    await expect(page.getByText(missedCard).first()).toBeVisible();
  });

  await test.step('sees the attempt in the progress', async () => {
    await page.getByRole('link', { name: t('nav.progress') }).click();
    await expect(page.getByRole('heading', { level: 1, name: t('progress.title') })).toBeVisible();
    await expect(page.getByText(t('progress.tasks', { count: 1 }))).toBeVisible();

    // Met, found, missed, picked when absent, treated right.
    await expect(page.getByRole('row', { name: card }).getByRole('cell')).toHaveText(['1', '1', '0', '0', '1']);
    await expect(page.getByRole('row', { name: missedCard }).getByRole('cell')).toHaveText(['1', '0', '1', '0', '0']);
  });
});
