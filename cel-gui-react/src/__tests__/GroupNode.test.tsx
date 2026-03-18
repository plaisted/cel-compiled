import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { GroupNode } from '../components/GroupNode.tsx';
import { CelGuiGroup } from '../types.ts';
import { CelBuilderProvider } from '../context/CelBuilderContext.tsx';

// Mock NodeRenderer so we don't deeply render rules
vi.mock('../components/NodeRenderer.tsx', () => ({
  NodeRenderer: ({ onRemove }: any) => (
    <div data-testid="mock-node-renderer">
      <button onClick={onRemove}>Remove child</button>
    </div>
  ),
}));

describe('GroupNode', () => {
  const defaultNode: CelGuiGroup = {
    type: 'group',
    combinator: 'and',
    not: false,
    rules: [
      { type: 'rule', field: 'a', operator: '==', value: 1 }
    ],
  };

  it('renders combinator and children', () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <GroupNode node={defaultNode} onChange={onChange} />
      </CelBuilderProvider>
    );

    expect(screen.getByRole('combobox')).toHaveValue('and');
    expect(screen.getByTestId('mock-node-renderer')).toBeInTheDocument();
  });

  it('handles combinator change', () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <GroupNode node={defaultNode} onChange={onChange} />
      </CelBuilderProvider>
    );

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'or' } });
    expect(onChange).toHaveBeenCalledWith({ ...defaultNode, combinator: 'or' });
  });

  it('handles NOT toggle', () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <GroupNode node={defaultNode} onChange={onChange} />
      </CelBuilderProvider>
    );

    fireEvent.click(screen.getByText('NOT'));
    expect(onChange).toHaveBeenCalledWith({ ...defaultNode, not: true });
  });

  it('adds a new rule', () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <GroupNode node={defaultNode} onChange={onChange} />
      </CelBuilderProvider>
    );

    fireEvent.click(screen.getByText('Add Rule'));
    expect(onChange).toHaveBeenCalledTimes(1);
    const updatedNode = onChange.mock.calls[0][0] as CelGuiGroup;
    expect(updatedNode.rules).toHaveLength(2);
    expect(updatedNode.rules[1].type).toBe('rule');
  });

  it('adds a new group', () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <GroupNode node={defaultNode} onChange={onChange} />
      </CelBuilderProvider>
    );

    fireEvent.click(screen.getByText('Add Group'));
    expect(onChange).toHaveBeenCalledTimes(1);
    const updatedNode = onChange.mock.calls[0][0] as CelGuiGroup;
    expect(updatedNode.rules).toHaveLength(2);
    expect(updatedNode.rules[1].type).toBe('group');
  });

  it('removes a child rule', () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <GroupNode node={defaultNode} onChange={onChange} />
      </CelBuilderProvider>
    );

    fireEvent.click(screen.getByText('Remove child'));
    expect(onChange).toHaveBeenCalledWith({ ...defaultNode, rules: [] });
  });

  it('calls onRemove when remove button is clicked', () => {
    const onRemove = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <GroupNode node={defaultNode} onChange={vi.fn()} onRemove={onRemove} />
      </CelBuilderProvider>
    );

    fireEvent.click(screen.getByText('Remove Group'));
    expect(onRemove).toHaveBeenCalled();
  });

  it('respects readOnly state', () => {
    render(
      <CelBuilderProvider readOnly={true}>
        <GroupNode node={defaultNode} onChange={vi.fn()} onRemove={vi.fn()} />
      </CelBuilderProvider>
    );

    expect(screen.getByRole('combobox')).toBeDisabled();
    expect(screen.queryByText('NOT')).not.toBeInTheDocument();
    expect(screen.queryByText('Remove Group')).not.toBeInTheDocument();
    expect(screen.queryByText('Add Rule')).not.toBeInTheDocument();
    expect(screen.queryByText('Add Group')).not.toBeInTheDocument();
  });
});
