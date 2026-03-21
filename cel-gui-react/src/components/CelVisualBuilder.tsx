import React, { useCallback, useMemo } from 'react';
import { CelVisualBuilderProps, CelGuiNode } from '../types.ts';
import { useCelExpression } from '../hooks/useCelExpression.ts';
import { CelSchemaProvider } from '../context/CelSchemaContext.tsx';
import { CelBuilderProvider } from '../context/CelBuilderContext.tsx';
import { NodeRenderer } from './NodeRenderer.tsx';
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
    (newNode: CelGuiNode) => {
      if (!isControlled) setInternalNode(newNode);
      onChange?.(newNode);
    },
    [isControlled, onChange, setInternalNode]
  );

  return (
    <CelSchemaProvider schema={schema}>
      <CelBuilderProvider readOnly={readOnly}>
        <div className={rootClassName} style={rootStyle}>
          <div className="cel-builder__content">
            {currentNode ? (
              <NodeRenderer node={currentNode} onChange={handleNodeChange} />
            ) : (
              <div className="cel-builder__empty">{emptyState ?? 'No expression'}</div>
            )}
          </div>
        </div>
      </CelBuilderProvider>
    </CelSchemaProvider>
  );
};
