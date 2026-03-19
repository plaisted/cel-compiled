import React, { useCallback, useEffect, useRef, useState } from 'react';
import { CelGuiGroup, CelGuiNode, CelGuiRule } from '../types.ts';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { NodeRenderer } from './NodeRenderer.tsx';

export interface NaturalGroupNodeProps {
  node: CelGuiGroup;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
}

function getGroupAriaLabel(combinator: string, not: boolean): string {
  if (not && combinator === 'and') return 'None of the following conditions';
  if (not && combinator === 'or') return 'Not any of the following conditions';
  if (combinator === 'or') return 'Any of the following conditions';
  return 'All of the following conditions';
}

export const NaturalGroupNode: React.FC<NaturalGroupNodeProps> = ({
  node,
  onChange,
  onRemove,
}) => {
  const { readOnly } = useCelBuilder();

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

  const handleCombinatorToggle = useCallback(() => {
    onChange({ ...node, combinator: node.combinator === 'and' ? 'or' : 'and' });
  }, [node, onChange]);

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

  const combinatorLabel = node.not
    ? node.combinator === 'and' ? 'NOT ALL' : 'NOT ANY'
    : node.combinator === 'and' ? 'ALL' : 'ANY';
  const ariaLabel = getGroupAriaLabel(node.combinator, !!node.not);

  return (
    <div
      role="group"
      aria-label={ariaLabel}
      className={`cel-group cel-group--natural${node.not ? ' cel-group--not' : ''}`}
    >
      <div className="cel-group__natural-header">
        <div className="cel-group__natural-badges">
          {!readOnly ? (
            <button
              type="button"
              className={`cel-group__combinator-toggle cel-group__combinator-toggle--${node.combinator}${node.not ? ' cel-group__combinator-toggle--not' : ''}`}
              onClick={handleCombinatorToggle}
              title="Click to toggle ALL / ANY"
            >
              {combinatorLabel}
            </button>
          ) : (
            <span className={`cel-group__combinator-toggle cel-group__combinator-toggle--${node.combinator}${node.not ? ' cel-group__combinator-toggle--not' : ''} cel-group__combinator-toggle--readonly`}>
              {combinatorLabel}
            </span>
          )}
        </div>
        <div className="cel-group__header-right">
          {!readOnly && (
            <button
              type="button"
              role="switch"
              aria-checked={!!node.not}
              aria-label="Toggle NOT modifier"
              className={`cel-group__not-switch${node.not ? ' cel-group__not-switch--on' : ''}`}
              onClick={handleNotToggle}
            >
              <span className="cel-group__not-switch__label">NOT</span>
              <span className="cel-group__not-switch__track" aria-hidden="true" />
            </button>
          )}
          {!readOnly && onRemove && (
            <button
              type="button"
              className="cel-group__remove cel-group__remove--natural"
              aria-label="Remove group"
              onClick={onRemove}
              title="Remove group"
            >
              ×
            </button>
          )}
        </div>
      </div>

      <div className="cel-group__rules cel-group__rules--natural">
        {node.rules.length === 0 ? (
          <div className="cel-group__empty-natural">No conditions yet</div>
        ) : (
          node.rules.map((rule, index) => (
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
          ))
        )}
      </div>

      {!readOnly && (
        <div className="cel-group__actions cel-group__actions--natural">
          <button
            ref={addRuleButtonRef}
            type="button"
            className="cel-group__add-rule"
            onClick={handleAddRule}
          >
            + condition
          </button>
          <span className="cel-group__actions-sep" aria-hidden="true">·</span>
          <button type="button" className="cel-group__add-group" onClick={handleAddGroup}>
            + group
          </button>
        </div>
      )}
    </div>
  );
};
