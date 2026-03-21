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
          theme?: string;
        }
      | undefined;

    expect(props).toBeDefined();
    const codeMirrorProps = props!;

    expect(codeMirrorProps.indentWithTab).toBe(false);
    expect(codeMirrorProps.theme).toBe('light');
    expect(Array.isArray(codeMirrorProps.extensions)).toBe(true);
    expect(codeMirrorProps.extensions).toHaveLength(4);
  });
});
