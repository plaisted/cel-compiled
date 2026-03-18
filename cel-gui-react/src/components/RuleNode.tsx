import React, { useCallback, useMemo } from 'react';
import { CelGuiNode, CelGuiRule } from '../types.ts';
import { useCelSchema } from '../context/CelSchemaContext.tsx';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { flattenFields, groupFields } from '../utils/fieldUtils.ts';

const OPERATOR_LABELS: Record<string, string> = {
  '==': 'is',
  '!=': 'is not',
  '>': 'is greater than',
  '>=': 'is at least',
  '<': 'is less than',
  '<=': 'is at most',
  'in': 'is one of',
  'contains': 'contains',
  'startsWith': 'starts with',
  'endsWith': 'ends with',
  'matches': 'matches',
};

function getRuleAriaLabel(field: string, operator: string, value: unknown): string {
  if (!field) return 'Condition: incomplete';
  const opLabel = OPERATOR_LABELS[operator] ?? operator;
  const valueStr = Array.isArray(value) ? value.join(', ') : String(value ?? '');
  return `Condition: ${field} ${opLabel} ${valueStr}`;
}

export interface RuleNodeProps {
  node: CelGuiRule;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
  readOnly?: boolean;
}

const DEFAULT_OPERATORS = [
  '==',
  '!=',
  '>',
  '>=',
  '<',
  '<=',
  'in',
  'contains',
  'startsWith',
  'endsWith',
  'matches',
];

export const RuleNode: React.FC<RuleNodeProps> = ({
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

  const currentField = useMemo(
    () => allFields.find((f) => f.name === node.field),
    [allFields, node.field]
  );

  const operators = useMemo(() => {
    if (!currentField?.type) return DEFAULT_OPERATORS;
    switch (currentField.type) {
      case 'string':
        return ['==', '!=', 'contains', 'startsWith', 'endsWith', 'matches', 'in'];
      case 'number':
        return ['==', '!=', '>', '>=', '<', '<=', 'in'];
      case 'boolean':
        return ['==', '!='];
      default:
        return DEFAULT_OPERATORS;
    }
  }, [currentField]);

  const groupedFields = useMemo(() => groupFields(allFields), [allFields]);

  const handleFieldChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement | HTMLInputElement>) => {
      // Reset value and operator when field changes
      onChange({ ...node, field: e.target.value, value: '', operator: '==' });
    },
    [node, onChange]
  );

  const handleOperatorChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      onChange({ ...node, operator: e.target.value });
    },
    [node, onChange]
  );

  const handleValueChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      let newValue: any = e.target.value;
      if (currentField?.type === 'number') {
        newValue = newValue === '' ? '' : Number(newValue);
      } else if (currentField?.type === 'boolean') {
        newValue = e.target.checked;
      }
      onChange({ ...node, value: newValue });
    },
    [currentField?.type, node, onChange]
  );

  // In operator takes an array of values
  const handleListValueChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      // Simple comma-separated list parser for MVP
      const rawValues = e.target.value.split(',').map((v) => v.trim());
      let parsedValues: any[] = rawValues;
      if (currentField?.type === 'number') {
        parsedValues = rawValues.map((v) => (v === '' ? '' : Number(v)));
      }
      onChange({ ...node, value: parsedValues });
    },
    [currentField?.type, node, onChange]
  );

  const renderValueEditor = () => {
    if (node.operator === 'in') {
      const displayValue = Array.isArray(node.value)
        ? node.value.join(', ')
        : String(node.value || '');
      return (
        <input
          type="text"
          value={displayValue}
          onChange={handleListValueChange}
          placeholder="value1, value2"
          disabled={readOnly}
          className="cel-rule__value-input"
        />
      );
    }

    if (currentField?.type === 'boolean') {
      return (
        <input
          type="checkbox"
          checked={!!node.value}
          onChange={handleValueChange}
          disabled={readOnly}
          className="cel-rule__value-checkbox"
        />
      );
    }

    if (currentField?.type === 'number') {
      return (
        <input
          type="number"
          value={node.value === undefined ? '' : node.value}
          onChange={handleValueChange}
          placeholder="0"
          disabled={readOnly}
          className="cel-rule__value-input"
        />
      );
    }

    // Default string/unknown
    return (
      <input
        type="text"
        value={node.value === undefined ? '' : node.value}
        onChange={handleValueChange}
        placeholder="value"
        disabled={readOnly}
        className="cel-rule__value-input"
      />
    );
  };

  return (
    <div className="cel-rule" aria-label={getRuleAriaLabel(node.field, node.operator, node.value)}>
      <div className="cel-rule__field">
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
            placeholder="field"
            disabled={readOnly}
          />
        )}
      </div>

      <div className="cel-rule__operator">
        <select
          value={node.operator}
          onChange={handleOperatorChange}
          disabled={readOnly}
        >
          {operators.map((op) => (
            <option key={op} value={op}>
              {op}
            </option>
          ))}
        </select>
      </div>

      <div className="cel-rule__value">{renderValueEditor()}</div>

      {!readOnly && onRemove && (
        <button
          type="button"
          className="cel-rule__remove"
          aria-label="Remove condition"
          onClick={onRemove}
        >
          ×
        </button>
      )}
    </div>
  );
};
