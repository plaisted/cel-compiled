import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MacroNode } from '../components/MacroNode.tsx';
import { CelGuiMacro } from '../types.ts';
import { CelBuilderProvider } from '../context/CelBuilderContext.tsx';
import { CelSchemaProvider } from '../context/CelSchemaContext.tsx';

describe('MacroNode', () => {
  const defaultNode: CelGuiMacro = {
    type: 'macro',
    macro: 'has',
    field: 'user.name',
  };

  const schema = {
    fields: [
      { name: 'user.name', type: 'string' as const },
    ],
  };

  it('renders macro label and field select', () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <CelSchemaProvider schema={schema}>
          <MacroNode node={defaultNode} onChange={onChange} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );

    expect(screen.getByText('has')).toBeInTheDocument();
    expect(screen.getByRole('combobox')).toHaveValue('user.name');
  });

  it('handles field change', () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <CelSchemaProvider schema={schema}>
          <MacroNode node={defaultNode} onChange={onChange} />
        </CelSchemaProvider>
      </CelBuilderProvider>
    );

    fireEvent.change(screen.getByRole('combobox'), { target: { value: '' } });
    expect(onChange).toHaveBeenCalledWith({ ...defaultNode, field: '' });
  });

  it('renders input if schema fields are not provided', () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <MacroNode node={defaultNode} onChange={onChange} />
      </CelBuilderProvider>
    );

    expect(screen.getByDisplayValue('user.name')).toBeInTheDocument();
  });
});
