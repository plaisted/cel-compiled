import { describe, it, expect } from 'vitest';
import { createCelCompletionSource } from '../editor/completion.ts';
import { CompletionContext } from '@codemirror/autocomplete';

describe('completion', () => {
  const schema = {
    fields: [
      {
        name: 'user',
        type: 'map' as const,
        children: [
          { name: 'user.name', type: 'string' as const },
          { name: 'user.age', type: 'number' as const },
        ],
      },
      { name: 'status', type: 'string' as const },
    ],
  };

  const completionSource = createCelCompletionSource(schema);

  const createMockContext = (text: string, explicit = true): CompletionContext => {
    return {
      matchBefore: (regex: RegExp) => {
        // Anchor to end of string so we match the trailing word, not a prefix
        const anchoredRegex = new RegExp(regex.source + '$', regex.flags);
        const match = text.match(anchoredRegex);
        if (match) {
          return { from: text.length - match[0].length, to: text.length, text: match[0] };
        }
        return null;
      },
      explicit,
    } as CompletionContext;
  };

  it('provides top-level schema fields and keywords', () => {
    const context = createMockContext('st');
    const result = completionSource(context);

    expect(result).not.toBeNull();
    const labels = result!.options.map((o) => o.label);

    // Schema fields
    expect(labels).toContain('user');
    expect(labels).toContain('status');

    // Keywords
    expect(labels).toContain('true');
    expect(labels).toContain('false');

    // Builtins (some of them)
    expect(labels).toContain('has');
  });

  it('provides dot-completion for schema fields', () => {
    const context = createMockContext('user.');
    const result = completionSource(context);

    expect(result).not.toBeNull();
    const labels = result!.options.map((o) => o.label);

    expect(labels).toContain('user.name');
    expect(labels).toContain('user.age');
  });

  it('provides receiver methods after dot', () => {
    const context = createMockContext('user.name.');
    const result = completionSource(context);

    expect(result).not.toBeNull();
    const labels = result!.options.map((o) => o.label);

    // Receiver methods from builtins/extensions
    expect(labels).toContain('contains');
    expect(labels).toContain('startsWith');
  });
});
