import React, { Suspense, useCallback } from 'react';
import { CelExpressionBuilderProps, CelBuilderMode, CelGuiNode } from '../types.ts';
import { useCelExpression } from '../hooks/useCelExpression.ts';
import { useCelConversion } from '../hooks/useCelConversion.ts';
import { CelSchemaProvider } from '../context/CelSchemaContext.tsx';
import { CelBuilderProvider } from '../context/CelBuilderContext.tsx';
import { NodeRenderer } from './NodeRenderer.tsx';

const CelCodeEditor = React.lazy(() => import('../editor/CelCodeEditor.tsx'));

export const CelExpressionBuilder: React.FC<CelExpressionBuilderProps> = ({
  defaultValue,
  value,
  onChange,
  onSourceChange,
  onModeChange,
  mode: modeProp,
  readOnly,
  conversion,
  schema,
  errors,
  layout = 'standard',
}) => {
  const {
    node: internalNode,
    source,
    mode: internalMode,
    setNode: setInternalNode,
    setSource: setInternalSource,
    setMode: setInternalMode,
  } = useCelExpression({
    defaultValue,
    defaultMode: modeProp ?? 'auto',
  });

  const { convertToSource, convertToGui, isConverting, error: conversionError } =
    useCelConversion(conversion);

  const isControlled = value !== undefined;
  const currentNode = isControlled ? value : internalNode;
  const currentMode = modeProp ?? internalMode;

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
          const text = await convertToSource(currentNode);
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
    source,
    conversion,
    convertToSource,
    convertToGui,
    setSource,
    setMode,
    handleNodeChange,
  ]);

  // Show toggle unless the consumer has locked the mode to visual or source
  const showToggle = !modeProp || modeProp === 'auto';

  return (
    <CelSchemaProvider schema={schema}>
      <CelBuilderProvider readOnly={readOnly} layout={layout}>
        <div className={`cel-builder${layout === 'natural' ? ' cel-builder--natural' : ''}`}>
          <div className="cel-builder__toolbar">
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
