import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { ru } from '../../i18n';
import { DiagnosisStep } from './DiagnosisStep';
import { TreatmentStep } from './TreatmentStep';
import { classes, cards, tree } from './fixtures';

describe('step 1: what do you see', () => {
  it('offers the cards by name and summary, grouped by class, each a checkbox', () => {
    render(<DiagnosisStep classes={classes} cards={cards} answer={[]} onToggle={vi.fn()} />);

    const groups = screen.getAllByRole('group');
    expect(groups.map((group) => within(group).getAllByRole('checkbox').length)).toEqual([2, 1, 1]);
    expect(screen.getByRole('checkbox', { name: /Секреты в репозитории/ })).toBeInTheDocument();
    expect(screen.getByText('Пароли и токены прямо в коде.')).toBeInTheDocument();
  });

  it('searches names, summaries and keywords', () => {
    render(<DiagnosisStep classes={classes} cards={cards} answer={[]} onToggle={vi.fn()} />);

    fireEvent.change(screen.getByLabelText(ru.task.search), { target: { value: 'копейки' } });

    expect(screen.getAllByRole('checkbox')).toHaveLength(1);
    expect(screen.getByRole('checkbox', { name: /Деньги во float/ })).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText(ru.task.search), { target: { value: 'нет такого' } });
    expect(screen.getByText(ru.task.noMatch)).toBeInTheDocument();
  });

  it('picks a card, and shows the picked ones checked', () => {
    const onToggle = vi.fn();
    render(<DiagnosisStep classes={classes} cards={cards} answer={[{ card: 'god-class', leaves: [] }]} onToggle={onToggle} />);

    expect(screen.getByRole('checkbox', { name: /Класс-бог/ })).toBeChecked();

    fireEvent.click(screen.getByRole('checkbox', { name: /Деньги во float/ }));
    expect(onToggle).toHaveBeenCalledWith('money-in-float');
  });
});

describe('step 2: what do you do with it here', () => {
  it('shows the whole tree for every picked card, each leaf a checkbox under a branch', () => {
    const answer = [
      { card: 'god-class', leaves: [] },
      { card: 'money-in-float', leaves: ['manual.representation'] },
    ];
    const { container } = render(<TreatmentStep cards={cards} tree={tree} answer={answer} onToggleLeaf={vi.fn()} />);

    // One fieldset per picked card; the branches inside are groups too, so they are not counted.
    const groups = [...container.querySelectorAll('fieldset')];
    expect(groups.map((group) => group.querySelector('legend')?.textContent)).toEqual(['Класс-бог', 'Деньги во float']);
    expect(groups.map((group) => within(group).getAllByRole('checkbox', { hidden: true }).length)).toEqual([3, 3]);

    // Branches are disclosure widgets a keyboard reaches; one holding a ticked leaf is open.
    const money = within(groups[1] as HTMLElement);
    expect(money.getByText('Исправить руками сейчас').tagName).toBe('SUMMARY');
    expect(money.getByRole('checkbox', { name: 'исправить представление данных' })).toBeChecked();
  });

  it('ticks a leaf for its card', () => {
    const onToggleLeaf = vi.fn();
    render(<TreatmentStep cards={cards} tree={tree} answer={[{ card: 'god-class', leaves: [] }]} onToggleLeaf={onToggleLeaf} />);

    fireEvent.click(screen.getByRole('checkbox', { name: 'в этом контексте это нормально', hidden: true }));

    expect(onToggleLeaf).toHaveBeenCalledWith('god-class', 'accept.fits-context');
  });

  it('says what an empty answer means', () => {
    render(<TreatmentStep cards={cards} tree={tree} answer={[]} onToggleLeaf={vi.fn()} />);

    expect(screen.getByText(ru.task.nothingPicked)).toBeInTheDocument();
  });
});
