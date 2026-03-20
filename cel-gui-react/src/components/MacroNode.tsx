import React, { useCallback, useMemo } from 'react';
import { CelFieldDefinition, CelGuiNode, CelGuiMacro } from '../types.ts';
import { useCelSchema } from '../context/CelSchemaContext.tsx';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { flattenFields, groupFields } from '../utils/fieldUtils.ts';
import { DeleteIcon } from './DeleteIcon.tsx';

export interface MacroNodeProps {
  node: CelGuiMacro;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
  readOnly?: boolean;
}

function getFieldTypeBadge(type?: CelFieldDefinition['type']): string {
  switch (type) {
    case 'number':
      return '#';
    case 'boolean':
      return 'T/F';
    case 'string':
      return 'Abc';
    case 'duration':
      return 'Dur';
    case 'timestamp':
      return 'Time';
    case 'bytes':
      return 'Bin';
    case 'list':
      return '[]';
    case 'map':
      return '{}';
    default:
      return '...';
  }
}

function getFieldOptionLabel(field: { label?: string; name: string; type?: CelFieldDefinition['type'] }) {
  const label = field.label || field.name;
  return field.type ? `${label} (${getFieldTypeBadge(field.type)})` : label;
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

  const selectableFields = useMemo(
    () => allFields.filter((field) => !field.children?.length),
    [allFields]
  );

  const groupedFields = useMemo(() => groupFields(selectableFields), [selectableFields]);

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
                {getFieldOptionLabel(f)}
              </option>
            ))}
            {Object.entries(groupedFields.groups).map(([groupName, fields]) => (
              <optgroup key={groupName} label={groupName}>
                {fields.map((f) => (
                  <option key={f.name} value={f.name}>
                    {getFieldOptionLabel(f)}
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
        <button type="button" className="cel-macro__remove" onClick={onRemove} aria-label="Remove condition">
          <DeleteIcon />
        </button>
      )}
    </div>
  );
};
