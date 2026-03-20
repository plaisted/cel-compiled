import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { CelFieldDefinition, CelGuiNode, CelGuiRule } from '../types.ts';
import { useCelSchema } from '../context/CelSchemaContext.tsx';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { flattenFields, groupFields } from '../utils/fieldUtils.ts';
import { DeleteIcon } from './DeleteIcon.tsx';
import { GroupIcon } from './GroupIcon.tsx';

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
  '==', '!=',
];

function getOperatorsForFieldType(type?: CelFieldDefinition['type']): string[] {
  switch (type) {
    case 'string':
      return ['==', '!=', 'contains', 'startsWith', 'endsWith', 'matches', 'in'];
    case 'number':
    case 'duration':
    case 'timestamp':
      return ['==', '!=', '>', '>=', '<', '<=', 'in'];
    case 'boolean':
      return ['==', '!='];
    case 'list':
      return ['contains'];
    case 'bytes':
    case 'map':
      return ['==', '!='];
    default:
      return DEFAULT_OPERATORS;
  }
}

function getDefaultValueForFieldType(type?: CelFieldDefinition['type']): unknown {
  switch (type) {
    case 'boolean':
      return true;
    case 'number':
      return '';
    case 'list':
      return [];
    default:
      return '';
  }
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

function hasValue(value: unknown): boolean {
  if (typeof value === 'string') return value.trim().length > 0;
  if (Array.isArray(value)) return value.length > 0;
  return value !== undefined && value !== null && value !== '';
}

function isRuleCollapsible(rule: CelGuiRule, type?: CelFieldDefinition['type']): boolean {
  if (!rule.field.trim()) return false;
  if (type === 'boolean') return true;
  if (rule.operator === 'in') return hasValue(rule.value);
  return hasValue(rule.value);
}

export interface NaturalRuleNodeProps {
  node: CelGuiRule;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
  onPromote?: () => void;
}

export const NaturalRuleNode: React.FC<NaturalRuleNodeProps> = ({
  node,
  onChange,
  onRemove,
  onPromote,
}) => {
  const schema = useCelSchema();
  const { readOnly } = useCelBuilder();
  const [isEditing, setIsEditing] = useState(() => !isRuleCollapsible(node));
  const [originalNode, setOriginalNode] = useState<CelGuiRule | null>(null);

  const allFields = useMemo(
    () => (schema?.fields ? flattenFields(schema.fields) : []),
    [schema]
  );

  const selectableFields = useMemo(
    () => allFields.filter((field) => !field.children?.length),
    [allFields]
  );

  const currentField = useMemo(
    () => allFields.find((f) => f.name === node.field),
    [allFields, node.field]
  );

  const operators = useMemo(() => getOperatorsForFieldType(currentField?.type), [currentField?.type]);

  const groupedFields = useMemo(() => groupFields(selectableFields), [selectableFields]);
  const canCollapse = isRuleCollapsible(node, currentField?.type);

  useEffect(() => {
    if (readOnly) {
      setIsEditing(false);
      return;
    }
    if (!canCollapse) {
      setIsEditing(true);
    }
  }, [canCollapse, readOnly]);

  const handleFieldChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement | HTMLInputElement>) => {
      const nextField = allFields.find((field) => field.name === e.target.value);
      const nextOperator = getOperatorsForFieldType(nextField?.type)[0] ?? '==';
      onChange({
        ...node,
        field: e.target.value,
        value: getDefaultValueForFieldType(nextField?.type),
        operator: nextOperator,
      });
    },
    [allFields, node, onChange]
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

  const handleBooleanPredicateChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      onChange({ ...node, operator: '==', value: e.target.value === 'true' });
    },
    [node, onChange]
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
    if (currentField?.type === 'boolean') {
      return null;
    }

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

  const selectedOperator = operators.includes(node.operator) ? node.operator : operators[0] ?? '==';
  const normalizedBooleanValue =
    typeof node.value === 'boolean' ? node.value : true;
  const booleanPredicateValue =
    node.operator === '!=' ? String(!normalizedBooleanValue) : String(normalizedBooleanValue);

  const fieldLabel = currentField?.label || node.field;
  const valueLabel = Array.isArray(node.value)
    ? node.value.join(', ')
    : currentField?.type === 'boolean'
      ? (node.operator === '!=' ? !normalizedBooleanValue : normalizedBooleanValue)
        ? 'true'
        : 'false'
      : String(node.value ?? '');
  const operatorLabel = OPERATOR_LABELS[node.operator] ?? node.operator;
  const summaryText = node.field
    ? `${fieldLabel} ${operatorLabel} ${valueLabel}`.trim()
    : 'Incomplete condition';

  const handleEnterEdit = useCallback(() => {
    if (readOnly) return;
    setOriginalNode(node);
    setIsEditing(true);
  }, [node, readOnly]);

  const handleConfirmEdit = useCallback(() => {
    if (!canCollapse) return;
    setOriginalNode(null);
    setIsEditing(false);
  }, [canCollapse]);

  const handleCancelEdit = useCallback(() => {
    if (originalNode) {
      if (!isRuleCollapsible(originalNode, allFields.find((f) => f.name === originalNode.field)?.type)) {
        onRemove?.();
      } else {
        onChange(originalNode);
        setIsEditing(false);
      }
    } else if (onRemove && !canCollapse) {
      onRemove();
    } else {
      setIsEditing(false);
    }
    setOriginalNode(null);
  }, [allFields, canCollapse, onChange, onRemove, originalNode]);

  const ariaLabel = node.field
    ? `Condition: ${node.field} ${OPERATOR_LABELS[node.operator] ?? node.operator} ${
        Array.isArray(node.value) ? node.value.join(', ') : String(node.value ?? '')
      }`
    : 'Condition: incomplete';

  if (!isEditing) {
    return (
      <div className="cel-rule cel-rule--natural cel-rule--summary" aria-label={ariaLabel}>
        <button
          type="button"
          className="cel-rule__summary"
          onClick={handleEnterEdit}
          disabled={readOnly}
          aria-label={`Edit condition: ${summaryText}`}
        >
          <span className="cel-rule__summary-text">
            <span className="cel-rule__summary-field">{fieldLabel}</span>
            <span className="cel-rule__summary-operator">{operatorLabel}</span>
            <span className="cel-rule__summary-value">{valueLabel}</span>
          </span>
        </button>

        {!readOnly && onRemove && (
          <div className="cel-rule__summary-actions">
            {onPromote && (
              <button
                type="button"
                className="cel-rule__promote"
                aria-label="Create group from condition"
                title="Create group from this condition"
                onClick={onPromote}
              >
                <GroupIcon />
              </button>
            )}
            <button
              type="button"
              className="cel-rule__remove cel-rule__remove--natural"
              aria-label="Remove condition"
              onClick={onRemove}
            >
              <DeleteIcon />
            </button>
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="cel-rule cel-rule--natural" aria-label={ariaLabel}>
      <div
        className={`cel-rule__chip-wrapper cel-rule__chip-wrapper--field${
          currentField?.type ? ` cel-rule__chip-wrapper--${currentField.type}` : ''
        }`}
      >
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
            placeholder="field"
            disabled={readOnly}
            className="cel-rule__chip cel-rule__chip--field"
          />
        )}
      </div>

      {currentField?.type === 'boolean' ? (
        <div className="cel-rule__chip-wrapper cel-rule__chip-wrapper--value">
          <select
            value={booleanPredicateValue}
            onChange={handleBooleanPredicateChange}
            disabled={readOnly}
            className="cel-rule__chip cel-rule__chip--operator cel-rule__chip--boolean-predicate"
          >
            <option value="true">is true</option>
            <option value="false">is false</option>
          </select>
        </div>
      ) : (
        <>
          <div className="cel-rule__chip-wrapper">
            <select
              value={selectedOperator}
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

          <div className="cel-rule__chip-wrapper cel-rule__chip-wrapper--value">
            {renderValueChip()}
          </div>
        </>
      )}

      {!readOnly && (
        <div className="cel-rule__edit-actions">
          <button
            type="button"
            className="cel-rule__confirm"
            aria-label="Confirm condition"
            onClick={handleConfirmEdit}
            disabled={!canCollapse}
          >
            Save
          </button>
          <button
            type="button"
            className="cel-rule__cancel"
            aria-label="Cancel condition changes"
            onClick={handleCancelEdit}
          >
            Cancel
          </button>
          {onPromote && (
            <button
              type="button"
              className="cel-rule__promote"
              aria-label="Create group from condition"
              title="Create group from this condition"
              onClick={onPromote}
            >
              <GroupIcon />
            </button>
          )}
          {onRemove && (
            <button
              type="button"
              className="cel-rule__delete"
              aria-label="Remove condition"
              onClick={onRemove}
            >
              <DeleteIcon />
            </button>
          )}
        </div>
      )}
    </div>
  );
};
