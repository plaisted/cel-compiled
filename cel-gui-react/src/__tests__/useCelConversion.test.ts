import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { useCelConversion } from '../hooks/useCelConversion.ts';
import { CelGuiNode } from '../types.ts';

describe('useCelConversion', () => {
  it('wraps callbacks and tracks isConverting', async () => {
    const toCelString = vi.fn().mockImplementation(async (node) => {
      return new Promise((resolve) => setTimeout(() => resolve('a == 1'), 10));
    });

    const { result } = renderHook(() => useCelConversion({ toCelString, toGuiModel: vi.fn() }));

    const node: CelGuiNode = { type: 'rule', field: 'a', operator: '==', value: 1 };

    let promise!: Promise<string>;
    act(() => {
      promise = result.current.convertToSource(node);
    });

    // Should be converting while promise is pending
    expect(result.current.isConverting).toBe(true);

    const source = await act(async () => await promise);

    expect(source).toBe('a == 1');
    expect(result.current.isConverting).toBe(false);
    expect(toCelString).toHaveBeenCalledWith(node);
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

    const source = await act(async () => await result.current.convertToSource({ type: 'rule', field: 'a', operator: '==', value: 1 }));
    expect(source).toBe('');
    expect(consoleWarnSpy).toHaveBeenCalledWith('useCelConversion: toCelString is not configured');

    const node = await act(async () => await result.current.convertToGui('a == 1'));
    expect(node).toBeNull();
    expect(consoleWarnSpy).toHaveBeenCalledWith('useCelConversion: toGuiModel is not configured');

    consoleWarnSpy.mockRestore();
  });
});
