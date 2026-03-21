import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { CelSchemaProvider, useCelSchema } from '../context/CelSchemaContext.tsx';

const TestComponent = () => {
  const schema = useCelSchema();
  return (
    <div>
      <span data-testid="schema-fields">
        {schema?.fields?.length || 0}
      </span>
    </div>
  );
};

describe('CelSchemaProvider', () => {
  it('provides schema to descendants', () => {
    const schema = {
      fields: [{ name: 'test', type: 'string' as const }],
    };

    render(
      <CelSchemaProvider schema={schema}>
        <TestComponent />
      </CelSchemaProvider>
    );

    expect(screen.getByTestId('schema-fields')).toHaveTextContent('1');
  });

  it('provides undefined if no schema provided', () => {
    render(
      <CelSchemaProvider>
        <TestComponent />
      </CelSchemaProvider>
    );

    expect(screen.getByTestId('schema-fields')).toHaveTextContent('0');
  });
});
