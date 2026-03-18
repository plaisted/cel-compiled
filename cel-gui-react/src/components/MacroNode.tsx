import React, { useCallback, useMemo } from 'react';
import { CelGuiNode, CelGuiMacro } from '../types.ts';
import { useCelSchema } from '../context/CelSchemaContext.tsx';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { flattenFields, groupFields } from '../utils/fieldUtils.ts';

export interface MacroNodeProps {
  node: CelGuiMacro;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
  readOnly?: boolean;
}

export const MacroNode: React.FC<MacroNodeProps> = ({
  node,
  onChange,
  onRemove,
  readOnly: readOnlyProp,
}) => {
  const schema = useCelSchema();
  const { readOnly: contextReadOnly } = useCelBuilder();
  const readOnly = readOnlyProp ?? contextReadOnly;

  const allFields = useMemo(
    () => (schema?.fields ? flattenFields(schema.fields) : []),
    [schema]
  );

  const groupedFields = useMemo(() => groupFields(allFields), [allFields]);

  const handleFieldChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement | HTMLInputElement>) => {
      onChange({ ...node, field: e.target.value });
    },
    [node, onChange]
  );

  return (
    <div className="cel-macro">
      <span className="cel-macro__label">{node.macro}</span>
      <div className="cel-macro__argument">
        {schema?.fields ? (
          <select
            value={node.field}
            onChange={handleFieldChange}
            disabled={readOnly}
          >
            <option value="">Select field...</option>
            {groupedFields.ungrouped.map((f) => (
              <option key={f.name} value={f.name}>
                {f.label || f.name}
              </option>
            ))}
            {Object.entries(groupedFields.groups).map(([groupName, fields]) => (
              <optgroup key={groupName} label={groupName}>
                {fields.map((f) => (
                  <option key={f.name} value={f.name}>
                    {f.label || f.name}
                  </option>
                ))}
              </optgroup>
            ))}
          </select>
        ) : (
          <input
            type="text"
            value={node.field}
            onChange={handleFieldChange}
            placeholder="argument"
            disabled={readOnly}
          />
        )}
      </div>

      {!readOnly && onRemove && (
        <button type="button" className="cel-macro__remove" onClick={onRemove}>
          ×
        </button>
      )}
    </div>
  );
};
