import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { NaturalGroupNode } from '../components/NaturalGroupNode.tsx';
import { NaturalRuleNode } from '../components/NaturalRuleNode.tsx';
import { CelExpressionBuilder } from '../components/CelExpressionBuilder.tsx';
import { CelGuiGroup, CelGuiRule } from '../types.ts';
import { CelBuilderProvider } from '../context/CelBuilderContext.tsx';
import { CelSchemaProvider } from '../context/CelSchemaContext.tsx';

// Mock the lazy-loaded editor so jsdom doesn't choke on CM6
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

// ─── 7.1  Group ARIA role + aria-label ───────────────────────────────────────

describe('7.1 Group ARIA attributes', () => {
  const mkGroup = (combinator: 'and' | 'or', not: boolean): CelGuiGroup => ({
    type: 'group',
    combinator,
    not,
    rules: [],
  });

  it('AND group has role="group" and aria-label "All of the following conditions"', () => {
    render(
      <CelBuilderProvider>
        <NaturalGroupNode node={mkGroup('and', false)} onChange={vi.fn()} />
      </CelBuilderProvider>
    );
    const group = screen.getByRole('group');
    expect(group).toHaveAttribute('aria-label', 'All of the following conditions');
  });

  it('OR group has aria-label "Any of the following conditions"', () => {
    render(
      <CelBuilderProvider>
        <NaturalGroupNode node={mkGroup('or', false)} onChange={vi.fn()} />
      </CelBuilderProvider>
    );
    expect(screen.getByRole('group')).toHaveAttribute(
      'aria-label',
      'Any of the following conditions'
    );
  });

  it('NOT+AND group has aria-label "None of the following conditions"', () => {
    render(
      <CelBuilderProvider>
        <NaturalGroupNode node={mkGroup('and', true)} onChange={vi.fn()} />
      </CelBuilderProvider>
    );
    expect(screen.getByRole('group')).toHaveAttribute(
      'aria-label',
      'None of the following conditions'
    );
  });

  it('NOT+OR group has aria-label "Not any of the following conditions"', () => {
    render(
      <CelBuilderProvider>
        <NaturalGroupNode node={mkGroup('or', true)} onChange={vi.fn()} />
      </CelBuilderProvider>
    );
    expect(screen.getByRole('group')).toHaveAttribute(
      'aria-label',
      'Not any of the following conditions'
    );
  });
});

// ─── 7.2  Rule aria-label ─────────────────────────────────────────────────────

describe('7.2 Rule aria-label', () => {
  it('shows complete condition label when field is set', () => {
    const node: CelGuiRule = {
      type: 'rule',
      field: 'user.age',
      operator: '>=',
      value: 18,
    };
    render(
      <CelBuilderProvider>
        <CelSchemaProvider>
          <NaturalRuleNode node={node} onChange={vi.fn()} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );
    const rule = document.querySelector('.cel-rule');
    expect(rule).toHaveAttribute('aria-label', 'Condition: user.age is at least 18');
  });

  it('shows "Condition: incomplete" when field is empty', () => {
    const node: CelGuiRule = {
      type: 'rule',
      field: '',
      operator: '==',
      value: '',
    };
    render(
      <CelBuilderProvider>
        <CelSchemaProvider>
          <NaturalRuleNode node={node} onChange={vi.fn()} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );
    const rule = document.querySelector('.cel-rule');
    expect(rule).toHaveAttribute('aria-label', 'Condition: incomplete');
  });

  it('uses operator label in the aria-label', () => {
    const node: CelGuiRule = {
      type: 'rule',
      field: 'user.name',
      operator: 'contains',
      value: 'alice',
    };
    render(
      <CelBuilderProvider>
        <CelSchemaProvider>
          <NaturalRuleNode node={node} onChange={vi.fn()} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );
    const rule = document.querySelector('.cel-rule');
    expect(rule).toHaveAttribute('aria-label', 'Condition: user.name contains alice');
  });
});

// ─── 7.3  Icon-only button aria-labels ───────────────────────────────────────

describe('7.3 Icon-only button aria-labels', () => {
  it('remove condition button has aria-label="Remove condition"', () => {
    const node: CelGuiRule = {
      type: 'rule',
      field: 'x',
      operator: '==',
      value: '1',
    };
    render(
      <CelBuilderProvider>
        <CelSchemaProvider>
          <NaturalRuleNode node={node} onChange={vi.fn()} onRemove={vi.fn()} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );
    expect(screen.getByRole('button', { name: 'Remove condition' })).toBeInTheDocument();
  });

  it('remove group button has aria-label="Remove group"', () => {
    render(
      <CelBuilderProvider>
        <NaturalGroupNode
          node={{ type: 'group', combinator: 'and', not: false, rules: [] }}
          onChange={vi.fn()}
          onRemove={vi.fn()}
        />
      </CelBuilderProvider>
    );
    expect(screen.getByRole('button', { name: 'Remove group' })).toBeInTheDocument();
  });

  it('NOT toggle button has aria-label="Toggle NOT modifier"', () => {
    render(
      <CelBuilderProvider>
        <NaturalGroupNode
          node={{ type: 'group', combinator: 'and', not: false, rules: [] }}
          onChange={vi.fn()}
        />
      </CelBuilderProvider>
    );
    expect(screen.getByRole('switch', { name: 'Toggle NOT modifier' })).toBeInTheDocument();
  });
});

// ─── 7.4  Focus moves to new rule's field selector after add-rule ─────────────

describe('7.4 Focus management after add-rule', () => {
  it('moves focus to the new rule field input after clicking + condition', async () => {
    const defaultGroup: CelGuiGroup = {
      type: 'group',
      combinator: 'and',
      not: false,
      rules: [],
    };
    // No schema → NaturalRuleNode renders a plain text input for field
    render(
      <CelExpressionBuilder defaultValue={defaultGroup} />
    );

    fireEvent.click(screen.getByText('+ condition'));

    await waitFor(() => {
      const fieldInput = screen.getByPlaceholderText('field');
      expect(document.activeElement).toBe(fieldInput);
    });
  });
});

// ─── 7.5  Mode toggle aria-label updates on mode switch ───────────────────────

describe('7.5 Mode toggle aria-label', () => {
  it('shows "Switch to source code editor" in visual mode', () => {
    const defaultGroup: CelGuiGroup = {
      type: 'group',
      combinator: 'and',
      not: false,
      rules: [],
    };
    render(<CelExpressionBuilder defaultValue={defaultGroup} />);

    const toggle = screen.getByRole('button', { name: 'Switch to source code editor' });
    expect(toggle).toBeInTheDocument();
  });

  it('shows "Switch to visual editor" after switching to source mode (no conversion)', async () => {
    const defaultGroup: CelGuiGroup = {
      type: 'group',
      combinator: 'and',
      not: false,
      rules: [],
    };
    render(<CelExpressionBuilder defaultValue={defaultGroup} />);

    // Click to switch to source (no conversion configured — mode switches directly)
    fireEvent.click(screen.getByRole('button', { name: 'Switch to source code editor' }));

    await waitFor(() => {
      expect(
        screen.getByRole('button', { name: 'Switch to visual editor' })
      ).toBeInTheDocument();
    });
  });
});
