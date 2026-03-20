import React, { useCallback, useEffect, useRef, useState } from 'react';
import { CelGuiGroup, CelGuiNode, CelGuiRule } from '../types.ts';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { NodeRenderer } from './NodeRenderer.tsx';

export interface NaturalGroupNodeProps {
  node: CelGuiGroup;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
}

const GROUP_LOGIC_OPTIONS = [
  { value: 'and:false', label: 'All rules match', combinator: 'and', not: false },
  { value: 'or:false', label: 'Any rule matches', combinator: 'or', not: false },
  { value: 'and:true', label: 'No rules match', combinator: 'and', not: true },
  { value: 'or:true', label: 'Not all rules match', combinator: 'or', not: true },
] as const;

function getGroupAriaLabel(combinator: string, not: boolean): string {
  if (not && combinator === 'and') return 'No rules match';
  if (not && combinator === 'or') return 'Not all rules match';
  if (combinator === 'or') return 'Any rule matches';
  return 'All rules match';
}

function getGroupSummaryLabel(combinator: string, not: boolean): string {
  if (not && combinator === 'and') return 'No rules match';
  if (not && combinator === 'or') return 'Not all rules match';
  if (combinator === 'or') return 'Any rule matches';
  return 'All rules match';
}

export const NaturalGroupNode: React.FC<NaturalGroupNodeProps> = ({ node, onChange, onRemove }) => {
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

  const handleLogicModeChange = useCallback((e: React.ChangeEvent<HTMLSelectElement>) => {
    const next = GROUP_LOGIC_OPTIONS.find((option) => option.value === e.target.value);
    if (!next) return;
    onChange({ ...node, combinator: next.combinator, not: next.not });
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

  const combinatorLabel = getGroupSummaryLabel(node.combinator, !!node.not);
  const ariaLabel = getGroupAriaLabel(node.combinator, !!node.not);
  const logicModeValue = `${node.combinator}:${!!node.not}`;

  return (
    <div
      role="group"
      aria-label={ariaLabel}
      className={`cel-group cel-group--natural${node.not ? ' cel-group--not' : ''}`}
    >
      <div className="cel-group__natural-header">
        <div className="cel-group__natural-badges">
          {!readOnly ? (
            <label className="cel-group__logic-selector">
              <select
                value={logicModeValue}
                onChange={handleLogicModeChange}
                className={`cel-group__combinator-toggle cel-group__combinator-toggle--${node.combinator}${node.not ? ' cel-group__combinator-toggle--not' : ''}`}
                aria-label="Rule matching mode"
                title="Choose how this group should match rules"
              >
                {GROUP_LOGIC_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
          ) : (
            <span
              className={`cel-group__combinator-toggle cel-group__combinator-toggle--${node.combinator}${node.not ? ' cel-group__combinator-toggle--not' : ''} cel-group__combinator-toggle--readonly`}
            >
              {combinatorLabel}
            </span>
          )}
        </div>
        <div className="cel-group__header-right">
          {!readOnly && (
            <>
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
            </>
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
    </div>
  );
};
