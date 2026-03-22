/**
 * Chip composer tests.
 * Covers empty state, insertion points, picker options, inline editing,
 * chip removal, and grouped constructs (conditional, arithmetic, concat).
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ChipValueComposer } from '../components/ChipValueComposer.tsx';
import {
  CelGuiFieldRefNode,
  CelGuiLiteralNode,
  CelGuiConcatNode,
  CelGuiArithmeticNode,
  CelGuiConditionalNode,
  CelGuiAdvancedValueNode,
} from '../types.ts';
import { CelBuilderProvider } from '../context/CelBuilderContext.tsx';
import { CelSchemaProvider } from '../context/CelSchemaContext.tsx';

// Mock NodeRenderer for conditional predicates
vi.mock('../components/NodeRenderer.tsx', () => ({
  NodeRenderer: ({ onChange }: any) => (
    <div data-testid="filter-node-renderer">
      <button onClick={() => onChange({ type: 'group', combinator: 'and', not: false, rules: [] })}>
        Change Filter
      </button>
    </div>
  ),
}));

function wrap(ui: React.ReactElement) {
  return render(
    <CelBuilderProvider>
      <CelSchemaProvider>{ui}</CelSchemaProvider>
    </CelBuilderProvider>
  );
}

// ── Empty state ────────────────────────────────────────────────────────────────

describe('ChipValueComposer — empty state', () => {
  it('shows "Add value" insertion button when node is null', () => {
    wrap(
      <ChipValueComposer node={null} resultType="string" onChange={vi.fn()} />
    );
    expect(screen.getByRole('button', { name: 'Add value' })).toBeInTheDocument();
  });

  it('shows "Add value" insertion button when node is empty advanced-value', () => {
    const node: CelGuiAdvancedValueNode = { type: 'advanced-value', expression: '' };
    wrap(<ChipValueComposer node={node} resultType="string" onChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Add value' })).toBeInTheDocument();
  });

  it('renders "Empty" text when readOnly and no node', () => {
    render(
      <CelBuilderProvider readOnly>
        <CelSchemaProvider>
          <ChipValueComposer node={null} resultType="string" onChange={vi.fn()} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );
    expect(screen.getByText('Empty')).toBeInTheDocument();
  });
});

// ── Picker options ─────────────────────────────────────────────────────────────

describe('ChipValueComposer — context-aware picker', () => {
  it('opens the picker on insertion button click', () => {
    wrap(
      <ChipValueComposer node={null} resultType="string" onChange={vi.fn()} />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Add value' }));
    expect(screen.getByRole('listbox', { name: 'Insert expression part' })).toBeInTheDocument();
  });

  it('shows concat option for string result type', () => {
    wrap(<ChipValueComposer node={null} resultType="string" onChange={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Add value' }));
    expect(screen.getByText(/concat/i)).toBeInTheDocument();
  });

  it('does not show concat option for number result type', () => {
    wrap(<ChipValueComposer node={null} resultType="number" onChange={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Add value' }));
    expect(screen.queryByText(/concat/i)).not.toBeInTheDocument();
  });

  it('shows arithmetic option for number result type', () => {
    wrap(<ChipValueComposer node={null} resultType="number" onChange={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Add value' }));
    expect(screen.getByText(/arithmetic/i)).toBeInTheDocument();
  });

  it('does not show arithmetic option for string result type', () => {
    wrap(<ChipValueComposer node={null} resultType="string" onChange={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Add value' }));
    expect(screen.queryByText(/arithmetic/i)).not.toBeInTheDocument();
  });

  it('always shows field-ref and conditional options', () => {
    for (const rt of ['string', 'number', 'boolean'] as const) {
      const { unmount } = wrap(
        <ChipValueComposer node={null} resultType={rt} onChange={vi.fn()} />
      );
      fireEvent.click(screen.getByRole('button', { name: 'Add value' }));
      expect(screen.getByText(/field reference/i)).toBeInTheDocument();
      expect(screen.getByText(/if \/ then \/ else/i)).toBeInTheDocument();
      unmount();
    }
  });

  it('closes the picker when Cancel is clicked', () => {
    wrap(<ChipValueComposer node={null} resultType="string" onChange={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Add value' }));
    expect(screen.getByRole('listbox', { name: 'Insert expression part' })).toBeInTheDocument();
    fireEvent.click(screen.getByText('Cancel'));
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });
});

// ── Inline editing ─────────────────────────────────────────────────────────────

describe('ChipValueComposer — inline editing', () => {
  it('renders a field-ref node as a chip with the field name', () => {
    const node: CelGuiFieldRefNode = { type: 'field-ref', field: 'user.name' };
    wrap(<ChipValueComposer node={node} resultType="string" onChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Field: user.name' })).toBeInTheDocument();
  });

  it('clicking a field chip enters inline edit mode', () => {
    const node: CelGuiFieldRefNode = { type: 'field-ref', field: 'user.name' };
    wrap(<ChipValueComposer node={node} resultType="string" onChange={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Field: user.name' }));
    expect(screen.getByPlaceholderText('field.path')).toBeInTheDocument();
  });

  it('editing a field-ref calls onChange with the new field', () => {
    const node: CelGuiFieldRefNode = { type: 'field-ref', field: 'user.name' };
    const onChange = vi.fn();
    wrap(<ChipValueComposer node={node} resultType="string" onChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: 'Field: user.name' }));
    const input = screen.getByPlaceholderText('field.path');
    fireEvent.change(input, { target: { value: 'user.email' } });
    expect(onChange).toHaveBeenCalledWith({ type: 'field-ref', field: 'user.email' });
  });

  it('renders a string literal chip', () => {
    const node: CelGuiLiteralNode = { type: 'literal', value: 'hello', valueType: 'string' };
    wrap(<ChipValueComposer node={node} resultType="string" onChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Value: "hello"' })).toBeInTheDocument();
  });

  it('renders a number literal chip', () => {
    const node: CelGuiLiteralNode = { type: 'literal', value: 42, valueType: 'number' };
    wrap(<ChipValueComposer node={node} resultType="number" onChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Value: 42' })).toBeInTheDocument();
  });

  it('renders an advanced-value chip with nonempty expression', () => {
    const node: CelGuiAdvancedValueNode = { type: 'advanced-value', expression: 'size(items)' };
    wrap(<ChipValueComposer node={node} resultType="number" onChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'CEL: size(items)' })).toBeInTheDocument();
  });
});

// ── Chip removal ───────────────────────────────────────────────────────────────

describe('ChipValueComposer — chip removal', () => {
  it('shows a remove button on a field-ref chip when onRemove is provided', () => {
    const node: CelGuiFieldRefNode = { type: 'field-ref', field: 'user.name' };
    wrap(
      <ChipValueComposer node={node} resultType="string" onChange={vi.fn()} onRemove={vi.fn()} />
    );
    expect(screen.getByRole('button', { name: 'Remove' })).toBeInTheDocument();
  });

  it('calls onRemove when the remove button is clicked', () => {
    const node: CelGuiFieldRefNode = { type: 'field-ref', field: 'user.name' };
    const onRemove = vi.fn();
    wrap(
      <ChipValueComposer node={node} resultType="string" onChange={vi.fn()} onRemove={onRemove} />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Remove' }));
    expect(onRemove).toHaveBeenCalled();
  });
});

// ── Insertion points ───────────────────────────────────────────────────────────

describe('ChipValueComposer — insertion points', () => {
  it('shows insert-before and insert-after buttons for a single field-ref chip', () => {
    const node: CelGuiFieldRefNode = { type: 'field-ref', field: 'user.name' };
    wrap(<ChipValueComposer node={node} resultType="string" onChange={vi.fn()} />);
    const inserts = screen.getAllByRole('button', { name: /insert/i });
    expect(inserts.length).toBeGreaterThanOrEqual(2);
  });

  it('inserting a node after a field-ref promotes root to concat', () => {
    const node: CelGuiFieldRefNode = { type: 'field-ref', field: 'user.first' };
    const onChange = vi.fn();
    wrap(<ChipValueComposer node={node} resultType="string" onChange={onChange} />);
    const insertAfter = screen.getByRole('button', { name: 'Insert after' });
    fireEvent.click(insertAfter);
    fireEvent.click(screen.getByText('Field reference'));
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'concat',
        operands: expect.arrayContaining([expect.objectContaining({ type: 'field-ref' })]),
      })
    );
  });

  it('inserting a node after a numeric field-ref promotes root to arithmetic', () => {
    const node: CelGuiFieldRefNode = { type: 'field-ref', field: 'order.qty' };
    const onChange = vi.fn();
    wrap(<ChipValueComposer node={node} resultType="number" onChange={onChange} />);
    const insertAfter = screen.getByRole('button', { name: 'Insert after' });
    fireEvent.click(insertAfter);
    fireEvent.click(screen.getByText('Field reference'));
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'arithmetic',
        operator: '+',
        left: expect.objectContaining({ type: 'field-ref', field: 'order.qty' }),
        right: expect.objectContaining({ type: 'field-ref' }),
      })
    );
  });

  it('inserting from empty state sets root directly', () => {
    const onChange = vi.fn();
    wrap(<ChipValueComposer node={null} resultType="string" onChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: 'Add value' }));
    fireEvent.click(screen.getByText('Field reference'));
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ type: 'field-ref' }));
  });
});

// ── Concat node ────────────────────────────────────────────────────────────────

describe('ChipValueComposer — concat', () => {
  it('renders concat operands with + separators between them', () => {
    const node: CelGuiConcatNode = {
      type: 'concat',
      operands: [
        { type: 'field-ref', field: 'first' },
        { type: 'field-ref', field: 'last' },
      ],
    };
    wrap(<ChipValueComposer node={node} resultType="string" onChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Field: first' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Field: last' })).toBeInTheDocument();
    // The separator span is present (distinct from insert-point buttons)
    expect(document.querySelector('.cel-chip-separator')).toBeInTheDocument();
  });

  it('removing a concat operand collapses to single node when only one left', () => {
    const node: CelGuiConcatNode = {
      type: 'concat',
      operands: [
        { type: 'field-ref', field: 'a' },
        { type: 'field-ref', field: 'b' },
      ],
    };
    const onChange = vi.fn();
    wrap(<ChipValueComposer node={node} resultType="string" onChange={onChange} />);
    const removes = screen.getAllByRole('button', { name: 'Remove' });
    fireEvent.click(removes[0]);
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ type: 'field-ref', field: 'b' }));
  });
});

// ── Arithmetic node ────────────────────────────────────────────────────────────

describe('ChipValueComposer — arithmetic', () => {
  it('renders arithmetic node with operator chip', () => {
    const node: CelGuiArithmeticNode = {
      type: 'arithmetic',
      operator: '*',
      left: { type: 'field-ref', field: 'qty' },
      right: { type: 'field-ref', field: 'price' },
    };
    wrap(<ChipValueComposer node={node} resultType="number" onChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Field: qty' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Operator: *' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Field: price' })).toBeInTheDocument();
  });

  it('clicking the operator chip opens an operator selector', () => {
    const node: CelGuiArithmeticNode = {
      type: 'arithmetic',
      operator: '+',
      left: { type: 'field-ref', field: 'a' },
      right: { type: 'field-ref', field: 'b' },
    };
    wrap(<ChipValueComposer node={node} resultType="number" onChange={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Operator: +' }));
    expect(screen.getByDisplayValue('+')).toBeInTheDocument();
  });
});

// ── Conditional (grouped construct) ───────────────────────────────────────────

describe('ChipValueComposer — conditional grouped construct', () => {
  it('renders if/then/else keywords', () => {
    const node: CelGuiConditionalNode = {
      type: 'conditional',
      condition: { type: 'group', combinator: 'and', not: false, rules: [] },
      then: { type: 'field-ref', field: 'a' },
      otherwise: { type: 'field-ref', field: 'b' },
    };
    wrap(<ChipValueComposer node={node} resultType="string" onChange={vi.fn()} />);
    expect(screen.getByText('if')).toBeInTheDocument();
    expect(screen.getByText('then')).toBeInTheDocument();
    expect(screen.getByText('else')).toBeInTheDocument();
  });

  it('renders the filter predicate inside the conditional', () => {
    const node: CelGuiConditionalNode = {
      type: 'conditional',
      condition: { type: 'group', combinator: 'and', not: false, rules: [] },
      then: { type: 'field-ref', field: 'a' },
      otherwise: { type: 'field-ref', field: 'b' },
    };
    wrap(<ChipValueComposer node={node} resultType="string" onChange={vi.fn()} />);
    expect(screen.getByTestId('filter-node-renderer')).toBeInTheDocument();
  });

  it('renders the "Add else-if" button', () => {
    const node: CelGuiConditionalNode = {
      type: 'conditional',
      condition: { type: 'group', combinator: 'and', not: false, rules: [] },
      then: { type: 'literal', value: 'A', valueType: 'string' },
      otherwise: { type: 'literal', value: 'B', valueType: 'string' },
    };
    wrap(<ChipValueComposer node={node} resultType="string" onChange={vi.fn()} />);
    expect(screen.getByText(/Add else-if/i)).toBeInTheDocument();
  });

  it('nests a new conditional in otherwise when "Add else-if" is clicked', () => {
    const node: CelGuiConditionalNode = {
      type: 'conditional',
      condition: { type: 'group', combinator: 'and', not: false, rules: [] },
      then: { type: 'literal', value: 'A', valueType: 'string' },
      otherwise: { type: 'literal', value: 'B', valueType: 'string' },
    };
    const onChange = vi.fn();
    wrap(<ChipValueComposer node={node} resultType="string" onChange={onChange} />);
    fireEvent.click(screen.getByText(/Add else-if/i));
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'conditional',
        otherwise: expect.objectContaining({ type: 'conditional' }),
      })
    );
  });
});
