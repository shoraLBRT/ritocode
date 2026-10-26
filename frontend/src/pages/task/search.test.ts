import { describe, expect, it } from 'vitest';
import { highlight, rank, stems } from './search';

describe('the stems a query is searched by', () => {
  it('cuts the endings off, so other forms of a word match', () => {
    expect(stems('Гонки')).toEqual(['гон']);
    expect(stems('чужие записи')).toEqual(['чуж', 'запи']);
  });

  it('drops short and common words, and reads ё as е', () => {
    expect(stems('нет проверки ёлки и')).toEqual(['провер', 'елк']);
    expect(stems('  ')).toEqual([]);
  });
});

describe('ranking', () => {
  const options = [
    { name: 'Проверка и действие врозь', summary: 'Два запроса проходят проверку оба.', keywords: 'гонка блокировка' },
    { name: 'Нет проверки владельца', summary: 'Чужую запись открывают подменой номера.', keywords: 'права доступ' },
    { name: 'Деньги во float', summary: 'Копейки расходятся.', keywords: 'decimal' },
  ];

  it('keeps only the options sharing a word, more shared words first', () => {
    expect(rank(options, stems('чужая запись проверка'), (o) => o).map((o) => o.name)).toEqual([
      'Нет проверки владельца',
      'Проверка и действие врозь',
    ]);
  });

  it('ranks a match in the name above one in the keywords, above one in the summary', () => {
    const named = { name: 'гонка', summary: '', keywords: '' };
    const keyed = { name: 'x', summary: '', keywords: 'гонка' };
    const told = { name: 'y', summary: 'гонка', keywords: '' };
    expect(rank([told, keyed, named], stems('гонка'), (o) => o)).toEqual([named, keyed, told]);
  });

  it('finds nothing for a word nobody uses', () => {
    expect(rank(options, stems('квантовый'), (o) => o)).toEqual([]);
  });
});

describe('highlighting', () => {
  it('marks every stretch a stem matched, whatever its case', () => {
    expect(highlight('Нет проверки: проверка нужна', stems('проверка'))).toEqual([
      { text: 'Нет ', match: false },
      { text: 'провер', match: true },
      { text: 'ки: ', match: false },
      { text: 'провер', match: true },
      { text: 'ка нужна', match: false },
    ]);
  });

  it('leaves the text whole when nothing matched', () => {
    expect(highlight('Деньги во float', stems('гонка'))).toEqual([{ text: 'Деньги во float', match: false }]);
  });
});
