import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AdvancedNode } from '../components/AdvancedNode.tsx';
import { CelGuiAdvanced } from '../types.ts';
import { CelBuilderProvider } from '../context/CelBuilderContext.tsx';

// Mock the lazy-loaded editor
vi.mock('../editor/CelCodeEditor.tsx', () => {
  return {
    default: function MockEditor({ value, onChange, readOnly }: any) {
      return (
        <textarea
          data-testid="mock-editor"
          value={value}
          readOnly={readOnly}
          onChange={(e) => onChange(e.target.value)}
        />
      );
    },
  };
});

describe('AdvancedNode', () => {
  const defaultNode: CelGuiAdvanced = {
    type: 'advanced',
    expression: 'a == 1',
  };

  it('renders lazy-loaded editor', async () => {
    const onChange = vi.fn();
    render(
      <CelBuilderProvider readOnly={false}>
        <AdvancedNode node={defaultNode} onChange={onChange} />
      </CelBuilderProvider>
    );

    const editor = await screen.findByTestId('mock-editor');
    expect(editor).toHaveValue('a == 1');
  });
});
