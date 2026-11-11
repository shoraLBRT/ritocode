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
  it('starts from the search: the dock is the field and the filters, with no cards until something is typed or pressed', () => {
    diagnosis();

    expect(screen.getByLabelText(ru.task.search)).toBeInTheDocument();
    expect(screen.getByRole('group', { name: ru.task.classFilter })).toBeInTheDocument();
    expect(screen.queryAllByRole('checkbox')).toHaveLength(0);
    expect(screen.queryByRole('button', { name: ru.task.raiseDock })).not.toBeInTheDocument();
    expect(screen.getByText(ru.task.nothingPickedYet)).toBeInTheDocument();
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
    expect(screen.getAllByRole('article').map((card) => card.querySelector('.rc-pcard__name')?.textContent)).toEqual([
      'Секреты в репозитории',
      'Хардкод конфигурации',
    ]);
    expect(screen.getByText(ru.task.cardCount.few.replace('{count}', '2'))).toBeInTheDocument();
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

  it('shows the matches as the catalogue’s cards, each with its class, its own pick and a way to open it', () => {
    diagnosis([{ card: 'secrets-in-repo', leaves: [] }]);
    search('секреты');

    const card = screen.getByRole('article', { name: 'Секреты в репозитории' });
    expect(card).toHaveClass('rc-pcard', 'k-hygiene', 'rc-pcard--picked');
    expect(card).toHaveTextContent('Гигиена и безопасность');
    expect(within(card).getByRole('checkbox', { name: /^Секреты в репозитории/ })).toBeChecked();
    expect(within(card).getByRole('button', { name: ru.task.openCard.replace('{name}', 'Секреты в репозитории') })).toBeInTheDocument();
  });

  it('opens a card over the code with its class, summary and the class’s description, and walks the matches', () => {
    const { onToggle } = diagnosis();
    fireEvent.click(within(screen.getByRole('group', { name: ru.task.classFilter })).getByRole('button', { name: /Гигиена и безопасность/ }));
    fireEvent.click(screen.getByRole('button', { name: ru.task.openCard.replace('{name}', 'Секреты в репозитории') }));

    const dialog = screen.getByRole('dialog', { name: 'Секреты в репозитории' });
    expect(dialog).toHaveTextContent('Пароли и токены прямо в коде.');
    expect(dialog).toHaveTextContent('То, что должно быть закрыто оградой.');
    expect(dialog).toHaveTextContent(ru.task.cardPosition.replace('{index}', '1').replace('{count}', '2'));
    expect(within(dialog).getByRole('button', { name: ru.problems.close })).toHaveFocus();

    fireEvent.click(within(dialog).getByRole('button', { name: ru.task.pickLong }));
    expect(onToggle).toHaveBeenLastCalledWith('secrets-in-repo');

    fireEvent.click(within(dialog).getByRole('button', { name: ru.task.nextCard }));
    expect(screen.getByRole('dialog', { name: 'Хардкод конфигурации' })).toBeInTheDocument();
    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'ArrowRight' });
    expect(screen.getByRole('dialog', { name: 'Секреты в репозитории' })).toBeInTheDocument();

    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('raises the dock by the grip, and lowers it when the results are gone', () => {
    diagnosis();
    search('секрет');

    const grip = screen.getByRole('button', { name: ru.task.raiseDock });
    expect(grip).toHaveAttribute('aria-expanded', 'false');
    fireEvent.click(grip);
    expect(screen.getByRole('button', { name: ru.task.lowerDock })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('region', { name: ru.task.step1 })).toHaveClass('dock--raised');

    search('');
    expect(screen.queryByRole('button', { name: ru.task.lowerDock })).not.toBeInTheDocument();
    search('секрет');
    expect(screen.getByRole('button', { name: ru.task.raiseDock })).toBeInTheDocument();
  });

  it('raises the dock when the cards are scrolled down, and lowers it when scrolled up from the top', () => {
    diagnosis();
    search('секрет');
    const results = screen.getByRole('region', { name: ru.task.step1 }).querySelector('.dock__results');
    if (!(results instanceof HTMLElement)) {
      throw new Error('no results');
    }
    // jsdom lays nothing out: the cards are said to be taller than their box.
    Object.defineProperty(results, 'scrollHeight', { value: 900 });
    Object.defineProperty(results, 'clientHeight', { value: 300 });

    fireEvent.wheel(results, { deltaY: 40 });
    expect(screen.getByRole('button', { name: ru.task.lowerDock })).toBeInTheDocument();
    fireEvent.wheel(results, { deltaY: -40 });
    expect(screen.getByRole('button', { name: ru.task.raiseDock })).toBeInTheDocument();
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

  it('shows every branch with its leaves when nothing is typed, and narrows them by branch', () => {
    render(<TreatmentStep cards={cards} tree={tree} answer={[{ card: 'god-class', leaves: [] }]} onToggleLeaf={vi.fn()} action={check} />);

    expect(screen.getAllByRole('checkbox')).toHaveLength(3);
    expect(within(screen.getByRole('region', { name: 'Исправить руками сейчас' })).getAllByRole('checkbox')).toHaveLength(2);

    fireEvent.click(within(screen.getByRole('group', { name: ru.task.branchFilter })).getByRole('button', { name: /Оставить/ }));
    expect(screen.getAllByRole('checkbox')).toHaveLength(1);
    expect(screen.getByText(ru.task.leafMatches.one.replace('{count}', '1'))).toBeInTheDocument();
  });

  it('goes back to diagnosis from the findings column', () => {
    const onBack = vi.fn();
    render(<TreatmentStep cards={cards} tree={tree} answer={[{ card: 'god-class', leaves: [] }]} onToggleLeaf={vi.fn()} action={check} onBack={onBack} />);

    fireEvent.click(screen.getByRole('button', { name: ru.task.backToStep1 }));
    expect(onBack).toHaveBeenCalled();
  });

  it('says what an empty answer means, and still offers the check', () => {
    render(<TreatmentStep cards={cards} tree={tree} answer={[]} onToggleLeaf={vi.fn()} action={check} />);

    expect(screen.getByText(ru.task.nothingPicked)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: ru.task.check })).toBeInTheDocument();
  });
});
