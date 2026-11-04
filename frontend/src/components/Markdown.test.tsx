import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { Markdown } from './Markdown';

function html(source: string): string {
  return render(<Markdown source={source} />).container.innerHTML;
}

describe('Markdown', () => {
  it('joins the lines of a paragraph and separates paragraphs at a blank line', () => {
    expect(html('Первая строка\nи её продолжение.\n\nВторой абзац.')).toBe(
      '<p>Первая строка и её продолжение.</p><p>Второй абзац.</p>',
    );
  });

  it('renders a bulleted list, with an indented line continuing its item', () => {
    expect(html('- один\n- два,\n  и дальше\n- три')).toBe('<ul><li>один</li><li>два, и дальше</li><li>три</li></ul>');
  });

  it('renders a numbered list, and a paragraph after a list', () => {
    expect(html('1. раз\n2. два\n\nПотом текст.')).toBe('<ol><li>раз</li><li>два</li></ol><p>Потом текст.</p>');
  });

  it('renders code, bold, emphasis and links inline', () => {
    expect(html('`round(total, 2)` и **важно**, *тоже* и _так_ — [OWASP](https://owasp.org/x)')).toBe(
      '<p><code>round(total, 2)</code> и <strong>важно</strong>, <em>тоже</em> и <em>так</em> — '
        + '<a href="https://owasp.org/x" rel="noreferrer noopener" target="_blank">OWASP</a></p>',
    );
  });

  it('renders ## and ### as headings, each ending the block above it', () => {
    expect(html('Вступление.\n## Что мы храним\n- адрес почты\n### Сколько\nПока аккаунт **жив**.')).toBe(
      '<p>Вступление.</p><h2>Что мы храним</h2><ul><li>адрес почты</li></ul><h3>Сколько</h3>'
        + '<p>Пока аккаунт <strong>жив</strong>.</p>',
    );
  });

  it('reads a single # and four of them as the text they are', () => {
    expect(html('# один\n\n#### четыре\n\n##без пробела')).toBe('<p># один</p><p>#### четыре</p><p>##без пробела</p>');
  });

  it('leaves emphasis marks inside code alone', () => {
    expect(html('`**not bold**`')).toBe('<p><code>**not bold**</code></p>');
  });

  it('never turns text into markup, and links only to http and https', () => {
    const rendered = html('<script>alert(1)</script> [x](javascript:alert(1))');

    expect(rendered).toBe('<p>&lt;script&gt;alert(1)&lt;/script&gt; [x](javascript:alert(1))</p>');
  });
});
