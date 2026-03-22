import React, { useCallback, useMemo } from 'react';
import { CelVisualBuilderProps, CelGuiExpressionNode } from '../types.ts';
import { useCelExpression } from '../hooks/useCelExpression.ts';
import { CelSchemaProvider } from '../context/CelSchemaContext.tsx';
import { CelBuilderProvider } from '../context/CelBuilderContext.tsx';
import { NodeRenderer } from './NodeRenderer.tsx';
import { ChipValueComposer } from './ChipValueComposer.tsx';
import { buildCelRootStyle } from './builderStyles.ts';

export const CelVisualBuilder: React.FC<CelVisualBuilderProps> = ({
  defaultValue,
  value,
  onChange,
  readOnly,
  schema,
  className,
  style,
  theme,
  emptyState,
}) => {
  const { node: internalNode, setNode: setInternalNode } = useCelExpression({
    defaultValue,
    defaultMode: 'visual',
  });

  const isControlled = value !== undefined;
  const currentNode = isControlled ? value : internalNode;

  const rootStyle = useMemo(() => buildCelRootStyle(theme, style), [style, theme]);

  const rootClassName = ['cel-builder', 'cel-builder--natural', className]
    .filter(Boolean)
    .join(' ');

  const handleNodeChange = useCallback(
    (newNode: CelGuiExpressionNode) => {
      if (!isControlled) setInternalNode(newNode);
      onChange?.(newNode);
    },
    [isControlled, onChange, setInternalNode]
  );

  const renderTree = () => {
    if (currentNode?.kind === 'value') {
      return (
        <ChipValueComposer
          node={currentNode.root}
          resultType={currentNode.resultType}
          onChange={(newRoot) =>
            handleNodeChange({ kind: 'value', resultType: currentNode.resultType, root: newRoot })
          }
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

    return <div className="cel-builder__empty">{emptyState ?? 'No expression'}</div>;
  };

  return (
    <CelSchemaProvider schema={schema}>
      <CelBuilderProvider readOnly={readOnly}>
        <div className={rootClassName} style={rootStyle}>
          <div className="cel-builder__content">
            {renderTree()}
          </div>
        </div>
      </CelBuilderProvider>
    </CelSchemaProvider>
  );
};
