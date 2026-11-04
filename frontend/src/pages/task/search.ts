/**
 * The search behind both steps (docs/SPEC.md §4.4, docs/DESIGN.md): the learner describes what they
 * see in their own words, and the options that share a word with the description come first.
 *
 * Russian words change their endings — «гонка», «гонки», «гонку» — so each word of the query is cut
 * to a crude stem before it is looked for: two letters off a word of five or more, one off a word
 * of four. That finds the forms without a dictionary, at the price of the odd loose match, which
 * ranking pushes down. Words too short or too common to mean anything are dropped.
 *
 * An option matches when any stem is found in it, and options are ranked by how many stems they
 * share, then by where the stems were found — the name counts most, the keywords next, the summary
 * least — and then keep their own order. Pure functions, so the ranking is tested without a screen.
 */

const STOP_WORDS = new Set(['без', 'для', 'нет', 'что', 'это', 'как', 'или', 'при', 'все', 'его', 'они', 'где', 'так', 'the', 'and']);

export function normalise(text: string): string {
  return text.toLocaleLowerCase('ru').replaceAll('ё', 'е');
}

/** The stems a query is searched by; empty when it holds no word worth searching for. */
export function stems(query: string): string[] {
  return normalise(query)
    .split(/[^\p{L}\p{N}_.-]+/u)
    .filter((word) => word.length > 1 && !STOP_WORDS.has(word))
    .map((word) => (word.length > 4 ? word.slice(0, -2) : word.length > 3 ? word.slice(0, -1) : word));
}

/** What an option is searched by. */
export interface Searchable {
  readonly name: string;
  readonly summary: string;
  readonly keywords: string;
}

/** The options that share a stem with the query, best first. */
export function rank<T>(options: readonly T[], query: readonly string[], fields: (option: T) => Searchable): T[] {
  return options
    .map((option, index) => {
      const { name, summary, keywords } = fields(option);
      const [inName, inKeywords, inSummary] = [normalise(name), normalise(keywords), normalise(summary)];
      let shared = 0;
      let weight = 0;

      for (const stem of query) {
        const score = (inName.includes(stem) ? 3 : 0) + (inKeywords.includes(stem) ? 2 : 0) + (inSummary.includes(stem) ? 1 : 0);
        shared += score > 0 ? 1 : 0;
        weight += score;
      }

      return { option, index, shared, weight };
    })
    .filter((entry) => entry.shared > 0)
    .sort((a, b) => b.shared - a.shared || b.weight - a.weight || a.index - b.index)
    .map((entry) => entry.option);
}

export interface Fragment {
  readonly text: string;
  readonly match: boolean;
}

/** The text cut into the stretches a stem matched and the rest, for highlighting. */
export function highlight(text: string, query: readonly string[]): Fragment[] {
  const lower = normalise(text);
  const ranges: [number, number][] = [];

  for (const stem of query) {
    for (let at = lower.indexOf(stem); at !== -1; at = lower.indexOf(stem, at + stem.length)) {
      ranges.push([at, at + stem.length]);
    }
  }

  if (ranges.length === 0) {
    return [{ text, match: false }];
  }

  ranges.sort((a, b) => a[0] - b[0]);
  const merged: [number, number][] = [];
  for (const range of ranges) {
    const last = merged[merged.length - 1];
    if (last !== undefined && range[0] <= last[1]) {
      last[1] = Math.max(last[1], range[1]);
    } else {
      merged.push([range[0], range[1]]);
    }
  }

  const fragments: Fragment[] = [];
  let position = 0;
  for (const [start, end] of merged) {
    if (start > position) {
      fragments.push({ text: text.slice(position, start), match: false });
    }
    fragments.push({ text: text.slice(start, end), match: true });
    position = end;
  }
  if (position < text.length) {
    fragments.push({ text: text.slice(position), match: false });
  }

  return fragments;
}
