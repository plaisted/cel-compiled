import React, { Suspense, useCallback, useMemo } from 'react';
import {
  CelGuiValueNode,
  CelGuiValueNodeType,
  CelGuiFieldRefNode,
  CelGuiLiteralNode,
  CelGuiConcatNode,
  CelGuiArithmeticNode,
  CelGuiConditionalNode,
  CelGuiTransformNode,
  CelGuiAdvancedValueNode,
  CelValueType,
} from '../types.ts';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { useCelSchema } from '../context/CelSchemaContext.tsx';
import { flattenFields } from '../utils/fieldUtils.ts';
import { NodeRenderer } from './NodeRenderer.tsx';
import { DeleteIcon } from './DeleteIcon.tsx';

const CelCodeEditor = React.lazy(() => import('../editor/CelCodeEditor.tsx'));

export interface ValueNodeRendererProps {
  node: CelGuiValueNode;
  resultType: string;
  onChange: (node: CelGuiValueNode) => void;
  onRemove?: () => void;
  depth?: number;
}

const NODE_TYPE_LABELS: Record<CelGuiValueNodeType, string> = {
  'field-ref': 'Field',
  literal: 'Literal',
  concat: 'Concat',
  arithmetic: 'Arithmetic',
  conditional: 'If / Then',
  transform: 'Transform',
  'advanced-value': 'CEL',
};

const ARITHMETIC_OPS = ['+', '-', '*', '/'] as const;
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

function getNodeTypesForResult(resultType: string): CelGuiValueNodeType[] {
  const base: CelGuiValueNodeType[] = ['field-ref', 'literal', 'advanced-value', 'conditional', 'transform'];
  if (resultType === 'string' || resultType === 'any') base.splice(2, 0, 'concat');
  if (resultType === 'number' || resultType === 'any') base.splice(2, 0, 'arithmetic');
  return base;
}

function makeDefaultNode(type: CelGuiValueNodeType, resultType: string): CelGuiValueNode {
  switch (type) {
    case 'field-ref': return { type: 'field-ref', field: '' };
    case 'literal': return { type: 'literal', value: '', valueType: resultType as CelValueType };
    case 'concat': return { type: 'concat', operands: [{ type: 'advanced-value', expression: '' }, { type: 'advanced-value', expression: '' }] };
    case 'arithmetic': return { type: 'arithmetic', operator: '+', left: { type: 'advanced-value', expression: '' }, right: { type: 'advanced-value', expression: '' } };
    case 'conditional': return {
      type: 'conditional',
      condition: { type: 'group', combinator: 'and', not: false, rules: [] },
      then: { type: 'advanced-value', expression: '' },
      otherwise: { type: 'advanced-value', expression: '' },
    };
    case 'transform': return { type: 'transform', operand: { type: 'advanced-value', expression: '' }, transform: 'upperAscii', args: [] };
    case 'advanced-value': return { type: 'advanced-value', expression: '' };
  }
}

// ── Field ref node ──────────────────────────────────────────────────────────

const FieldRefNodeEditor: React.FC<{
  node: CelGuiFieldRefNode;
  onChange: (n: CelGuiValueNode) => void;
}> = ({ node, onChange }) => {
  const schema = useCelSchema();
  const { readOnly } = useCelBuilder();
  const fields = useMemo(() => (schema?.fields ? flattenFields(schema.fields).filter((f) => !f.children?.length) : []), [schema]);

  return (
    <div className="cel-value-node__field-ref">
      {fields.length > 0 ? (
        <select
          className="cel-value-node__input"
          value={node.field}
          disabled={readOnly}
          onChange={(e) => onChange({ ...node, field: e.target.value })}
        >
          <option value="">Select field…</option>
          {fields.map((f) => (
            <option key={f.name} value={f.name}>{f.label ?? f.name}</option>
          ))}
        </select>
      ) : (
        <input
          className="cel-value-node__input"
          type="text"
          value={node.field}
          disabled={readOnly}
          placeholder="field.path"
          onChange={(e) => onChange({ ...node, field: e.target.value })}
        />
      )}
    </div>
  );
};

// ── Literal node ────────────────────────────────────────────────────────────

const LiteralNodeEditor: React.FC<{
  node: CelGuiLiteralNode;
  onChange: (n: CelGuiValueNode) => void;
}> = ({ node, onChange }) => {
  const { readOnly } = useCelBuilder();

  if (node.valueType === 'boolean') {
    return (
      <div className="cel-value-node__literal">
        <select
          className="cel-value-node__input"
          value={String(node.value)}
          disabled={readOnly}
          onChange={(e) => onChange({ ...node, value: e.target.value === 'true' })}
        >
          <option value="true">true</option>
          <option value="false">false</option>
        </select>
      </div>
    );
  }

  return (
    <div className="cel-value-node__literal">
      <input
        className="cel-value-node__input"
        type={node.valueType === 'number' ? 'number' : 'text'}
        value={node.value === null || node.value === undefined ? '' : String(node.value)}
        disabled={readOnly}
        placeholder={node.valueType === 'number' ? '0' : 'value…'}
        onChange={(e) => {
          const raw = e.target.value;
          const val = node.valueType === 'number' ? (raw === '' ? '' : Number(raw)) : raw;
          onChange({ ...node, value: val });
        }}
      />
    </div>
  );
};

// ── Concat node ─────────────────────────────────────────────────────────────

const ConcatNodeEditor: React.FC<{
  node: CelGuiConcatNode;
  resultType: string;
  onChange: (n: CelGuiValueNode) => void;
  depth: number;
}> = ({ node, onChange, depth }) => {
  const { readOnly } = useCelBuilder();

  const updateOperand = (i: number, updated: CelGuiValueNode) => {
    const operands = node.operands.map((op, idx) => (idx === i ? updated : op));
    onChange({ ...node, operands });
  };

  const addOperand = () => onChange({ ...node, operands: [...node.operands, { type: 'advanced-value', expression: '' }] });
  const removeOperand = (i: number) => onChange({ ...node, operands: node.operands.filter((_, idx) => idx !== i) });

  return (
    <div className="cel-value-node__concat">
      <div className="cel-value-node__concat-operands">
        {node.operands.map((op, i) => (
          <div key={i} className="cel-value-node__concat-operand">
            <ValueNodeRenderer node={op} resultType="string" onChange={(n) => updateOperand(i, n)} depth={depth + 1} />
            {!readOnly && node.operands.length > 2 && (
              <button type="button" className="cel-value-node__remove" aria-label="Remove operand" onClick={() => removeOperand(i)}>
                <DeleteIcon />
              </button>
            )}
            {i < node.operands.length - 1 && <span className="cel-value-node__concat-plus">+</span>}
          </div>
        ))}
      </div>
      {!readOnly && (
        <button type="button" className="cel-value-node__add" onClick={addOperand}>+ Add segment</button>
      )}
    </div>
  );
};

// ── Arithmetic node ─────────────────────────────────────────────────────────

const ArithmeticNodeEditor: React.FC<{
  node: CelGuiArithmeticNode;
  resultType: string;
  onChange: (n: CelGuiValueNode) => void;
  depth: number;
}> = ({ node, onChange, depth }) => {
  const { readOnly } = useCelBuilder();
  return (
    <div className="cel-value-node__arithmetic">
      <ValueNodeRenderer node={node.left} resultType="number" onChange={(n) => onChange({ ...node, left: n })} depth={depth + 1} />
      <select
        className="cel-value-node__op-select"
        value={node.operator}
        disabled={readOnly}
        onChange={(e) => onChange({ ...node, operator: e.target.value as any })}
      >
        {ARITHMETIC_OPS.map((op) => <option key={op} value={op}>{op}</option>)}
      </select>
      <ValueNodeRenderer node={node.right} resultType="number" onChange={(n) => onChange({ ...node, right: n })} depth={depth + 1} />
    </div>
  );
};

// ── Conditional node ─────────────────────────────────────────────────────────

const ConditionalNodeEditor: React.FC<{
  node: CelGuiConditionalNode;
  resultType: string;
  onChange: (n: CelGuiValueNode) => void;
  depth: number;
}> = ({ node, resultType, onChange, depth }) => {
  const { readOnly } = useCelBuilder();

  // else-if: if the otherwise branch is itself a conditional, offer "Add condition" that appends a new conditional
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
    <div className="cel-value-node__conditional">
      <div className="cel-value-node__conditional-row">
        <span className="cel-value-node__label">if</span>
        <div className="cel-value-node__conditional-condition">
          <NodeRenderer
            node={node.condition}
            onChange={(newFilter) => onChange({ ...node, condition: newFilter })}
          />
        </div>
      </div>
      <div className="cel-value-node__conditional-row">
        <span className="cel-value-node__label">then</span>
        <ValueNodeRenderer node={node.then} resultType={resultType} onChange={(n) => onChange({ ...node, then: n })} depth={depth + 1} />
      </div>
      {node.otherwise.type === 'conditional' ? (
        <ConditionalNodeEditor node={node.otherwise} resultType={resultType} onChange={(n) => onChange({ ...node, otherwise: n })} depth={depth + 1} />
      ) : (
        <div className="cel-value-node__conditional-row">
          <span className="cel-value-node__label">else</span>
          <ValueNodeRenderer node={node.otherwise} resultType={resultType} onChange={(n) => onChange({ ...node, otherwise: n })} depth={depth + 1} />
        </div>
      )}
      {!readOnly && (
        <button type="button" className="cel-value-node__add" onClick={addElseIf}>+ Add else-if</button>
      )}
    </div>
  );
};

// ── Transform node ───────────────────────────────────────────────────────────

const TransformNodeEditor: React.FC<{
  node: CelGuiTransformNode;
  resultType: string;
  onChange: (n: CelGuiValueNode) => void;
  depth: number;
}> = ({ node, resultType, onChange, depth }) => {
  const { readOnly } = useCelBuilder();
  return (
    <div className="cel-value-node__transform">
      <ValueNodeRenderer node={node.operand} resultType={resultType} onChange={(n) => onChange({ ...node, operand: n })} depth={depth + 1} />
      <span className="cel-value-node__dot">.</span>
      <select
        className="cel-value-node__op-select"
        value={node.transform}
        disabled={readOnly}
        onChange={(e) => onChange({ ...node, transform: e.target.value })}
      >
        {KNOWN_TRANSFORMS.map((t) => <option key={t} value={t}>{t}()</option>)}
      </select>
    </div>
  );
};

// ── Advanced value node ──────────────────────────────────────────────────────

const AdvancedValueNodeEditor: React.FC<{
  node: CelGuiAdvancedValueNode;
  onChange: (n: CelGuiValueNode) => void;
}> = ({ node, onChange }) => {
  const schema = useCelSchema();
  const { readOnly } = useCelBuilder();
  return (
    <div className="cel-value-node__advanced">
      <Suspense fallback={<div className="cel-advanced__loading">Loading editor…</div>}>
        <CelCodeEditor
          value={node.expression}
          onChange={(val) => onChange({ ...node, expression: val })}
          readOnly={readOnly}
          schema={schema}
          className="cel-advanced__editor"
        />
      </Suspense>
    </div>
  );
};

// ── Top-level dispatcher ─────────────────────────────────────────────────────

export const ValueNodeRenderer: React.FC<ValueNodeRendererProps> = ({
  node,
  resultType,
  onChange,
  onRemove,
  depth = 0,
}) => {
  const { readOnly } = useCelBuilder();

  const handleTypeChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      const newType = e.target.value as CelGuiValueNodeType;
      onChange(makeDefaultNode(newType, resultType));
    },
    [onChange, resultType]
  );

  const nodeTypes = getNodeTypesForResult(resultType);

  const renderEditor = () => {
    switch (node.type) {
      case 'field-ref':
        return <FieldRefNodeEditor node={node} onChange={onChange} />;
      case 'literal':
        return <LiteralNodeEditor node={node} onChange={onChange} />;
      case 'concat':
        return <ConcatNodeEditor node={node} resultType={resultType} onChange={onChange} depth={depth} />;
      case 'arithmetic':
        return <ArithmeticNodeEditor node={node} resultType={resultType} onChange={onChange} depth={depth} />;
      case 'conditional':
        return <ConditionalNodeEditor node={node} resultType={resultType} onChange={onChange} depth={depth} />;
      case 'transform':
        return <TransformNodeEditor node={node} resultType={resultType} onChange={onChange} depth={depth} />;
      case 'advanced-value':
        return <AdvancedValueNodeEditor node={node} onChange={onChange} />;
      default:
        return <div>Unknown value node type</div>;
    }
  };

  return (
    <div className={`cel-value-node cel-value-node--${node.type}${depth > 0 ? ' cel-value-node--nested' : ''}`}>
      <div className="cel-value-node__header">
        <select
          className="cel-value-node__type-select"
          value={node.type}
          disabled={readOnly}
          onChange={handleTypeChange}
          aria-label="Value expression type"
        >
          {nodeTypes.map((t) => (
            <option key={t} value={t}>{NODE_TYPE_LABELS[t]}</option>
          ))}
        </select>
        {!readOnly && onRemove && (
          <button type="button" className="cel-value-node__remove" aria-label="Remove node" onClick={onRemove}>
            <DeleteIcon />
          </button>
        )}
      </div>
      <div className="cel-value-node__body">{renderEditor()}</div>
    </div>
  );
};
