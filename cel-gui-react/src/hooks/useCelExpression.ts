import { useState, useCallback } from 'react';
import { CelGuiExpressionNode, CelBuilderMode } from '../types.ts';

export interface UseCelExpressionOptions {
  defaultValue?: CelGuiExpressionNode;
  defaultSource?: string;
  defaultMode?: CelBuilderMode;
  defaultPretty?: boolean;
}

export function useCelExpression({
  defaultValue,
  defaultSource = '',
  defaultMode = 'auto',
  defaultPretty = false,
}: UseCelExpressionOptions = {}) {
  const [node, setNodeState] = useState<CelGuiExpressionNode | undefined>(defaultValue);
  const [source, setSourceState] = useState<string>(defaultSource);
  const [mode, setModeState] = useState<CelBuilderMode>(defaultMode);
  const [pretty, setPrettyState] = useState<boolean>(defaultPretty);
  const [isDirty, setIsDirty] = useState(false);

  const setNode = useCallback((newNode: CelGuiExpressionNode) => {
    setNodeState(newNode);
    setIsDirty(true);
  }, []);

  const setSource = useCallback((newSource: string) => {
    setSourceState(newSource);
    setIsDirty(true);
  }, []);

  const setMode = useCallback((newMode: CelBuilderMode) => {
    setModeState(newMode);
  }, []);

  const setPretty = useCallback((isPretty: boolean) => {
    setPrettyState(isPretty);
  }, []);

  const toggleMode = useCallback(() => {
    setModeState((prev) => (prev === 'source' ? 'auto' : 'source'));
  }, []);

  const resetDirty = useCallback(() => {
    setIsDirty(false);
  }, []);

  return {
    node,
    source,
    mode,
    pretty,
    isDirty,
    setNode,
    setSource,
    setMode,
    setPretty,
    toggleMode,
    resetDirty,
  };
}
