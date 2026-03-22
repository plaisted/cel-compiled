import React, { Suspense, useCallback, useMemo } from 'react';
import { CelExpressionBuilderProps, CelBuilderMode, CelGuiExpressionNode, CelValueType } from '../types.ts';
import { useCelExpression } from '../hooks/useCelExpression.ts';
import { useCelConversion } from '../hooks/useCelConversion.ts';
import { CelSchemaProvider } from '../context/CelSchemaContext.tsx';
import { CelBuilderProvider } from '../context/CelBuilderContext.tsx';
import { NodeRenderer } from './NodeRenderer.tsx';
import { ChipValueComposer } from './ChipValueComposer.tsx';
import { buildCelRootStyle } from './builderStyles.ts';

const CelCodeEditor = React.lazy(() => import('../editor/CelCodeEditor.tsx'));

export const CelExpressionBuilder: React.FC<CelExpressionBuilderProps> = ({
  kind = 'value',
  resultType: resultTypeProp,
  defaultValue,
  value,
  onChange,
  onSourceChange,
  onModeChange,
  onPrettyChange,
  editorMode: editorModeProp,
  pretty: prettyProp,
  readOnly,
  conversion,
  schema,
  errors,
  className,
  style,
  theme,
}) => {
  // When kind="value" and no defaultValue is provided, seed an empty value root
  // so the composer can render the "Add value" state with the correct result type.
  const effectiveResultType: CelValueType = resultTypeProp ?? 'string';
  const seedDefaultValue = useMemo((): CelGuiExpressionNode | undefined => {
    if (defaultValue) return defaultValue;
    if (kind === 'value') {
      return { kind: 'value', resultType: effectiveResultType, root: { type: 'advanced-value', expression: '' } };
    }
    return undefined;
  }, []); // eslint-disable-line react-hooks/exhaustive-deps -- intentionally stable seed

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
    defaultValue: seedDefaultValue,
    defaultMode: editorModeProp ?? 'auto',
    defaultPretty: prettyProp ?? false,
  });

  const { convertToSource, convertToGui, isConverting, error: conversionError, resetError } =
    useCelConversion(conversion);

  const isControlled = value !== undefined;
  const currentNode = isControlled ? value : internalNode;
  const currentMode = editorModeProp ?? internalMode;
  const currentPretty = prettyProp ?? internalPretty;

  const rootStyle = useMemo(() => buildCelRootStyle(theme, style), [style, theme]);

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
    (newNode: CelGuiExpressionNode) => {
      if (conversionError) resetError();
      if (!isControlled) setInternalNode(newNode);
      onChange?.(newNode);
    },
    [isControlled, onChange, setInternalNode, conversionError, resetError]
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
        const exprKind = currentNode?.kind ?? kind;
        const resultType =
          currentNode?.kind === 'value' ? currentNode.resultType : effectiveResultType;

        if (exprKind === 'value' && source.trim() === '') {
          handleNodeChange({
            kind: 'value',
            resultType,
            root: { type: 'advanced-value', expression: '' },
          });
          setMode('auto');
          return;
        }

        try {
          const node = await convertToGui(source, exprKind, exprKind === 'value' ? resultType : undefined);
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
    kind,
    effectiveResultType,
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
  const showToggle = !editorModeProp || editorModeProp === 'auto';

  // Render the visual editor depending on expression kind:
  // - filter → existing rule/group/macro/advanced tree (unchanged)
  // - value  → inline chip composer backed by the typed value-expression model
  const renderVisualTree = () => {
    if (currentNode?.kind === 'value') {
      return (
        <ChipValueComposer
          node={currentNode.root}
          resultType={currentNode.resultType}
          onChange={(newRoot) =>
            handleNodeChange({ kind: 'value', resultType: currentNode.resultType, root: newRoot })
          }
          errors={errors}
        />
      );
    }

    if (currentNode?.kind === 'filter') {
      return (
        <NodeRenderer
          node={currentNode.root}
          onChange={(newRoot) => handleNodeChange({ kind: 'filter', root: newRoot })}
        />
      );
    }

    return <div className="cel-builder__empty">No expression</div>;
  };

  return (
    <CelSchemaProvider schema={schema}>
      <CelBuilderProvider readOnly={readOnly}>
        <div className={rootClassName} style={rootStyle}>
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
                  onChange={(text) => {
                    if (conversionError) resetError();
                    setSource(text);
                  }}
                  readOnly={readOnly}
                  schema={schema}
                  errors={errors}
                  className="cel-builder__editor"
                />
              </Suspense>
            ) : (
              renderVisualTree()
            )}
          </div>
        </div>
      </CelBuilderProvider>
    </CelSchemaProvider>
  );
};
