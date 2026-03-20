import { render } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { CelCodeEditor } from '../editor/CelCodeEditor.tsx';

const codeMirrorSpy = vi.fn<[unknown], null>(() => null);

vi.mock('@uiw/react-codemirror', () => ({
  default: (props: unknown) => {
    codeMirrorSpy(props);
    return null;
  },
}));

describe('CelCodeEditor', () => {
  it('disables indentWithTab so Tab can accept completions', () => {
    render(<CelCodeEditor value="user." />);

    expect(codeMirrorSpy).toHaveBeenCalled();
    const lastCall = codeMirrorSpy.mock.calls.at(-1);

    expect(lastCall).toBeDefined();
    const props = lastCall?.[0] as
      | {
          indentWithTab?: boolean;
          extensions?: unknown[];
        }
      | undefined;

    expect(props.indentWithTab).toBe(false);
    expect(Array.isArray(props.extensions)).toBe(true);
    expect(props.extensions).toHaveLength(4);
  });
});
