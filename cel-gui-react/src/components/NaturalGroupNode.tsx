import React, { useCallback, useEffect, useId, useRef, useState } from 'react';
import { CelGuiGroup, CelGuiNode, CelGuiRule } from '../types.ts';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { NodeRenderer } from './NodeRenderer.tsx';
import { DeleteIcon } from './DeleteIcon.tsx';

export interface NaturalGroupNodeProps {
  node: CelGuiGroup;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
  depth?: number;
}

const GROUP_LOGIC_OPTIONS = [
  { value: 'and:false', label: 'all', combinator: 'and', not: false },
  { value: 'or:false', label: 'any', combinator: 'or', not: false },
  { value: 'and:true', label: 'none', combinator: 'and', not: true },
  { value: 'or:true', label: 'not all', combinator: 'or', not: true },
] as const;

function getLogicModeWord(combinator: string, not: boolean): string {
  if (not && combinator === 'and') return 'none';
  if (not && combinator === 'or') return 'not all';
  if (combinator === 'or') return 'any';
  return 'all';
}

function getGroupAriaLabel(combinator: string, not: boolean): string {
  if (not && combinator === 'and') return 'No rules match';
  if (not && combinator === 'or') return 'Not all rules match';
  if (combinator === 'or') return 'Any rule matches';
  return 'All rules match';
}

export const NaturalGroupNode: React.FC<NaturalGroupNodeProps> = ({
  node,
  onChange,
  onRemove,
  depth = 0,
}) => {
  const { readOnly } = useCelBuilder();

  const [lastAddedId, setLastAddedId] = useState<string | null>(
    () => (node.metadata?.lastAddedId as string | undefined) ?? null
  );
  const [isLogicMenuOpen, setIsLogicMenuOpen] = useState(false);
  const prevLengthRef = useRef(node.rules.length);
  const nodeContainersRef = useRef<Map<string, HTMLDivElement>>(new Map());
  const addRuleButtonRef = useRef<HTMLButtonElement>(null);
  const logicMenuRef = useRef<HTMLDivElement>(null);
  const logicListboxId = useId();
  const [focusTarget, setFocusTarget] = useState<
    { type: 'node'; id: string } | { type: 'addButton' } | null
  >(() => (node.metadata?.lastAddedId ? { type: 'node', id: node.metadata.lastAddedId as string } : null));

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

  useEffect(() => {
    if (!isLogicMenuOpen) return;

    const handlePointerDown = (event: MouseEvent) => {
      if (!logicMenuRef.current?.contains(event.target as Node)) {
        setIsLogicMenuOpen(false);
      }
    };

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsLogicMenuOpen(false);
      }
    };

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleEscape);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [isLogicMenuOpen]);

  const handleSelectLogicMode = useCallback((value: string) => {
    const next = GROUP_LOGIC_OPTIONS.find((option) => option.value === value);
    if (!next) return;
    onChange({ ...node, combinator: next.combinator, not: next.not });
    setIsLogicMenuOpen(false);
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

  const handlePromoteRule = useCallback(
    (index: number) => {
      const targetNode = node.rules[index];
      if (!targetNode) return;

      const groupId = crypto.randomUUID();
      const newRuleId = crypto.randomUUID();

      const newRule: CelGuiRule = {
        type: 'rule',
        id: newRuleId,
        field: '',
        operator: '==',
        value: '',
      };

      const wrappedGroup: CelGuiGroup = {
        type: 'group',
        id: groupId,
        combinator: 'and',
        not: false,
        rules: [targetNode, newRule],
        metadata: {
          lastAddedId: newRuleId,
        },
      };

      const newRules = [...node.rules];
      newRules[index] = wrappedGroup;
      setFocusTarget({ type: 'node', id: groupId });
      onChange({ ...node, rules: newRules });
    },
    [node, onChange]
  );

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
  const logicModeValue = `${node.combinator}:${!!node.not}`;
  const logicModeWord = getLogicModeWord(node.combinator, !!node.not);
  const isRootGroup = depth === 0;

  return (
    <div
      role="group"
      aria-label={ariaLabel}
      className={`cel-group cel-group--natural${node.not ? ' cel-group--not' : ''}`}
    >
      <div className="cel-group__natural-header">
        <div className="cel-group__natural-badges">
          {!readOnly ? (
            <div className="cel-group__logic-selector" ref={logicMenuRef}>
              {isRootGroup && <span className="cel-group__logic-prefix">if</span>}
              <button
                type="button"
                className={`cel-group__combinator-toggle cel-group__combinator-toggle--${node.combinator}${node.not ? ' cel-group__combinator-toggle--not' : ''}`}
                aria-label="Rule matching mode"
                aria-haspopup="listbox"
                aria-expanded={isLogicMenuOpen}
                aria-controls={logicListboxId}
                title="Choose how this group should match rules"
                onClick={() => setIsLogicMenuOpen((open) => !open)}
              >
                {logicModeWord}
              </button>
              {isLogicMenuOpen && (
                <div className="cel-group__logic-menu">
                  <ul
                    id={logicListboxId}
                    className="cel-group__logic-options"
                    role="listbox"
                    aria-label="Rule matching mode options"
                  >
                    {GROUP_LOGIC_OPTIONS.map((option) => (
                      <li key={option.value}>
                        <button
                          type="button"
                          role="option"
                          aria-selected={option.value === logicModeValue}
                          className={`cel-group__logic-option${
                            option.value === logicModeValue ? ' cel-group__logic-option--selected' : ''
                          }`}
                          onClick={() => handleSelectLogicMode(option.value)}
                        >
                          {option.label}
                        </button>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
              {isRootGroup && <span className="cel-group__logic-suffix">match</span>}
            </div>
          ) : (
            <span className="cel-group__logic-selector cel-group__logic-selector--readonly">
              {isRootGroup && <span className="cel-group__logic-prefix">if</span>}
              <span
                className={`cel-group__combinator-toggle cel-group__combinator-toggle--${node.combinator}${node.not ? ' cel-group__combinator-toggle--not' : ''} cel-group__combinator-toggle--readonly`}
              >
                {logicModeWord}
              </span>
              {isRootGroup && <span className="cel-group__logic-suffix">match</span>}
            </span>
          )}
        </div>
        <div className="cel-group__header-right">
          {!readOnly && onRemove && (
            <button
              type="button"
              className="cel-group__remove cel-group__remove--natural"
              aria-label="Remove group"
              onClick={onRemove}
              title="Remove group"
            >
              <DeleteIcon />
            </button>
          )}
        </div>
      </div>

      <div className={`cel-group__rules cel-group__rules--natural${!readOnly ? ' cel-group__rules--has-add' : ''}`}>
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
                onPromote={rule.type === 'rule' ? () => handlePromoteRule(index) : undefined}
                depth={depth + 1}
              />
            </div>
          ))
        )}
      </div>

      {!readOnly && (
        <div
          className={`cel-group__add-anchor${node.rules.length > 0 ? ' cel-group__add-anchor--connected' : ''}`}
        >
          <button
            ref={addRuleButtonRef}
            type="button"
            className="cel-group__add-rule"
            aria-label="Add condition"
            title="Add condition"
            onClick={handleAddRule}
          >
            +
          </button>
        </div>
      )}
    </div>
  );
};
