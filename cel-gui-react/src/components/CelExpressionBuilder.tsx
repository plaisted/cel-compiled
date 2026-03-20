import React, { Suspense, useCallback, useMemo } from 'react';
import { CelExpressionBuilderProps, CelBuilderMode, CelGuiNode, CelThemeTokens } from '../types.ts';
import { useCelExpression } from '../hooks/useCelExpression.ts';
import { useCelConversion } from '../hooks/useCelConversion.ts';
import { CelSchemaProvider } from '../context/CelSchemaContext.tsx';
import { CelBuilderProvider } from '../context/CelBuilderContext.tsx';
import { NodeRenderer } from './NodeRenderer.tsx';

const CelCodeEditor = React.lazy(() => import('../editor/CelCodeEditor.tsx'));

const THEME_TOKEN_TO_CSS_VAR: Record<keyof CelThemeTokens, string> = {
  surface: '--cel-surface',
  surfaceLow: '--cel-surface-low',
  surfaceMid: '--cel-surface-mid',
  surfaceHigh: '--cel-surface-high',
  surfaceHighest: '--cel-surface-highest',
  surfaceCard: '--cel-surface-card',
  surfaceCardSolid: '--cel-surface-card-solid',
  text: '--cel-text',
  textMuted: '--cel-text-muted',
  textSoft: '--cel-text-soft',
  outline: '--cel-outline',
  outlineStrong: '--cel-outline-strong',
  primary: '--cel-primary',
  primaryDim: '--cel-primary-dim',
  primarySoft: '--cel-primary-soft',
  secondary: '--cel-secondary',
  secondarySoft: '--cel-secondary-soft',
  tertiary: '--cel-tertiary',
  danger: '--cel-danger',
  dangerSoft: '--cel-danger-soft',
  success: '--cel-success',
  inverseSurface: '--cel-inverse-surface',
  inversePrimary: '--cel-inverse-primary',
  radiusSm: '--cel-radius-sm',
  radius: '--cel-radius',
  radiusMd: '--cel-radius-md',
  shadowAmbient: '--cel-shadow-ambient',
  shadowSoft: '--cel-shadow-soft',
  transition: '--cel-transition',
  ring: '--cel-ring',
};

export const CelExpressionBuilder: React.FC<CelExpressionBuilderProps> = ({
  defaultValue,
  value,
  onChange,
  onSourceChange,
  onModeChange,
  onPrettyChange,
  mode: modeProp,
  pretty: prettyProp,
  readOnly,
  conversion,
  schema,
  errors,
  className,
  style,
  theme,
}) => {
  const {
    node: internalNode,
    source,
    mode: internalMode,
    pretty: internalPretty,
    setNode: setInternalNode,
    setSource: setInternalSource,
    setMode: setInternalMode,
    setPretty: setInternalPretty,
  } = useCelExpression({
    defaultValue,
    defaultMode: modeProp ?? 'auto',
    defaultPretty: prettyProp ?? false,
  });

  const { convertToSource, convertToGui, isConverting, error: conversionError } =
    useCelConversion(conversion);

  const isControlled = value !== undefined;
  const currentNode = isControlled ? value : internalNode;
  const currentMode = modeProp ?? internalMode;
  const currentPretty = prettyProp ?? internalPretty;

  const rootStyle = useMemo(() => {
    const themeStyle: React.CSSProperties & Record<string, string> = {};
    if (theme) {
      for (const [token, value] of Object.entries(theme) as [keyof CelThemeTokens, string][]) {
        if (value === undefined) continue;
        themeStyle[THEME_TOKEN_TO_CSS_VAR[token]] = value;
      }
    }
    return { ...themeStyle, ...style };
  }, [style, theme]);

  const rootClassName = ['cel-builder', 'cel-builder--natural', className]
    .filter(Boolean)
    .join(' ');

  const setSource = useCallback(
    (text: string) => {
      setInternalSource(text);
      onSourceChange?.(text);
    },
    [setInternalSource, onSourceChange]
  );

  const setMode = useCallback(
    (mode: CelBuilderMode) => {
      setInternalMode(mode);
      onModeChange?.(mode);
    },
    [setInternalMode, onModeChange]
  );

  const setPretty = useCallback(
    (pretty: boolean) => {
      setInternalPretty(pretty);
      onPrettyChange?.(pretty);
    },
    [setInternalPretty, onPrettyChange]
  );

  const handleNodeChange = useCallback(
    (newNode: CelGuiNode) => {
      if (!isControlled) setInternalNode(newNode);
      onChange?.(newNode);
    },
    [isControlled, onChange, setInternalNode]
  );

  const handleToggleMode = useCallback(async () => {
    if (currentMode !== 'source') {
      // visual/auto → source: convert node to CEL text first
      if (currentNode && conversion) {
        try {
          const text = await convertToSource(currentNode, currentPretty);
          setSource(text);
        } catch {
          return; // stay in visual on conversion error
        }
      }
      setMode('source');
    } else {
      // source → auto: parse CEL text back to node
      if (conversion) {
        try {
          const node = await convertToGui(source);
          if (node) handleNodeChange(node);
        } catch {
          return; // stay in source on parse error
        }
      }
      setMode('auto');
    }
  }, [
    currentMode,
    currentNode,
    currentPretty,
    source,
    conversion,
    convertToSource,
    convertToGui,
    setSource,
    setMode,
    handleNodeChange,
  ]);

  const handlePrettyToggle = useCallback(async (e: React.ChangeEvent<HTMLInputElement>) => {
    const newValue = e.target.checked;
    setPretty(newValue);
    
    // If in source mode, re-format immediately if possible
    if (currentMode === 'source' && currentNode && conversion) {
      try {
        const text = await convertToSource(currentNode, newValue);
        setSource(text);
      } catch {
        // ignore formatting errors
      }
    }
  }, [currentMode, currentNode, conversion, convertToSource, setSource, setPretty]);

  // Show toggle unless the consumer has locked the mode to visual or source
  const showToggle = !modeProp || modeProp === 'auto';

  return (
    <CelSchemaProvider schema={schema}>
      <CelBuilderProvider readOnly={readOnly}>
        <div className={rootClassName} style={rootStyle}>
          <div className="cel-builder__header">
            <div className="cel-builder__header-copy">
              <h2 className="cel-builder__title">Expression Builder</h2>
              <p className="cel-builder__subtitle">Construct high-performance CEL filters.</p>
            </div>
            <div className="cel-builder__toolbar">
              <div className="cel-builder__toolbar-group">
                <label className="cel-builder__pretty-toggle">
                  <input
                    type="checkbox"
                    checked={currentPretty}
                    onChange={handlePrettyToggle}
                    disabled={isConverting}
                  />
                  <span>Pretty Print</span>
                </label>
              </div>
              {showToggle && (
                <button
                  type="button"
                  className="cel-builder__mode-toggle"
                  aria-label={
                    currentMode === 'source'
                      ? 'Switch to visual editor'
                      : 'Switch to source code editor'
                  }
                  onClick={handleToggleMode}
                  disabled={isConverting}
                >
                  {currentMode === 'source' ? 'Visual' : 'Source'}
                </button>
              )}
            </div>
          </div>

          {isConverting && (
            <div className="cel-builder__converting" aria-live="polite" role="status">
              Converting...
            </div>
          )}
          {conversionError && (
            <div className="cel-builder__error" aria-live="polite" role="status">
              {conversionError.message}
            </div>
          )}

          <div className="cel-builder__content">
            {currentMode === 'source' ? (
              <Suspense fallback={<div className="cel-builder__loading">Loading editor...</div>}>
                <CelCodeEditor
                  value={source}
                  onChange={setSource}
                  readOnly={readOnly}
                  schema={schema}
                  errors={errors}
                  className="cel-builder__editor"
                />
              </Suspense>
            ) : currentNode ? (
              <NodeRenderer node={currentNode} onChange={handleNodeChange} />
            ) : (
              <div className="cel-builder__empty">No expression</div>
            )}
          </div>
        </div>
      </CelBuilderProvider>
    </CelSchemaProvider>
  );
};
