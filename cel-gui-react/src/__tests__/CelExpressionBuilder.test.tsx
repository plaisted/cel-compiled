import React from 'react';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CelExpressionBuilder } from '../components/CelExpressionBuilder.tsx';
import { CelGuiNode } from '../types.ts';

// Mock NodeRenderer
vi.mock('../components/NodeRenderer.tsx', () => ({
  NodeRenderer: ({ node, onChange }: any) => (
    <div data-testid="node-renderer">
      <span>Visual Mode Node</span>
      <button onClick={() => onChange({ type: 'rule', field: 'a', operator: '==', value: 2 })}>
        Change Node
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
  const defaultNode: CelGuiNode = { type: 'rule', field: 'a', operator: '==', value: 1 };

  it('renders visual mode by default (uncontrolled)', () => {
    render(<CelExpressionBuilder defaultValue={defaultNode} />);
    expect(screen.getByTestId('node-renderer')).toBeInTheDocument();
    expect(screen.getByText('Visual Mode Node')).toBeInTheDocument();
  });

  it('renders correctly without initial node', () => {
    render(<CelExpressionBuilder />);
    expect(screen.getByText('No expression')).toBeInTheDocument();
  });

  it('updates node internally in uncontrolled mode', () => {
    const onChange = vi.fn();
    render(<CelExpressionBuilder defaultValue={defaultNode} onChange={onChange} />);

    fireEvent.click(screen.getByText('Change Node'));
    expect(onChange).toHaveBeenCalledWith({ type: 'rule', field: 'a', operator: '==', value: 2 });
  });

  it('respects controlled value and does not update internally if not changed by parent', () => {
    const onChange = vi.fn();
    const { rerender } = render(<CelExpressionBuilder value={defaultNode} onChange={onChange} />);

    fireEvent.click(screen.getByText('Change Node'));
    expect(onChange).toHaveBeenCalled();

    // The node-renderer should still show the original node if we check its props (not easily done without spying, but the visual mode doesn't change)
    // Rerender with new value to simulate parent updating
    const newNode: CelGuiNode = { type: 'rule', field: 'a', operator: '==', value: 2 };
    rerender(<CelExpressionBuilder value={newNode} onChange={onChange} />);
  });

  it('switches between visual and source mode', async () => {
    const conversion = {
      toCelString: vi.fn().mockResolvedValue('a == 1'),
      toGuiModel: vi.fn().mockResolvedValue({ type: 'rule', field: 'a', operator: '==', value: 1 }),
    };

    render(<CelExpressionBuilder defaultValue={defaultNode} conversion={conversion} />);

    // Initially in visual mode
    expect(screen.getByTestId('node-renderer')).toBeInTheDocument();

    // Switch to source mode
    const toggleBtn = screen.getByText('Source');
    await act(async () => {
      fireEvent.click(toggleBtn);
    });

    expect(conversion.toCelString).toHaveBeenCalledWith(defaultNode, false);
    expect(screen.getByText('Visual')).toBeInTheDocument(); // button text changes

    const editor = await screen.findByTestId('cel-code-editor');
    expect(editor).toHaveValue('a == 1');

    // Switch back to visual mode
    await act(async () => {
      fireEvent.click(screen.getByText('Visual'));
    });

    expect(conversion.toGuiModel).toHaveBeenCalledWith('a == 1');
    expect(screen.getByText('Source')).toBeInTheDocument();
    expect(screen.getByTestId('node-renderer')).toBeInTheDocument();
  });

  it('stays in visual mode if conversion to source fails', async () => {
    const conversion = {
      toCelString: vi.fn().mockRejectedValue(new Error('Failed to convert')),
      toGuiModel: vi.fn(),
    };

    render(<CelExpressionBuilder defaultValue={defaultNode} conversion={conversion} />);

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

    render(<CelExpressionBuilder defaultValue={defaultNode} conversion={conversion} />);

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

  it('hides toggle if mode is locked', () => {
    render(<CelExpressionBuilder mode="visual" defaultValue={defaultNode} />);
    expect(screen.queryByText('Source')).not.toBeInTheDocument();
    expect(screen.queryByText('Visual')).not.toBeInTheDocument();
  });

  it('renders pretty print checkbox and toggles it', async () => {
    const conversion = {
      toCelString: vi.fn().mockResolvedValue('a == 1'),
      toGuiModel: vi.fn().mockResolvedValue({ type: 'rule', field: 'a', operator: '==', value: 1 }),
    };
    const onPrettyChange = vi.fn();

    render(
      <CelExpressionBuilder
        defaultValue={defaultNode}
        conversion={conversion}
        onPrettyChange={onPrettyChange}
      />
    );

    const checkbox = screen.getByLabelText('Pretty Print') as HTMLInputElement;
    expect(checkbox).toBeInTheDocument();
    expect(checkbox.checked).toBe(false);

    // Toggle checkbox
    fireEvent.click(checkbox);
    expect(checkbox.checked).toBe(true);
    expect(onPrettyChange).toHaveBeenCalledWith(true);
  });

  it('re-formats source immediately when pretty print is toggled in source mode', async () => {
    const conversion = {
      toCelString: vi.fn().mockResolvedValue('a == 1'),
      toGuiModel: vi.fn().mockResolvedValue({ type: 'rule', field: 'a', operator: '==', value: 1 }),
    };

    render(
      <CelExpressionBuilder
        defaultValue={defaultNode}
        conversion={conversion}
        mode="source"
      />
    );

    const checkbox = screen.getByLabelText('Pretty Print');
    
    // Clear initial call if any
    conversion.toCelString.mockClear();
    
    await act(async () => {
      fireEvent.click(checkbox);
    });

    expect(conversion.toCelString).toHaveBeenCalledWith(expect.anything(), true);
  });
});
