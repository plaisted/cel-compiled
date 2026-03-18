import React, { useCallback, useMemo } from 'react';
import { CelGuiNode, CelGuiRule } from '../types.ts';
import { useCelSchema } from '../context/CelSchemaContext.tsx';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { flattenFields, groupFields } from '../utils/fieldUtils.ts';

export const OPERATOR_LABELS: Record<string, string> = {
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

const DEFAULT_OPERATORS = [
  '==', '!=', '>', '>=', '<', '<=', 'in',
  'contains', 'startsWith', 'endsWith', 'matches',
];

export interface NaturalRuleNodeProps {
  node: CelGuiRule;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
}

export const NaturalRuleNode: React.FC<NaturalRuleNodeProps> = ({
  node,
  onChange,
  onRemove,
}) => {
  const schema = useCelSchema();
  const { readOnly } = useCelBuilder();

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

  const handleListValueChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const rawValues = e.target.value.split(',').map((v) => v.trim());
      const parsedValues: any[] =
        currentField?.type === 'number'
          ? rawValues.map((v) => (v === '' ? '' : Number(v)))
          : rawValues;
      onChange({ ...node, value: parsedValues });
    },
    [currentField?.type, node, onChange]
  );

  const renderValueChip = () => {
    if (node.operator === 'in') {
      const displayValue = Array.isArray(node.value)
        ? node.value.join(', ')
        : String(node.value || '');
      return (
        <input
          type="text"
          value={displayValue}
          onChange={handleListValueChange}
          placeholder="val1, val2…"
          disabled={readOnly}
          className="cel-rule__chip cel-rule__chip--value"
        />
      );
    }

    if (currentField?.type === 'boolean') {
      return (
        <label className="cel-rule__chip cel-rule__chip--value cel-rule__chip--boolean">
          <input
            type="checkbox"
            checked={!!node.value}
            onChange={handleValueChange}
            disabled={readOnly}
          />
          <span>{node.value ? 'true' : 'false'}</span>
        </label>
      );
    }

    return (
      <input
        type={currentField?.type === 'number' ? 'number' : 'text'}
        value={node.value === undefined ? '' : node.value}
        onChange={handleValueChange}
        placeholder="value"
        disabled={readOnly}
        className="cel-rule__chip cel-rule__chip--value"
      />
    );
  };

  const ariaLabel = node.field
    ? `Condition: ${node.field} ${OPERATOR_LABELS[node.operator] ?? node.operator} ${
        Array.isArray(node.value) ? node.value.join(', ') : String(node.value ?? '')
      }`
    : 'Condition: incomplete';

  return (
    <div className="cel-rule cel-rule--natural" aria-label={ariaLabel}>
      {/* Field chip */}
      <div className="cel-rule__chip-wrapper">
        {schema?.fields ? (
          <select
            value={node.field}
            onChange={handleFieldChange}
            disabled={readOnly}
            className="cel-rule__chip cel-rule__chip--field"
          >
            <option value="">Select field…</option>
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
            className="cel-rule__chip cel-rule__chip--field"
          />
        )}
      </div>

      {/* Operator chip */}
      <div className="cel-rule__chip-wrapper">
        <select
          value={node.operator}
          onChange={handleOperatorChange}
          disabled={readOnly}
          className="cel-rule__chip cel-rule__chip--operator"
        >
          {operators.map((op) => (
            <option key={op} value={op}>
              {OPERATOR_LABELS[op] ?? op}
            </option>
          ))}
        </select>
      </div>

      {/* Value chip */}
      <div className="cel-rule__chip-wrapper cel-rule__chip-wrapper--value">
        {renderValueChip()}
      </div>

      {!readOnly && onRemove && (
        <button
          type="button"
          className="cel-rule__remove cel-rule__remove--natural"
          aria-label="Remove condition"
          onClick={onRemove}
        >
          ×
        </button>
      )}
    </div>
  );
};
