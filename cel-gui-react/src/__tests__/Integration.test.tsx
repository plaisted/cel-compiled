import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CelExpressionBuilder } from '../components/CelExpressionBuilder.tsx';
import { CelGuiGroup } from '../types.ts';

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

  it('builds a complete expression tree from default group', async () => {
    const onChange = vi.fn();
    const defaultGroup: CelGuiGroup = {
      type: 'group',
      combinator: 'and',
      not: false,
      rules: [],
    };

    render(<CelExpressionBuilder schema={schema} defaultValue={defaultGroup} onChange={onChange} />);

    // Add a rule
    fireEvent.click(screen.getByRole('button', { name: 'Add condition' }));
    expect(onChange).toHaveBeenCalled();

    // Get the latest node from onChange
    const lastCallNode = onChange.mock.calls[onChange.mock.calls.length - 1][0] as CelGuiGroup;
    expect(lastCallNode.rules).toHaveLength(1);
    expect(lastCallNode.rules[0].type).toBe('rule');

    // Simulate changing the field of the rule
    // We need to re-render with the controlled value to test interaction properly,
    // but CelExpressionBuilder also works uncontrolled. Let's just use DOM interactions
    // since it manages internal state when not fully controlled!
    const selects = screen.getAllByRole('combobox');
    const ruleFieldSelect = selects[0];
    fireEvent.change(ruleFieldSelect, { target: { value: 'user.age' } });

    const ruleOperatorSelect = screen.getAllByRole('combobox')[1];
    fireEvent.change(ruleOperatorSelect, { target: { value: '>=' } });

    const ruleValueInput = screen.getByPlaceholderText('Enter number...');
    fireEvent.change(ruleValueInput, { target: { value: '18' } });

    // Promote the condition into a nested group that preserves the condition
    fireEvent.click(screen.getByRole('button', { name: 'Create group from condition' }));

    // Check final emitted structure
    const finalNode = onChange.mock.calls[onChange.mock.calls.length - 1][0] as CelGuiGroup;

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

    // Serialize to JSON to ensure no cyclic deps and clean contract
    const serialized = JSON.stringify(finalNode);
    const deserialized = JSON.parse(serialized);

    expect(deserialized.type).toBe('group');
    expect(deserialized.rules[0].rules[0].field).toBe('user.age');
    expect(deserialized.rules[0].rules[0].value).toBe(18);
  });
});
