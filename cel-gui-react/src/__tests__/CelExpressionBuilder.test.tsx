import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CelExpressionBuilder } from '../components/CelExpressionBuilder.tsx';
import { CelVisualBuilder } from '../components/CelVisualBuilder.tsx';
import { CelGuiFilterRoot, CelGuiValueRoot } from '../types.ts';

// Mock NodeRenderer — emits filter-node changes (CelGuiNode level, builder wraps in expression)
vi.mock('../components/NodeRenderer.tsx', () => ({
  NodeRenderer: ({ onChange }: any) => (
    <div data-testid="node-renderer">
      <span>Visual Mode Node</span>
      <button onClick={() => onChange({ type: 'rule', field: 'a', operator: '==', value: 2 })}>
        Change Node
      </button>
    </div>
  ),
}));

// Mock ChipValueComposer (chip-based value editor)
vi.mock('../components/ChipValueComposer.tsx', () => ({
  ChipValueComposer: ({ onChange }: any) => (
    <div data-testid="value-node-renderer">
      <span>Value Mode Node</span>
      <button onClick={() => onChange({ type: 'field-ref', field: 'user.name' })}>
        Change Value Node
      </button>
    </div>
  ),
}));

// Mock CelCodeEditor
vi.mock('../editor/CelCodeEditor.tsx', () => ({
  default: function MockEditor({ value, onChange }: any) {
    return (
      <textarea
        data-testid="cel-code-editor"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    );
  },
}));

describe('CelExpressionBuilder', () => {
  const defaultFilterNode: CelGuiFilterRoot = {
    kind: 'filter',
    root: { type: 'rule', field: 'a', operator: '==', value: 1 },
  };

  const defaultValueNode: CelGuiValueRoot = {
    kind: 'value',
    resultType: 'string',
    root: { type: 'field-ref', field: 'user.name' },
  };

  it('renders visual filter mode by default (uncontrolled)', () => {
    render(<CelExpressionBuilder kind="filter" defaultValue={defaultFilterNode} />);
    expect(screen.getByTestId('node-renderer')).toBeInTheDocument();
    expect(screen.getByText('Visual Mode Node')).toBeInTheDocument();
  });

  it('renders visual value mode with kind="value"', () => {
    render(<CelExpressionBuilder kind="value" defaultValue={defaultValueNode} />);
    expect(screen.getByTestId('value-node-renderer')).toBeInTheDocument();
    expect(screen.getByText('Value Mode Node')).toBeInTheDocument();
  });

  it('renders correctly without initial node', () => {
    render(<CelExpressionBuilder />);
    expect(screen.getByText('No expression')).toBeInTheDocument();
  });

  it('updates filter node internally in uncontrolled mode and wraps in expression root', () => {
    const onChange = vi.fn();
    render(<CelExpressionBuilder kind="filter" defaultValue={defaultFilterNode} onChange={onChange} />);

    fireEvent.click(screen.getByText('Change Node'));
    expect(onChange).toHaveBeenCalledWith({
      kind: 'filter',
      root: { type: 'rule', field: 'a', operator: '==', value: 2 },
    });
  });

  it('updates value node internally in uncontrolled mode and wraps in expression root', () => {
    const onChange = vi.fn();
    render(<CelExpressionBuilder kind="value" defaultValue={defaultValueNode} onChange={onChange} />);

    fireEvent.click(screen.getByText('Change Value Node'));
    expect(onChange).toHaveBeenCalledWith({
      kind: 'value',
      resultType: 'string',
      root: { type: 'field-ref', field: 'user.name' },
    });
  });

  it('respects controlled value and does not update internally', () => {
    const onChange = vi.fn();
    const { rerender } = render(
      <CelExpressionBuilder kind="filter" value={defaultFilterNode} onChange={onChange} />
    );

    fireEvent.click(screen.getByText('Change Node'));
    expect(onChange).toHaveBeenCalled();

    const newNode: CelGuiFilterRoot = {
      kind: 'filter',
      root: { type: 'rule', field: 'a', operator: '==', value: 2 },
    };
    rerender(<CelExpressionBuilder kind="filter" value={newNode} onChange={onChange} />);
  });

  it('switches between visual and source mode', async () => {
    const conversion = {
      toCelString: vi.fn().mockResolvedValue('a == 1'),
      toGuiModel: vi.fn().mockResolvedValue(defaultFilterNode),
    };

    render(<CelExpressionBuilder kind="filter" defaultValue={defaultFilterNode} conversion={conversion} />);

    expect(screen.getByTestId('node-renderer')).toBeInTheDocument();

    const toggleBtn = screen.getByText('Source');
    await act(async () => {
      fireEvent.click(toggleBtn);
    });

    expect(conversion.toCelString).toHaveBeenCalledWith(defaultFilterNode, false);
    expect(screen.getByText('Visual')).toBeInTheDocument();

    const editor = await screen.findByTestId('cel-code-editor');
    expect(editor).toHaveValue('a == 1');

    await act(async () => {
      fireEvent.click(screen.getByText('Visual'));
    });

    expect(conversion.toGuiModel).toHaveBeenCalledWith('a == 1', 'filter', undefined);
    expect(screen.getByText('Source')).toBeInTheDocument();
    expect(screen.getByTestId('node-renderer')).toBeInTheDocument();
  });

  it('switches value builder to source and back', async () => {
    const conversion = {
      toCelString: vi.fn().mockResolvedValue('user.name'),
      toGuiModel: vi.fn().mockResolvedValue(defaultValueNode),
    };

    render(<CelExpressionBuilder kind="value" defaultValue={defaultValueNode} conversion={conversion} />);

    await act(async () => {
      fireEvent.click(screen.getByText('Source'));
    });

    expect(conversion.toCelString).toHaveBeenCalledWith(defaultValueNode, false);

    await act(async () => {
      fireEvent.click(screen.getByText('Visual'));
    });

    expect(conversion.toGuiModel).toHaveBeenCalledWith('user.name', 'value', 'string');
  });

  it('round-trips an empty value builder through source mode without calling toGuiModel for empty text', async () => {
    const conversion = {
      toCelString: vi.fn().mockResolvedValue(''),
      toGuiModel: vi.fn(),
    };
    const onChange = vi.fn();

    render(
      <CelExpressionBuilder
        kind="value"
        resultType="number"
        conversion={conversion}
        onChange={onChange}
      />
    );

    await act(async () => {
      fireEvent.click(screen.getByText('Source'));
    });

    const editor = await screen.findByTestId('cel-code-editor');
    expect(editor).toHaveValue('');

    await act(async () => {
      fireEvent.click(screen.getByText('Visual'));
    });

    expect(conversion.toGuiModel).not.toHaveBeenCalled();
    expect(onChange).toHaveBeenCalledWith({
      kind: 'value',
      resultType: 'number',
      root: { type: 'advanced-value', expression: '' },
    });
  });

  it('stays in visual mode if conversion to source fails', async () => {
    const conversion = {
      toCelString: vi.fn().mockRejectedValue(new Error('Failed to convert')),
      toGuiModel: vi.fn(),
    };

    render(<CelExpressionBuilder kind="filter" defaultValue={defaultFilterNode} conversion={conversion} />);

    await act(async () => {
      fireEvent.click(screen.getByText('Source'));
    });

    expect(screen.getByText('Failed to convert')).toBeInTheDocument();
    expect(screen.getByTestId('node-renderer')).toBeInTheDocument();
  });

  it('stays in source mode if conversion to visual fails', async () => {
    const conversion = {
      toCelString: vi.fn().mockResolvedValue('a == 1'),
      toGuiModel: vi.fn().mockRejectedValue(new Error('Parse error')),
    };

    render(<CelExpressionBuilder kind="filter" defaultValue={defaultFilterNode} conversion={conversion} />);

    await act(async () => {
      fireEvent.click(screen.getByText('Source'));
    });

    const editor = await screen.findByTestId('cel-code-editor');
    expect(editor).toBeInTheDocument();

    await act(async () => {
      fireEvent.click(screen.getByText('Visual'));
    });

    expect(screen.getByText('Parse error')).toBeInTheDocument();
    expect(screen.getByTestId('cel-code-editor')).toBeInTheDocument();
  });

  it('hides toggle if editorMode is locked to visual', () => {
    render(<CelExpressionBuilder editorMode="visual" defaultValue={defaultFilterNode} />);
    expect(screen.queryByText('Source')).not.toBeInTheDocument();
    expect(screen.queryByText('Visual')).not.toBeInTheDocument();
  });

  it('renders pretty print checkbox and toggles it', async () => {
    const conversion = {
      toCelString: vi.fn().mockResolvedValue('a == 1'),
      toGuiModel: vi.fn().mockResolvedValue(defaultFilterNode),
    };
    const onPrettyChange = vi.fn();

    render(
      <CelExpressionBuilder
        kind="filter"
        defaultValue={defaultFilterNode}
        conversion={conversion}
        onPrettyChange={onPrettyChange}
      />
    );

    const checkbox = screen.getByLabelText('Pretty Print') as HTMLInputElement;
    expect(checkbox).toBeInTheDocument();
    expect(checkbox.checked).toBe(false);

    fireEvent.click(checkbox);
    expect(checkbox.checked).toBe(true);
    expect(onPrettyChange).toHaveBeenCalledWith(true);
  });

  it('applies root className and style props', () => {
    render(
      <CelExpressionBuilder
        kind="filter"
        defaultValue={defaultFilterNode}
        className="custom-builder"
        style={{ marginTop: '12px' }}
      />
    );

    const root = document.querySelector('.cel-builder') as HTMLElement;
    expect(root).toHaveClass('custom-builder');
    expect(root).toHaveStyle({ marginTop: '12px' });
  });

  it('maps theme props to root CSS variables', () => {
    render(
      <CelExpressionBuilder
        kind="filter"
        defaultValue={defaultFilterNode}
        theme={{ primary: '#123456', radiusMd: '20px' }}
      />
    );

    const root = document.querySelector('.cel-builder') as HTMLElement;
    expect(root.style.getPropertyValue('--cel-primary')).toBe('#123456');
    expect(root.style.getPropertyValue('--cel-radius-md')).toBe('20px');
  });

  it('renders a visual-only filter builder without source controls', () => {
    render(<CelVisualBuilder defaultValue={defaultFilterNode} />);

    expect(screen.getByTestId('node-renderer')).toBeInTheDocument();
    expect(screen.queryByText('Source')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Pretty Print')).not.toBeInTheDocument();
  });

  it('renders a visual-only value builder without source controls', () => {
    render(<CelVisualBuilder defaultValue={defaultValueNode} />);

    expect(screen.getByTestId('value-node-renderer')).toBeInTheDocument();
  });

  it('re-formats source immediately when pretty print is toggled in source mode', async () => {
    const conversion = {
      toCelString: vi.fn().mockResolvedValue('a == 1'),
      toGuiModel: vi.fn().mockResolvedValue(defaultFilterNode),
    };

    render(
      <CelExpressionBuilder
        kind="filter"
        defaultValue={defaultFilterNode}
        conversion={conversion}
        editorMode="source"
      />
    );

    const checkbox = screen.getByLabelText('Pretty Print');

    conversion.toCelString.mockClear();

    await act(async () => {
      fireEvent.click(checkbox);
    });

    expect(conversion.toCelString).toHaveBeenCalledWith(expect.anything(), true);
  });
});
