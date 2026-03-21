import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { NodeRenderer } from '../components/NodeRenderer.tsx';
import { CelGuiGroup, CelGuiRule, CelGuiMacro, CelGuiAdvanced } from '../types.ts';

vi.mock('../components/NaturalGroupNode.tsx', () => ({
  NaturalGroupNode: () => <div data-testid="group-node">Group Node</div>,
}));
vi.mock('../components/NaturalRuleNode.tsx', () => ({
  NaturalRuleNode: () => <div data-testid="rule-node">Rule Node</div>,
}));
vi.mock('../components/MacroNode.tsx', () => ({
  MacroNode: () => <div data-testid="macro-node">Macro Node</div>,
}));
vi.mock('../components/AdvancedNode.tsx', () => ({
  AdvancedNode: () => <div data-testid="advanced-node">Advanced Node</div>,
}));

describe('NodeRenderer', () => {
  const onChange = vi.fn();

  it('renders NaturalGroupNode for group type', () => {
    const node: CelGuiGroup = { type: 'group', combinator: 'and', not: false, rules: [] };
    render(<NodeRenderer node={node} onChange={onChange} />);
    expect(screen.getByTestId('group-node')).toBeInTheDocument();
  });

  it('renders NaturalRuleNode for rule type', () => {
    const node: CelGuiRule = { type: 'rule', field: 'a', operator: '==', value: 1 };
    render(<NodeRenderer node={node} onChange={onChange} />);
    expect(screen.getByTestId('rule-node')).toBeInTheDocument();
  });

  it('renders MacroNode for macro type', () => {
    const node: CelGuiMacro = { type: 'macro', macro: 'has', field: 'a' };
    render(<NodeRenderer node={node} onChange={onChange} />);
    expect(screen.getByTestId('macro-node')).toBeInTheDocument();
  });

  it('renders AdvancedNode for advanced type', () => {
    const node: CelGuiAdvanced = { type: 'advanced', expression: 'a == 1' };
    render(<NodeRenderer node={node} onChange={onChange} />);
    expect(screen.getByTestId('advanced-node')).toBeInTheDocument();
  });

  it('renders unknown node type', () => {
    const node = { type: 'unknown' } as any;
    render(<NodeRenderer node={node} onChange={onChange} />);
    expect(screen.getByText('Unknown node type: unknown')).toBeInTheDocument();
  });
});
