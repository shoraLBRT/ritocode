import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { ru } from '../../i18n';
import { DiagnosisStep } from './DiagnosisStep';
import { TreatmentStep } from './TreatmentStep';
import { classes, cards, tree } from './fixtures';

function diagnosis(answer: { card: string; leaves: string[] }[] = [], onToggle = vi.fn(), onNext = vi.fn()) {
  render(<DiagnosisStep classes={classes} cards={cards} answer={answer} onToggle={onToggle} onNext={onNext} />);
  return { onToggle, onNext };
}

const search = (value: string) => {
  fireEvent.change(screen.getByLabelText(ru.task.search), { target: { value } });
};

describe('step 1: what do you see', () => {
  it('starts from the search, with the catalogue folded by class and muted', () => {
    diagnosis();

    expect(screen.getByLabelText(ru.task.search)).toBeInTheDocument();
    expect(screen.queryAllByRole('checkbox')).toHaveLength(0);
    expect(screen.getByText(ru.task.browse)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Разрастание/, expanded: false }));
    expect(screen.getByRole('checkbox', { name: /^Класс-бог/ })).toBeInTheDocument();
  });

  it('finds cards by name, summary and keywords as the learner types, whatever the word form', () => {
    diagnosis();

    search('копейки');
    expect(screen.getAllByRole('checkbox')).toHaveLength(1);
    expect(screen.getByRole('checkbox', { name: /^Деньги во float/ })).toBeInTheDocument();
    expect(screen.getByText(ru.task.matches.one.replace('{count}', '1'))).toBeInTheDocument();

    search('пароли секреты');
    expect(screen.getByRole('checkbox', { name: /^Секреты в репозитории/ })).toBeInTheDocument();
    expect(screen.getAllByText('Секре', { selector: 'mark' }).length).toBeGreaterThan(0);

    search('квантовый');
    expect(screen.getByText(ru.task.noMatch)).toBeInTheDocument();
  });

  it('narrows the cards by class, and counts each class among the matches', () => {
    diagnosis();

    const filters = screen.getByRole('group', { name: ru.task.classFilter });
    fireEvent.click(within(filters).getByRole('button', { name: /Гигиена и безопасность/ }));

    expect(within(filters).getByRole('button', { name: /Гигиена и безопасность/ })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getAllByRole('checkbox').map((box) => box.closest('label')?.querySelector('.option__name')?.textContent)).toEqual([
      'Секреты в репозитории',
      'Хардкод конфигурации',
    ]);
  });

  it('picks with a click or with Enter on the match walked to, and shows the picks as chips', () => {
    const { onToggle } = diagnosis([{ card: 'god-class', leaves: [] }]);

    expect(within(screen.getByRole('list', { name: ru.task.pickedCards })).getByText('Класс-бог')).toBeInTheDocument();

    search('секрет конфигурации');
    fireEvent.keyDown(screen.getByLabelText(ru.task.search), { key: 'ArrowDown' });
    fireEvent.keyDown(screen.getByLabelText(ru.task.search), { key: 'Enter' });
    expect(onToggle).toHaveBeenLastCalledWith('hardcoded-config');

    fireEvent.click(screen.getByRole('checkbox', { name: /^Секреты в репозитории/ }));
    expect(onToggle).toHaveBeenLastCalledWith('secrets-in-repo');

    fireEvent.click(screen.getByRole('button', { name: ru.task.remove.replace('{name}', 'Класс-бог') }));
    expect(onToggle).toHaveBeenLastCalledWith('god-class');
  });

  it('moves on to treatment, or calls a clean answer clean', () => {
    const { onNext } = diagnosis();
    fireEvent.click(screen.getByRole('button', { name: ru.task.toStep2Clean }));
    expect(onNext).toHaveBeenCalled();
  });
});

describe('step 2: what do you do with it here', () => {
  const check = <button type="button">{ru.task.check}</button>;

  it('lists the picked findings with their state, the current one with its summary', () => {
    const answer = [
      { card: 'god-class', leaves: [] },
      { card: 'money-in-float', leaves: ['manual.representation'] },
    ];
    render(<TreatmentStep cards={cards} tree={tree} answer={answer} onToggleLeaf={vi.fn()} action={check} />);

    const findings = within(screen.getByRole('list', { name: ru.task.findings })).getAllByRole('listitem');
    expect(findings.map((item) => item.querySelector('.finding__name')?.textContent)).toEqual(['Класс-бог', 'Деньги во float']);
    expect(findings[0]).toHaveTextContent(ru.task.needsLeafShort);
    expect(findings[0]).toHaveTextContent('Один класс делает всё.');
    expect(findings[1]).toHaveTextContent('1 мера');

    fireEvent.click(screen.getByRole('button', { name: /Деньги во float/ }));
    expect(within(screen.getByRole('list', { name: ru.task.pickedLeaves })).getByText('Руками · исправить представление данных')).toBeInTheDocument();
  });

  it('finds the leaves by word, each saying where its measure lives, and ticks one for the current finding', () => {
    const onToggleLeaf = vi.fn();
    render(<TreatmentStep cards={cards} tree={tree} answer={[{ card: 'god-class', leaves: [] }]} onToggleLeaf={onToggleLeaf} action={check} />);

    fireEvent.change(screen.getByLabelText(ru.task.leafSearch), { target: { value: 'оставить' } });
    const leaf = screen.getByRole('checkbox', { name: /^в этом контексте это нормально/ });
    expect(leaf.closest('label')).toHaveTextContent(ru.task.branchWhere.accept);

    fireEvent.click(leaf);
    expect(onToggleLeaf).toHaveBeenCalledWith('god-class', 'accept.fits-context');
  });

  it('opens a branch to browse when nothing is typed', () => {
    render(<TreatmentStep cards={cards} tree={tree} answer={[{ card: 'god-class', leaves: [] }]} onToggleLeaf={vi.fn()} action={check} />);

    expect(screen.queryAllByRole('checkbox')).toHaveLength(0);
    fireEvent.click(screen.getByRole('button', { name: /Исправить руками сейчас/, expanded: false }));
    expect(screen.getAllByRole('checkbox')).toHaveLength(2);
  });

  it('says what an empty answer means, and still offers the check', () => {
    render(<TreatmentStep cards={cards} tree={tree} answer={[]} onToggleLeaf={vi.fn()} action={check} />);

    expect(screen.getByText(ru.task.nothingPicked)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: ru.task.check })).toBeInTheDocument();
  });
});
