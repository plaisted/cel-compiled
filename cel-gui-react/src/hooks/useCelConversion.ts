import { useState, useCallback, useRef } from 'react';
import { CelGuiExpressionNode, CelConversionOptions } from '../types.ts';
import { sanitizeExpressionForConversion } from '../utils/sanitizeNode.ts';

export function useCelConversion(options?: CelConversionOptions) {
  const [isConverting, setIsConverting] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  // Hold the latest callbacks in refs so the returned functions are stable
  // regardless of whether the consumer memoizes the options object.
  const toCelStringRef = useRef(options?.toCelString);
  toCelStringRef.current = options?.toCelString;
  const toGuiModelRef = useRef(options?.toGuiModel);
  toGuiModelRef.current = options?.toGuiModel;

  // Track concurrent calls so isConverting only clears when all are done.
  const pendingRef = useRef(0);

  const convertToSource = useCallback(async (node: CelGuiExpressionNode, pretty?: boolean) => {
    if (!toCelStringRef.current) {
      console.warn('useCelConversion: toCelString is not configured');
      return '';
    }
    const sanitized = sanitizeExpressionForConversion(node);
    if (!sanitized) {
      return '';
    }
    if (++pendingRef.current === 1) setIsConverting(true);
    setError(null);
    try {
      return await toCelStringRef.current(sanitized, pretty);
    } catch (e) {
      const err = e instanceof Error ? e : new Error(String(e));
      setError(err);
      throw err;
    } finally {
      if (--pendingRef.current === 0) setIsConverting(false);
    }
  }, []);

  const convertToGui = useCallback(async (source: string, kind?: 'filter' | 'value', resultType?: string) => {
    if (!toGuiModelRef.current) {
      console.warn('useCelConversion: toGuiModel is not configured');
      return null;
    }
    if (++pendingRef.current === 1) setIsConverting(true);
    setError(null);
    try {
      return await toGuiModelRef.current(source, kind, resultType as any);
    } catch (e) {
      const err = e instanceof Error ? e : new Error(String(e));
      setError(err);
      throw err;
    } finally {
      if (--pendingRef.current === 0) setIsConverting(false);
    }
  }, []);

  return {
    convertToSource,
    convertToGui,
    isConverting,
    error,
  };
}
