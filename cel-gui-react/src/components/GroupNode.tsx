import React, { useCallback, useRef, useEffect, useState } from 'react';
import { CelGuiGroup, CelGuiNode, CelGuiRule } from '../types.ts';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { NodeRenderer } from './NodeRenderer.tsx';

export interface GroupNodeProps {
  node: CelGuiGroup;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
  readOnly?: boolean;
}

function getGroupAriaLabel(combinator: string, not: boolean): string {
  if (not && combinator === 'and') return 'None of the following conditions';
  if (not && combinator === 'or') return 'Not any of the following conditions';
  if (combinator === 'or') return 'Any of the following conditions';
  return 'All of the following conditions';
}

export const GroupNode: React.FC<GroupNodeProps> = ({
  node,
  onChange,
  onRemove,
  readOnly: readOnlyProp,
}) => {
  const { readOnly: contextReadOnly } = useCelBuilder();
  const readOnly = readOnlyProp ?? contextReadOnly;

  const [lastAddedId, setLastAddedId] = useState<string | null>(null);
  const prevLengthRef = useRef(node.rules.length);
  const nodeContainersRef = useRef<Map<string, HTMLDivElement>>(new Map());
  const addRuleButtonRef = useRef<HTMLButtonElement>(null);
  const [focusTarget, setFocusTarget] = useState<
    { type: 'node'; id: string } | { type: 'addButton' } | null
  >(null);

  useEffect(() => {
    if (node.rules.length < prevLengthRef.current) {
      setLastAddedId(null);
    }
    prevLengthRef.current = node.rules.length;
  }, [node.rules.length]);

  useEffect(() => {
    if (!focusTarget) return;
    if (focusTarget.type === 'addButton') {
      addRuleButtonRef.current?.focus();
    } else {
      const container = nodeContainersRef.current.get(focusTarget.id);
      if (container) {
        const focusable = container.querySelector<HTMLElement>(
          'input, select, button, [tabindex="0"]'
        );
        focusable?.focus();
      }
    }
    setFocusTarget(null);
  }, [focusTarget]);

  const handleCombinatorChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      onChange({ ...node, combinator: e.target.value as 'and' | 'or' });
    },
    [node, onChange]
  );

  const handleNotToggle = useCallback(() => {
    onChange({ ...node, not: !node.not });
  }, [node, onChange]);

  const handleAddRule = useCallback(() => {
    const id = crypto.randomUUID();
    const newRule: CelGuiRule = {
      type: 'rule',
      id,
      field: '',
      operator: '==',
      value: '',
    };
    setLastAddedId(id);
    setFocusTarget({ type: 'node', id });
    onChange({ ...node, rules: [...node.rules, newRule] });
  }, [node, onChange]);

  const handleAddGroup = useCallback(() => {
    const id = crypto.randomUUID();
    const newGroup: CelGuiGroup = {
      type: 'group',
      id,
      combinator: 'and',
      not: false,
      rules: [],
    };
    setLastAddedId(id);
    setFocusTarget({ type: 'node', id });
    onChange({ ...node, rules: [...node.rules, newGroup] });
  }, [node, onChange]);

  const handleRuleChange = useCallback(
    (index: number, updatedNode: CelGuiNode) => {
      const newRules = [...node.rules];
      newRules[index] = updatedNode;
      onChange({ ...node, rules: newRules });
    },
    [node, onChange]
  );

  const handleRemoveRule = useCallback(
    (index: number) => {
      const newRules = node.rules.filter((_, i) => i !== index);
      if (newRules.length === 0) {
        setFocusTarget({ type: 'addButton' });
      } else {
        const prevIndex = Math.max(0, index - 1);
        const targetId = newRules[prevIndex]?.id;
        if (targetId) setFocusTarget({ type: 'node', id: targetId });
      }
      onChange({ ...node, rules: newRules });
    },
    [node, onChange]
  );

  const ariaLabel = getGroupAriaLabel(node.combinator, !!node.not);

  return (
    <div
      role="group"
      aria-label={ariaLabel}
      className={`cel-group ${node.not ? 'cel-group--not' : ''}`}
    >
      <div className="cel-group__header">
        {!readOnly && (
          <button
            type="button"
            className="cel-group__not-toggle"
            aria-label="Toggle NOT modifier"
            onClick={handleNotToggle}
          >
            NOT
          </button>
        )}
        <select
          className="cel-group__combinator"
          value={node.combinator}
          onChange={handleCombinatorChange}
          disabled={readOnly}
        >
          <option value="and">AND</option>
          <option value="or">OR</option>
        </select>
        {!readOnly && onRemove && (
          <button
            type="button"
            className="cel-group__remove"
            aria-label="Remove group"
            onClick={onRemove}
          >
            Remove Group
          </button>
        )}
      </div>
      <div className="cel-group__rules">
        {node.rules.map((rule, index) => (
          <div
            key={rule.id ?? index}
            ref={(el) => {
              const key = rule.id ?? String(index);
              if (el) nodeContainersRef.current.set(key, el);
              else nodeContainersRef.current.delete(key);
            }}
            className={rule.id === lastAddedId ? 'cel-node--entering' : undefined}
          >
            <NodeRenderer
              node={rule}
              onChange={(updated) => handleRuleChange(index, updated)}
              onRemove={() => handleRemoveRule(index)}
            />
          </div>
        ))}
      </div>
      {!readOnly && (
        <div className="cel-group__actions">
          <button
            ref={addRuleButtonRef}
            type="button"
            className="cel-group__add-rule"
            onClick={handleAddRule}
          >
            Add Rule
          </button>
          <button
            type="button"
            className="cel-group__add-group"
            onClick={handleAddGroup}
          >
            Add Group
          </button>
        </div>
      )}
    </div>
  );
};
