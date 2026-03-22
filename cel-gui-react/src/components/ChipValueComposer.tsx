/**
 * ChipValueComposer — horizontal chip-based value expression editor.
 *
 * Renders a CelGuiValueNode tree as an inline sequence of chips with
 * context-aware insertion points between them. Editing happens inline
 * (click-to-edit) rather than through always-visible form controls.
 *
 * Drag-and-drop reordering is deferred to a future iteration; insertion-based
 * editing (add/remove/replace) covers the first-pass UX.
 */

import React, { useCallback, useRef, useState } from 'react';
import type {
  CelGuiArithmeticNode,
  CelGuiConcatNode,
  CelGuiConditionalNode,
  CelGuiFieldRefNode,
  CelGuiLiteralNode,
  CelGuiTransformNode,
  CelGuiAdvancedValueNode,
  CelGuiValueNode,
  CelValueType,
} from '../types.ts';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { useCelSchema } from '../context/CelSchemaContext.tsx';
import { flattenFields } from '../utils/fieldUtils.ts';
import { NodeRenderer } from './NodeRenderer.tsx';

// ── Picker options ─────────────────────────────────────────────────────────────

interface PickerOption {
  type: string;
  label: string;
}

function getPickerOptions(resultType: CelValueType): PickerOption[] {
  const opts: PickerOption[] = [
    { type: 'field-ref', label: 'Field reference' },
    { type: 'literal', label: 'Constant value' },
    { type: 'conditional', label: 'If / Then / Else' },
    { type: 'transform', label: 'Transform' },
    { type: 'advanced-value', label: 'CEL expression' },
  ];
  if (resultType === 'string' || resultType === 'any') {
    opts.splice(2, 0, { type: 'concat', label: 'Concat (add text)' });
  }
  if (resultType === 'number' || resultType === 'any') {
    opts.splice(2, 0, { type: 'arithmetic', label: 'Arithmetic' });
  }
  return opts;
}

// ── Default node factory ───────────────────────────────────────────────────────

function makeDefaultNode(type: string, resultType: CelValueType): CelGuiValueNode {
  switch (type) {
    case 'field-ref':
      return { type: 'field-ref', field: '' };
    case 'literal':
      return { type: 'literal', value: resultType === 'number' ? 0 : '', valueType: resultType };
    case 'concat':
      return {
        type: 'concat',
        operands: [
          { type: 'advanced-value', expression: '' },
          { type: 'advanced-value', expression: '' },
        ],
      };
    case 'arithmetic':
      return {
        type: 'arithmetic',
        operator: '+',
        left: { type: 'advanced-value', expression: '' },
        right: { type: 'advanced-value', expression: '' },
      };
    case 'conditional':
      return {
        type: 'conditional',
        condition: { type: 'group', combinator: 'and', not: false, rules: [] },
        then: { type: 'advanced-value', expression: '' },
        otherwise: { type: 'advanced-value', expression: '' },
      };
    case 'transform':
      return {
        type: 'transform',
        operand: { type: 'advanced-value', expression: '' },
        transform: 'upperAscii',
        args: [],
      };
    default:
      return { type: 'advanced-value', expression: '' };
  }
}

function supportsSiblingComposition(resultType: CelValueType): boolean {
  return resultType === 'string' || resultType === 'number';
}

function combineSiblingNodes(
  existingNode: CelGuiValueNode,
  insertedNode: CelGuiValueNode,
  resultType: CelValueType,
  insertBefore: boolean
): CelGuiValueNode {
  if (resultType === 'number') {
    return {
      type: 'arithmetic',
      operator: '+',
      left: insertBefore ? insertedNode : existingNode,
      right: insertBefore ? existingNode : insertedNode,
    };
  }

  const existingOperands = existingNode.type === 'concat' ? existingNode.operands : [existingNode];
  const insertedOperands = insertedNode.type === 'concat' ? insertedNode.operands : [insertedNode];

  return {
    type: 'concat',
    operands: insertBefore
      ? [...insertedOperands, ...existingOperands]
      : [...existingOperands, ...insertedOperands],
  };
}

// ── Chip picker dropdown ───────────────────────────────────────────────────────

interface ChipPickerProps {
  resultType: CelValueType;
  onSelect: (type: string) => void;
  onClose: () => void;
}

const ChipPicker: React.FC<ChipPickerProps> = ({ resultType, onSelect, onClose }) => {
  const options = getPickerOptions(resultType);
  return (
    <div className="cel-chip-picker" role="listbox" aria-label="Insert expression part">
      {options.map((opt) => (
        <button
          key={opt.type}
          type="button"
          role="option"
          aria-selected={false}
          className="cel-chip-picker__option"
          onClick={() => onSelect(opt.type)}
        >
          {opt.label}
        </button>
      ))}
      <button
        type="button"
        className="cel-chip-picker__cancel"
        aria-label="Cancel"
        onClick={onClose}
      >
        Cancel
      </button>
    </div>
  );
};

// ── Insert button with picker ──────────────────────────────────────────────────

interface InsertButtonProps {
  resultType: CelValueType;
  onInsert: (type: string) => void;
  label?: string;
  ariaLabel?: string;
}

const InsertButton: React.FC<InsertButtonProps> = ({
  resultType,
  onInsert,
  label = '+',
  ariaLabel = 'Insert expression part',
}) => {
  const [open, setOpen] = useState(false);
  const btnRef = useRef<HTMLDivElement>(null);

  const handleInsert = useCallback(
    (type: string) => {
      setOpen(false);
      onInsert(type);
    },
    [onInsert]
  );

  return (
    <div className="cel-chip-insert" ref={btnRef}>
      <button
        type="button"
        className="cel-chip-insert__btn"
        aria-label={ariaLabel}
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
      >
        {label}
      </button>
      {open && (
        <ChipPicker
          resultType={resultType}
          onSelect={handleInsert}
          onClose={() => setOpen(false)}
        />
      )}
    </div>
  );
};

// ── Leaf chip: field ref ───────────────────────────────────────────────────────

interface FieldRefChipProps {
  node: CelGuiFieldRefNode;
  onChange: (n: CelGuiValueNode) => void;
  onRemove?: () => void;
}

const FieldRefChip: React.FC<FieldRefChipProps> = ({ node, onChange, onRemove }) => {
  const { readOnly } = useCelBuilder();
  const schema = useCelSchema();
  const [editing, setEditing] = useState(!node.field);
  const fields = flattenFields(schema?.fields ?? []).filter((f) => !f.children?.length);
  const label = node.field || 'field…';

  if (editing && !readOnly) {
    if (fields.length > 0) {
      return (
        <div className="cel-chip cel-chip--field cel-chip--editing">
          <select
            autoFocus
            className="cel-chip__inline-input"
            value={node.field}
            onChange={(e) => {
              onChange({ ...node, field: e.target.value });
              setEditing(false);
            }}
            onBlur={() => setEditing(false)}
          >
            <option value="">Select field…</option>
            {fields.map((f) => (
              <option key={f.name} value={f.name}>
                {f.label ?? f.name}
              </option>
            ))}
          </select>
        </div>
      );
    }
    return (
      <div className="cel-chip cel-chip--field cel-chip--editing">
        <input
          autoFocus
          className="cel-chip__inline-input"
          type="text"
          value={node.field}
          placeholder="field.path"
          onChange={(e) => onChange({ ...node, field: e.target.value })}
          onBlur={() => setEditing(false)}
          onKeyDown={(e) => e.key === 'Enter' && setEditing(false)}
        />
      </div>
    );
  }

  return (
    <div className="cel-chip cel-chip--field">
      <button
        type="button"
        className="cel-chip__label"
        aria-label={`Field: ${label}`}
        title="Click to edit field"
        onClick={() => !readOnly && setEditing(true)}
      >
        {label}
      </button>
      {!readOnly && onRemove && (
        <button
          type="button"
          className="cel-chip__remove"
          aria-label="Remove"
          onClick={onRemove}
        >
          ×
        </button>
      )}
    </div>
  );
};

// ── Leaf chip: literal ─────────────────────────────────────────────────────────

interface LiteralChipProps {
  node: CelGuiLiteralNode;
  onChange: (n: CelGuiValueNode) => void;
  onRemove?: () => void;
}

const LiteralChip: React.FC<LiteralChipProps> = ({ node, onChange, onRemove }) => {
  const { readOnly } = useCelBuilder();
  const isEmpty = node.value === '' || node.value === null || node.value === undefined;
  const [editing, setEditing] = useState(isEmpty);

  const displayValue = () => {
    if (node.value === null || node.value === undefined) return 'null';
    if (node.valueType === 'string') return `"${node.value}"`;
    return String(node.value);
  };

  if (editing && !readOnly) {
    if (node.valueType === 'boolean') {
      return (
        <div className="cel-chip cel-chip--literal cel-chip--editing">
          <select
            autoFocus
            className="cel-chip__inline-input"
            value={String(node.value ?? 'true')}
            onChange={(e) => {
              onChange({ ...node, value: e.target.value === 'true' });
              setEditing(false);
            }}
            onBlur={() => setEditing(false)}
          >
            <option value="true">true</option>
            <option value="false">false</option>
          </select>
        </div>
      );
    }
    return (
      <div className="cel-chip cel-chip--literal cel-chip--editing">
        <input
          autoFocus
          className="cel-chip__inline-input"
          type={node.valueType === 'number' ? 'number' : 'text'}
          value={node.value === null || node.value === undefined ? '' : String(node.value)}
          placeholder={node.valueType === 'number' ? '0' : 'value…'}
          onChange={(e) => {
            const raw = e.target.value;
            const val = node.valueType === 'number' ? (raw === '' ? '' : Number(raw)) : raw;
            onChange({ ...node, value: val });
          }}
          onBlur={() => setEditing(false)}
          onKeyDown={(e) => e.key === 'Enter' && setEditing(false)}
        />
      </div>
    );
  }

  return (
    <div className="cel-chip cel-chip--literal">
      <button
        type="button"
        className="cel-chip__label"
        aria-label={`Value: ${displayValue()}`}
        title="Click to edit value"
        onClick={() => !readOnly && setEditing(true)}
      >
        {displayValue()}
      </button>
      {!readOnly && onRemove && (
        <button
          type="button"
          className="cel-chip__remove"
          aria-label="Remove"
          onClick={onRemove}
        >
          ×
        </button>
      )}
    </div>
  );
};

// ── Leaf chip: advanced CEL ────────────────────────────────────────────────────

interface AdvancedChipProps {
  node: CelGuiAdvancedValueNode;
  onChange: (n: CelGuiValueNode) => void;
  onRemove?: () => void;
}

const AdvancedChip: React.FC<AdvancedChipProps> = ({ node, onChange, onRemove }) => {
  const { readOnly } = useCelBuilder();
  const [editing, setEditing] = useState(!node.expression);
  const label = node.expression || 'CEL…';

  if (editing && !readOnly) {
    return (
      <div className="cel-chip cel-chip--advanced cel-chip--editing">
        <input
          autoFocus
          className="cel-chip__inline-input cel-chip__inline-input--wide"
          type="text"
          value={node.expression}
          placeholder="CEL expression…"
          onChange={(e) => onChange({ ...node, expression: e.target.value })}
          onBlur={() => setEditing(false)}
          onKeyDown={(e) => e.key === 'Enter' && setEditing(false)}
        />
      </div>
    );
  }

  return (
    <div className="cel-chip cel-chip--advanced">
      <button
        type="button"
        className="cel-chip__label"
        aria-label={`CEL: ${label}`}
        title="Click to edit CEL expression"
        onClick={() => !readOnly && setEditing(true)}
      >
        {label}
      </button>
      {!readOnly && onRemove && (
        <button
          type="button"
          className="cel-chip__remove"
          aria-label="Remove"
          onClick={onRemove}
        >
          ×
        </button>
      )}
    </div>
  );
};

// ── Operator chip ──────────────────────────────────────────────────────────────

interface OperatorChipProps {
  operator: string;
  operators: string[];
  onChange: (op: string) => void;
}

const OperatorChip: React.FC<OperatorChipProps> = ({ operator, operators, onChange }) => {
  const { readOnly } = useCelBuilder();
  const [editing, setEditing] = useState(false);

  if (editing && !readOnly) {
    return (
      <div className="cel-chip cel-chip--operator cel-chip--editing">
        <select
          autoFocus
          className="cel-chip__inline-input"
          value={operator}
          onChange={(e) => {
            onChange(e.target.value);
            setEditing(false);
          }}
          onBlur={() => setEditing(false)}
        >
          {operators.map((op) => (
            <option key={op} value={op}>
              {op}
            </option>
          ))}
        </select>
      </div>
    );
  }

  return (
    <div className="cel-chip cel-chip--operator">
      <button
        type="button"
        className="cel-chip__label"
        aria-label={`Operator: ${operator}`}
        title="Click to change operator"
        onClick={() => !readOnly && setEditing(true)}
      >
        {operator}
      </button>
    </div>
  );
};

// ── Single-node chip dispatcher ────────────────────────────────────────────────
// Renders a leaf node (field-ref, literal, advanced-value) as a chip.
// Non-leaf nodes (concat, arithmetic, conditional, transform) are handled by
// the root composer which decomposes them into their constituent chips.

interface LeafChipProps {
  node: CelGuiValueNode;
  resultType: CelValueType;
  onChange: (n: CelGuiValueNode) => void;
  onRemove?: () => void;
}

const LeafChip: React.FC<LeafChipProps> = ({ node, resultType, onChange, onRemove }) => {
  switch (node.type) {
    case 'field-ref':
      return <FieldRefChip node={node} onChange={onChange} onRemove={onRemove} />;
    case 'literal':
      return <LiteralChip node={node} onChange={onChange} onRemove={onRemove} />;
    case 'advanced-value':
      return <AdvancedChip node={node} onChange={onChange} onRemove={onRemove} />;
    default:
      // Nested composite — render recursively inside a contained chip block
      return (
        <div className="cel-chip cel-chip--composite">
          <ChipValueComposer
            node={node}
            resultType={resultType}
            onChange={onChange}
            onRemove={onRemove}
            nested
          />
        </div>
      );
  }
};

// ── Concat composer ────────────────────────────────────────────────────────────

interface ConcatComposerProps {
  node: CelGuiConcatNode;
  onChange: (n: CelGuiValueNode) => void;
}

const ConcatComposer: React.FC<ConcatComposerProps> = ({ node, onChange }) => {
  const { readOnly } = useCelBuilder();

  const updateOperand = (i: number, updated: CelGuiValueNode) => {
    const operands = node.operands.map((op, idx) => (idx === i ? updated : op));
    onChange({ ...node, operands });
  };

  const insertAt = (i: number, type: string) => {
    const newNode = makeDefaultNode(type, 'string');
    const operands = [...node.operands.slice(0, i), newNode, ...node.operands.slice(i)];
    onChange({ ...node, operands });
  };

  const removeAt = (i: number) => {
    const operands = node.operands.filter((_, idx) => idx !== i);
    if (operands.length < 1) {
      onChange({ type: 'advanced-value', expression: '' });
    } else if (operands.length === 1) {
      onChange(operands[0]);
    } else {
      onChange({ ...node, operands });
    }
  };

  return (
    <div className="cel-chip-row" role="group" aria-label="Concatenation expression">
      {!readOnly && (
        <InsertButton
          resultType="string"
          onInsert={(type) => insertAt(0, type)}
          ariaLabel="Insert before first part"
        />
      )}
      {node.operands.map((op, i) => (
        <React.Fragment key={i}>
          <LeafChip
            node={op}
            resultType="string"
            onChange={(n) => updateOperand(i, n)}
            onRemove={!readOnly && node.operands.length > 1 ? () => removeAt(i) : undefined}
          />
          {i < node.operands.length - 1 && (
            <>
              <span className="cel-chip-separator" aria-hidden="true">+</span>
              {!readOnly && (
                <InsertButton
                  resultType="string"
                  onInsert={(type) => insertAt(i + 1, type)}
                  ariaLabel={`Insert after part ${i + 1}`}
                />
              )}
            </>
          )}
        </React.Fragment>
      ))}
      {!readOnly && (
        <InsertButton
          resultType="string"
          onInsert={(type) => insertAt(node.operands.length, type)}
          ariaLabel="Insert after last part"
        />
      )}
    </div>
  );
};

// ── Arithmetic composer ────────────────────────────────────────────────────────

interface ArithmeticComposerProps {
  node: CelGuiArithmeticNode;
  onChange: (n: CelGuiValueNode) => void;
}

const ArithmeticComposer: React.FC<ArithmeticComposerProps> = ({ node, onChange }) => {
  const { readOnly } = useCelBuilder();
  const ARITHMETIC_OPS = ['+', '-', '*', '/'];

  return (
    <div className="cel-chip-row" role="group" aria-label="Arithmetic expression">
      <LeafChip
        node={node.left}
        resultType="number"
        onChange={(n) => onChange({ ...node, left: n })}
        onRemove={!readOnly ? () => onChange(node.right) : undefined}
      />
      <OperatorChip
        operator={node.operator}
        operators={ARITHMETIC_OPS}
        onChange={(op) => onChange({ ...node, operator: op as CelGuiArithmeticNode['operator'] })}
      />
      <LeafChip
        node={node.right}
        resultType="number"
        onChange={(n) => onChange({ ...node, right: n })}
        onRemove={!readOnly ? () => onChange(node.left) : undefined}
      />
    </div>
  );
};

// ── Conditional (if/then/else) composer ───────────────────────────────────────

interface ConditionalComposerProps {
  node: CelGuiConditionalNode;
  resultType: CelValueType;
  onChange: (n: CelGuiValueNode) => void;
  onRemove?: () => void;
}

const ConditionalComposer: React.FC<ConditionalComposerProps> = ({
  node,
  resultType,
  onChange,
  onRemove,
}) => {
  const { readOnly } = useCelBuilder();

  const addElseIf = () => {
    const newConditional: CelGuiConditionalNode = {
      type: 'conditional',
      condition: { type: 'group', combinator: 'and', not: false, rules: [] },
      then: { type: 'advanced-value', expression: '' },
      otherwise: node.otherwise,
    };
    onChange({ ...node, otherwise: newConditional });
  };

  return (
    <div className="cel-chip-conditional" role="group" aria-label="Conditional expression">
      {/* IF clause */}
      <div className="cel-chip-conditional__clause">
        <span className="cel-chip-conditional__keyword">if</span>
        <div className="cel-chip-conditional__predicate">
          <NodeRenderer
            node={node.condition}
            onChange={(newFilter) => onChange({ ...node, condition: newFilter })}
          />
        </div>
      </div>

      {/* THEN clause */}
      <div className="cel-chip-conditional__clause">
        <span className="cel-chip-conditional__keyword">then</span>
        <div className="cel-chip-conditional__branch">
          <ChipValueComposer
            node={node.then}
            resultType={resultType}
            onChange={(n) => onChange({ ...node, then: n })}
            nested
          />
        </div>
      </div>

      {/* ELSE / ELSE-IF clause */}
      {node.otherwise.type === 'conditional' ? (
        <ConditionalComposer
          node={node.otherwise as CelGuiConditionalNode}
          resultType={resultType}
          onChange={(n) => onChange({ ...node, otherwise: n })}
        />
      ) : (
        <div className="cel-chip-conditional__clause">
          <span className="cel-chip-conditional__keyword">else</span>
          <div className="cel-chip-conditional__branch">
            <ChipValueComposer
              node={node.otherwise}
              resultType={resultType}
              onChange={(n) => onChange({ ...node, otherwise: n })}
              nested
            />
          </div>
        </div>
      )}

      {!readOnly && (
        <div className="cel-chip-conditional__actions">
          <button
            type="button"
            className="cel-chip-conditional__add-else-if"
            onClick={addElseIf}
          >
            + Add else-if
          </button>
          {onRemove && (
            <button
              type="button"
              className="cel-chip__remove cel-chip-conditional__remove"
              aria-label="Remove conditional"
              onClick={onRemove}
            >
              ×
            </button>
          )}
        </div>
      )}
    </div>
  );
};

// ── Transform composer ─────────────────────────────────────────────────────────

const KNOWN_TRANSFORMS = [
  'upperAscii',
  'lowerAscii',
  'trim',
  'trimLeft',
  'trimRight',
  'string',
  'int',
  'double',
  'size',
];

interface TransformComposerProps {
  node: CelGuiTransformNode;
  resultType: CelValueType;
  onChange: (n: CelGuiValueNode) => void;
  onRemove?: () => void;
}

const TransformComposer: React.FC<TransformComposerProps> = ({
  node,
  resultType,
  onChange,
  onRemove,
}) => {
  const { readOnly } = useCelBuilder();
  const [editingTransform, setEditingTransform] = useState(false);

  return (
    <div className="cel-chip-row" role="group" aria-label="Transform expression">
      <LeafChip
        node={node.operand}
        resultType={resultType}
        onChange={(n) => onChange({ ...node, operand: n })}
      />
      <span className="cel-chip-separator" aria-hidden="true">.</span>
      {editingTransform && !readOnly ? (
        <div className="cel-chip cel-chip--transform cel-chip--editing">
          <select
            autoFocus
            className="cel-chip__inline-input"
            value={node.transform}
            onChange={(e) => {
              onChange({ ...node, transform: e.target.value });
              setEditingTransform(false);
            }}
            onBlur={() => setEditingTransform(false)}
          >
            {KNOWN_TRANSFORMS.map((t) => (
              <option key={t} value={t}>
                {t}()
              </option>
            ))}
          </select>
        </div>
      ) : (
        <div className="cel-chip cel-chip--transform">
          <button
            type="button"
            className="cel-chip__label"
            aria-label={`Transform: ${node.transform}`}
            title="Click to change transform"
            onClick={() => !readOnly && setEditingTransform(true)}
          >
            {node.transform}()
          </button>
          {!readOnly && onRemove && (
            <button
              type="button"
              className="cel-chip__remove"
              aria-label="Remove transform"
              onClick={onRemove}
            >
              ×
            </button>
          )}
        </div>
      )}
    </div>
  );
};

// ── Root chip composer ─────────────────────────────────────────────────────────

export interface ChipValueComposerProps {
  node: CelGuiValueNode | null | undefined;
  resultType: CelValueType;
  onChange: (node: CelGuiValueNode) => void;
  onRemove?: () => void;
  /** When true, renders without the outer wrapper (used in recursive/nested positions). */
  nested?: boolean;
}

export const ChipValueComposer: React.FC<ChipValueComposerProps> = ({
  node,
  resultType,
  onChange,
  onRemove,
  nested = false,
}) => {
  const { readOnly } = useCelBuilder();

  const isEmpty =
    !node ||
    (node.type === 'advanced-value' && (node as CelGuiAdvancedValueNode).expression === '');

  // Insert a new node, promoting simple roots to concat/arithmetic when needed
  const handleInsert = useCallback(
    (type: string) => {
      const newNode = makeDefaultNode(type, resultType);
      if (isEmpty) {
        onChange(newNode);
        return;
      }
      if (!supportsSiblingComposition(resultType)) {
        onChange(newNode);
        return;
      }

      onChange(combineSiblingNodes(node!, newNode, resultType, false));
    },
    [isEmpty, node, onChange, resultType]
  );

  const renderContent = () => {
    if (isEmpty) {
      if (readOnly) {
        return <span className="cel-chip-composer__empty">Empty</span>;
      }
      return (
        <InsertButton
          resultType={resultType}
          onInsert={handleInsert}
          label="+ Add value"
          ariaLabel="Add value"
        />
      );
    }

    switch (node!.type) {
      case 'field-ref':
        return (
          <div className="cel-chip-row">
            {!readOnly && supportsSiblingComposition(resultType) && (
              <InsertButton
                resultType={resultType}
                onInsert={(t) => {
                  const newN = makeDefaultNode(t, resultType);
                  onChange(combineSiblingNodes(node!, newN, resultType, true));
                }}
                ariaLabel="Insert before"
              />
            )}
            <FieldRefChip
              node={node as CelGuiFieldRefNode}
              onChange={onChange}
              onRemove={onRemove}
            />
            {!readOnly && supportsSiblingComposition(resultType) && (
              <InsertButton
                resultType={resultType}
                onInsert={(t) => {
                  const newN = makeDefaultNode(t, resultType);
                  onChange(combineSiblingNodes(node!, newN, resultType, false));
                }}
                ariaLabel="Insert after"
              />
            )}
          </div>
        );
      case 'literal':
        return (
          <div className="cel-chip-row">
            {!readOnly && supportsSiblingComposition(resultType) && (
              <InsertButton
                resultType={resultType}
                onInsert={(t) => {
                  const newN = makeDefaultNode(t, resultType);
                  onChange(combineSiblingNodes(node!, newN, resultType, true));
                }}
                ariaLabel="Insert before"
              />
            )}
            <LiteralChip
              node={node as CelGuiLiteralNode}
              onChange={onChange}
              onRemove={onRemove}
            />
            {!readOnly && supportsSiblingComposition(resultType) && (
              <InsertButton
                resultType={resultType}
                onInsert={(t) => {
                  const newN = makeDefaultNode(t, resultType);
                  onChange(combineSiblingNodes(node!, newN, resultType, false));
                }}
                ariaLabel="Insert after"
              />
            )}
          </div>
        );
      case 'advanced-value':
        return (
          <div className="cel-chip-row">
            <AdvancedChip
              node={node as CelGuiAdvancedValueNode}
              onChange={onChange}
              onRemove={onRemove}
            />
          </div>
        );
      case 'concat':
        return <ConcatComposer node={node as CelGuiConcatNode} onChange={onChange} />;
      case 'arithmetic':
        return <ArithmeticComposer node={node as CelGuiArithmeticNode} onChange={onChange} />;
      case 'conditional':
        return (
          <ConditionalComposer
            node={node as CelGuiConditionalNode}
            resultType={resultType}
            onChange={onChange}
            onRemove={onRemove}
          />
        );
      case 'transform':
        return (
          <TransformComposer
            node={node as CelGuiTransformNode}
            resultType={resultType}
            onChange={onChange}
            onRemove={onRemove}
          />
        );
      default:
        return <span className="cel-chip-composer__unknown">Unknown node</span>;
    }
  };

  if (nested) {
    return <>{renderContent()}</>;
  }

  return (
    <div className="cel-chip-composer" aria-label="Value expression composer">
      {renderContent()}
    </div>
  );
};
