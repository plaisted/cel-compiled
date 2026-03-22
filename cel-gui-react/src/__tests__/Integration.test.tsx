import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CelExpressionBuilder } from '../components/CelExpressionBuilder.tsx';
import { CelGuiGroup, CelGuiFilterRoot, CelGuiValueRoot } from '../types.ts';

// Mock the lazy-loaded editor to avoid CM6 issues in jsdom
vi.mock('../editor/CelCodeEditor.tsx', () => {
  return {
    default: function MockEditor({ value, onChange }: any) {
      return (
        <textarea
          data-testid="cel-code-editor"
          value={value}
          onChange={(e) => onChange(e.target.value)}
        />
      );
    },
  };
});

// Mock ChipValueComposer for integration tests that don't need full chip rendering
vi.mock('../components/ChipValueComposer.tsx', () => ({
  ChipValueComposer: ({ onChange, node, resultType }: any) => (
    <div data-testid="chip-value-composer" data-result-type={resultType}>
      <span>{node?.type ?? 'empty'}</span>
      <button onClick={() => onChange({ type: 'field-ref', field: 'user.name' })}>
        Update Value
      </button>
    </div>
  ),
}));

describe('Integration Test: Expression Building', () => {
  const schema = {
    fields: [
      { name: 'user.name', type: 'string' as const },
      { name: 'user.age', type: 'number' as const },
      { name: 'user.isActive', type: 'boolean' as const },
    ],
  };

  it('renders empty state when no defaultValue is given', () => {
    render(<CelExpressionBuilder schema={schema} />);
    expect(screen.getByText('No expression')).toBeInTheDocument();
  });

  // ── Value builder integration ───────────────────────────────────────────────

  describe('Value builder — kind="value"', () => {
    it('initializes an empty value builder from resultType prop alone', () => {
      render(
        <CelExpressionBuilder kind="value" resultType="number" schema={schema} />
      );
      const composer = screen.getByTestId('chip-value-composer');
      expect(composer).toBeInTheDocument();
      expect(composer.dataset.resultType).toBe('number');
    });

    it('initializes a value builder from a provided defaultValue', () => {
      const defaultValue: CelGuiValueRoot = {
        kind: 'value',
        resultType: 'string',
        root: { type: 'field-ref', field: 'user.name' },
      };
      render(
        <CelExpressionBuilder kind="value" defaultValue={defaultValue} schema={schema} />
      );
      const composer = screen.getByTestId('chip-value-composer');
      expect(composer.dataset.resultType).toBe('string');
      expect(screen.getByText('field-ref')).toBeInTheDocument();
    });

    it('calls onChange with value root when chip composer emits a change', () => {
      const onChange = vi.fn();
      const defaultValue: CelGuiValueRoot = {
        kind: 'value',
        resultType: 'string',
        root: { type: 'field-ref', field: 'user.name' },
      };
      render(
        <CelExpressionBuilder kind="value" defaultValue={defaultValue} onChange={onChange} schema={schema} />
      );
      fireEvent.click(screen.getByText('Update Value'));
      expect(onChange).toHaveBeenCalledWith(
        expect.objectContaining({ kind: 'value', resultType: 'string' })
      );
    });

    it('preserves resultType when switching value builder to source and back', async () => {
      const valueRoot: CelGuiValueRoot = {
        kind: 'value',
        resultType: 'number',
        root: { type: 'field-ref', field: 'a' },
      };
      const conversion = {
        toCelString: vi.fn().mockResolvedValue('a'),
        toGuiModel: vi.fn().mockResolvedValue(valueRoot),
      };

      render(
        <CelExpressionBuilder
          kind="value"
          resultType="number"
          defaultValue={valueRoot}
          conversion={conversion}
          schema={schema}
        />
      );

      await act(async () => {
        fireEvent.click(screen.getByText('Source'));
      });

      await act(async () => {
        fireEvent.click(screen.getByText('Visual'));
      });

      // toGuiModel should be called with the numeric result type preserved
      expect(conversion.toGuiModel).toHaveBeenCalledWith('a', 'value', 'number');
    });
  });

  it('builds a complete expression tree from default filter group', async () => {
    const onChange = vi.fn();
    const defaultGroup: CelGuiGroup = {
      type: 'group',
      combinator: 'and',
      not: false,
      rules: [],
    };
    const defaultFilterRoot: CelGuiFilterRoot = { kind: 'filter', root: defaultGroup };

    render(
      <CelExpressionBuilder kind="filter" schema={schema} defaultValue={defaultFilterRoot} onChange={onChange} />
    );

    // Add a rule
    fireEvent.click(screen.getByRole('button', { name: 'Add condition' }));
    expect(onChange).toHaveBeenCalled();

    // onChange now emits CelGuiFilterRoot — unwrap the root
    const lastCallExpr = onChange.mock.calls[onChange.mock.calls.length - 1][0] as CelGuiFilterRoot;
    expect(lastCallExpr.kind).toBe('filter');
    const lastCallNode = lastCallExpr.root as CelGuiGroup;
    expect(lastCallNode.rules).toHaveLength(1);
    expect(lastCallNode.rules[0].type).toBe('rule');

    const selects = screen.getAllByRole('combobox');
    const ruleFieldSelect = selects[0];
    fireEvent.change(ruleFieldSelect, { target: { value: 'user.age' } });

    const ruleOperatorSelect = screen.getAllByRole('combobox')[1];
    fireEvent.change(ruleOperatorSelect, { target: { value: '>=' } });

    const ruleValueInput = screen.getByPlaceholderText('Enter number...');
    fireEvent.change(ruleValueInput, { target: { value: '18' } });

    // Promote the condition into a nested group
    fireEvent.click(screen.getByRole('button', { name: 'Create group from condition' }));

    // Check final emitted structure
    const finalExpr = onChange.mock.calls[onChange.mock.calls.length - 1][0] as CelGuiFilterRoot;
    const finalNode = finalExpr.root as CelGuiGroup;

    expect(finalNode.type).toBe('group');
    expect(finalNode.combinator).toBe('and');
    expect(finalNode.not).toBe(false);
    expect(finalNode.rules).toHaveLength(1);

    const promotedGroup = finalNode.rules[0] as CelGuiGroup;
    expect(promotedGroup.type).toBe('group');
    expect(promotedGroup.combinator).toBe('and');
    expect(promotedGroup.not).toBe(false);
    expect(promotedGroup.rules[0]).toMatchObject({
      type: 'rule',
      field: 'user.age',
      operator: '>=',
      value: 18,
    });

    // Serialize to JSON
    const serialized = JSON.stringify(finalNode);
    const deserialized = JSON.parse(serialized);

    expect(deserialized.type).toBe('group');
    expect(deserialized.rules[0].rules[0].field).toBe('user.age');
    expect(deserialized.rules[0].rules[0].value).toBe(18);
  });
});
