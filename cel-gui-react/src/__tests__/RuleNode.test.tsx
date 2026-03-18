import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { RuleNode } from '../components/RuleNode.tsx';
import { CelGuiRule } from '../types.ts';
import { CelBuilderProvider } from '../context/CelBuilderContext.tsx';
import { CelSchemaProvider } from '../context/CelSchemaContext.tsx';

describe('RuleNode', () => {
  const defaultNode: CelGuiRule = {
    type: 'rule',
    field: 'user.name',
    operator: '==',
    value: 'John',
  };

  const schema = {
    fields: [
      { name: 'user.name', type: 'string' as const },
      { name: 'user.age', type: 'number' as const },
      { name: 'user.isActive', type: 'boolean' as const },
    ],
  };

  it('renders field, operator, and value inputs', () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <CelSchemaProvider schema={schema}>
          <RuleNode node={defaultNode} onChange={onChange} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );

    const selects = screen.getAllByRole('combobox');
    expect(selects).toHaveLength(2); // field, operator
    expect(selects[0]).toHaveValue('user.name');
    expect(selects[1]).toHaveValue('==');
    expect(screen.getByDisplayValue('John')).toBeInTheDocument();
  });

  it('filters operators based on field type (number)', () => {
    const onChange = vi.fn();
    const numNode: CelGuiRule = { ...defaultNode, field: 'user.age', value: 30 };
    render(
      <CelBuilderProvider readOnly={false}>
        <CelSchemaProvider schema={schema}>
          <RuleNode node={numNode} onChange={onChange} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );

    const operatorSelect = screen.getAllByRole('combobox')[1];
    const options = Array.from(operatorSelect.querySelectorAll('option')).map((o) => o.value);
    expect(options).toEqual(['==', '!=', '>', '>=', '<', '<=', 'in']);
  });

  it('filters operators based on field type (boolean)', () => {
    const onChange = vi.fn();
    const boolNode: CelGuiRule = { ...defaultNode, field: 'user.isActive', value: true };
    render(
      <CelBuilderProvider readOnly={false}>
        <CelSchemaProvider schema={schema}>
          <RuleNode node={boolNode} onChange={onChange} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );

    const operatorSelect = screen.getAllByRole('combobox')[1];
    const options = Array.from(operatorSelect.querySelectorAll('option')).map((o) => o.value);
    expect(options).toEqual(['==', '!=']);
  });

  it('adapts value editor for boolean type', () => {
    const onChange = vi.fn();
    const boolNode: CelGuiRule = { ...defaultNode, field: 'user.isActive', value: true };
    render(
      <CelBuilderProvider readOnly={false}>
        <CelSchemaProvider schema={schema}>
          <RuleNode node={boolNode} onChange={onChange} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );

    const checkbox = screen.getByRole('checkbox');
    expect(checkbox).toBeChecked();

    fireEvent.click(checkbox);
    expect(onChange).toHaveBeenCalledWith({ ...boolNode, value: false });
  });

  it('adapts value editor for in operator', () => {
    const onChange = vi.fn();
    const inNode: CelGuiRule = { ...defaultNode, operator: 'in', value: ['John', 'Jane'] };
    render(
      <CelBuilderProvider readOnly={false}>
        <CelSchemaProvider schema={schema}>
          <RuleNode node={inNode} onChange={onChange} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );

    const input = screen.getByDisplayValue('John, Jane');
    expect(input).toBeInTheDocument();

    fireEvent.change(input, { target: { value: 'John, Jane, Doe' } });
    expect(onChange).toHaveBeenCalledWith({ ...inNode, value: ['John', 'Jane', 'Doe'] });
  });

  it('handles field change and resets operator/value', () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <CelSchemaProvider schema={schema}>
          <RuleNode node={defaultNode} onChange={onChange} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );

    const fieldSelect = screen.getAllByRole('combobox')[0];
    fireEvent.change(fieldSelect, { target: { value: 'user.age' } });

    expect(onChange).toHaveBeenCalledWith({ ...defaultNode, field: 'user.age', operator: '==', value: '' });
  });

  it('calls onRemove when remove button is clicked', () => {
    const onRemove = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <CelSchemaProvider schema={schema}>
          <RuleNode node={defaultNode} onChange={vi.fn()} onRemove={onRemove} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );

    fireEvent.click(screen.getByText('×'));
    expect(onRemove).toHaveBeenCalled();
  });
});
