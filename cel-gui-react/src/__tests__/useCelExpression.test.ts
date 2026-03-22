import { renderHook, act } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { useCelExpression } from '../hooks/useCelExpression.ts';
import { CelGuiFilterRoot, CelGuiValueRoot } from '../types.ts';

describe('useCelExpression', () => {
  const defaultFilterNode: CelGuiFilterRoot = {
    kind: 'filter',
    root: { type: 'rule', field: 'a', operator: '==', value: 1 },
  };

  it('initializes with default filter expression root', () => {
    const { result } = renderHook(() =>
      useCelExpression({
        defaultValue: defaultFilterNode,
        defaultSource: 'a == 1',
        defaultMode: 'visual',
      })
    );

    expect(result.current.node).toEqual(defaultFilterNode);
    expect(result.current.source).toBe('a == 1');
    expect(result.current.mode).toBe('visual');
    expect(result.current.isDirty).toBe(false);
  });

  it('initializes with default value expression root', () => {
    const defaultValueNode: CelGuiValueRoot = {
      kind: 'value',
      resultType: 'string',
      root: { type: 'field-ref', field: 'user.name' },
    };
    const { result } = renderHook(() => useCelExpression({ defaultValue: defaultValueNode }));
    expect(result.current.node).toEqual(defaultValueNode);
    expect(result.current.isDirty).toBe(false);
  });

  it('updates node and sets isDirty', () => {
    const { result } = renderHook(() => useCelExpression());
    const newNode: CelGuiFilterRoot = {
      kind: 'filter',
      root: { type: 'rule', field: 'b', operator: '!=', value: 2 },
    };

    act(() => {
      result.current.setNode(newNode);
    });

    expect(result.current.node).toEqual(newNode);
    expect(result.current.isDirty).toBe(true);
  });

  it('updates source and sets isDirty', () => {
    const { result } = renderHook(() => useCelExpression());

    act(() => {
      result.current.setSource('b != 2');
    });

    expect(result.current.source).toBe('b != 2');
    expect(result.current.isDirty).toBe(true);
  });

  it('toggles mode between auto and source', () => {
    const { result } = renderHook(() => useCelExpression({ defaultMode: 'auto' }));

    act(() => {
      result.current.toggleMode();
    });
    expect(result.current.mode).toBe('source');

    act(() => {
      result.current.toggleMode();
    });
    expect(result.current.mode).toBe('auto');
  });

  it('resets dirty state', () => {
    const { result } = renderHook(() => useCelExpression());

    act(() => {
      result.current.setSource('test');
    });
    expect(result.current.isDirty).toBe(true);

    act(() => {
      result.current.resetDirty();
    });
    expect(result.current.isDirty).toBe(false);
  });
});
