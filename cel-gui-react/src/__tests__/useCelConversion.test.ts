import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { useCelConversion } from '../hooks/useCelConversion.ts';
import { CelGuiFilterRoot, CelGuiExpressionNode } from '../types.ts';

describe('useCelConversion', () => {
  const filterNode: CelGuiFilterRoot = {
    kind: 'filter',
    root: { type: 'rule', field: 'a', operator: '==', value: 1 },
  };

  it('wraps callbacks and tracks isConverting', async () => {
    const toCelString = vi.fn().mockImplementation(async (_node: CelGuiExpressionNode) => {
      return new Promise((resolve) => setTimeout(() => resolve('a == 1'), 10));
    });

    const { result } = renderHook(() => useCelConversion({ toCelString, toGuiModel: vi.fn() }));

    let promise!: Promise<string>;
    act(() => {
      promise = result.current.convertToSource(filterNode);
    });

    expect(result.current.isConverting).toBe(true);

    const source = await act(async () => await promise);

    expect(source).toBe('a == 1');
    expect(result.current.isConverting).toBe(false);
    expect(toCelString).toHaveBeenCalledWith(filterNode, undefined);
  });

  it('surfaces errors during conversion', async () => {
    const error = new Error('Conversion failed');
    const toGuiModel = vi.fn().mockRejectedValue(error);

    const { result } = renderHook(() => useCelConversion({ toCelString: vi.fn(), toGuiModel }));

    await act(async () => {
      try {
        await result.current.convertToGui('invalid');
      } catch (e) {
        // Expected
      }
    });

    expect(result.current.error).toBe(error);
    expect(result.current.isConverting).toBe(false);
  });

  it('handles missing callbacks gracefully', async () => {
    const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const { result } = renderHook(() => useCelConversion());

    const source = await act(async () => await result.current.convertToSource(filterNode));
    expect(source).toBe('');
    expect(consoleWarnSpy).toHaveBeenCalledWith('useCelConversion: toCelString is not configured');

    const node = await act(async () => await result.current.convertToGui('a == 1'));
    expect(node).toBeNull();
    expect(consoleWarnSpy).toHaveBeenCalledWith('useCelConversion: toGuiModel is not configured');

    consoleWarnSpy.mockRestore();
  });

  it('prunes incomplete blank filter rules before converting to source', async () => {
    const toCelString = vi.fn().mockResolvedValue('a == 1');
    const { result } = renderHook(() => useCelConversion({ toCelString, toGuiModel: vi.fn() }));

    const node: CelGuiFilterRoot = {
      kind: 'filter',
      root: {
        type: 'group',
        combinator: 'and',
        not: false,
        rules: [
          { type: 'rule', field: 'a', operator: '==', value: 1 },
          { type: 'rule', field: '', operator: '==', value: '' },
        ],
      },
    };

    const source = await act(async () => await result.current.convertToSource(node));

    expect(source).toBe('a == 1');
    expect(toCelString).toHaveBeenCalledWith(
      {
        kind: 'filter',
        root: {
          type: 'group',
          combinator: 'and',
          not: false,
          rules: [{ type: 'rule', field: 'a', operator: '==', value: 1 }],
        },
      },
      undefined
    );
  });

  it('returns an empty string when filter root collapses after sanitizing', async () => {
    const toCelString = vi.fn();
    const { result } = renderHook(() => useCelConversion({ toCelString, toGuiModel: vi.fn() }));

    const node: CelGuiFilterRoot = {
      kind: 'filter',
      root: { type: 'rule', field: '', operator: '==', value: '' },
    };

    const source = await act(async () => await result.current.convertToSource(node));

    expect(source).toBe('');
    expect(toCelString).not.toHaveBeenCalled();
  });
});
