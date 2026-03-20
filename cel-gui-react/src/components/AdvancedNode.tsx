import React, { Suspense, useCallback } from 'react';
import { CelGuiNode, CelGuiAdvanced } from '../types.ts';
import { useCelSchema } from '../context/CelSchemaContext.tsx';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { DeleteIcon } from './DeleteIcon.tsx';

const CelCodeEditor = React.lazy(() => import('../editor/CelCodeEditor.tsx'));

export interface AdvancedNodeProps {
  node: CelGuiAdvanced;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
  readOnly?: boolean;
}

export const AdvancedNode: React.FC<AdvancedNodeProps> = ({
  node,
  onChange,
  onRemove,
  readOnly: readOnlyProp,
}) => {
  const schema = useCelSchema();
  const { readOnly: contextReadOnly } = useCelBuilder();
  const readOnly = readOnlyProp ?? contextReadOnly;

  const handleExpressionChange = useCallback(
    (value: string) => {
      onChange({ ...node, expression: value });
    },
    [node, onChange]
  );

  return (
    <div className="cel-advanced">
      <Suspense fallback={<div className="cel-advanced__loading">Loading editor...</div>}>
        <CelCodeEditor
          value={node.expression}
          onChange={handleExpressionChange}
          readOnly={readOnly}
          schema={schema}
          className="cel-advanced__editor"
        />
      </Suspense>

      {!readOnly && onRemove && (
        <button
          type="button"
          className="cel-advanced__remove"
          aria-label="Remove condition"
          onClick={onRemove}
        >
          <DeleteIcon />
        </button>
      )}
    </div>
  );
};
