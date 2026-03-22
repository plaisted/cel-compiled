import { CelGuiNode, CelGuiExpressionNode } from '../types.ts';

function hasText(value: unknown): boolean {
  return typeof value === 'string' ? value.trim().length > 0 : !!value;
}

export function sanitizeNodeForConversion(node?: CelGuiNode | null): CelGuiNode | undefined {
  if (!node) return undefined;

  switch (node.type) {
    case 'rule':
      return hasText(node.field) ? node : undefined;
    case 'macro':
      return hasText(node.field) ? node : undefined;
    case 'advanced':
      return hasText(node.expression) ? node : undefined;
    case 'group': {
      const rules = node.rules
        .map((rule) => sanitizeNodeForConversion(rule))
        .filter((rule): rule is CelGuiNode => !!rule);

      if (rules.length === 0) return undefined;
      return { ...node, rules };
    }
    default:
      return node;
  }
}

export function sanitizeExpressionForConversion(node?: CelGuiExpressionNode | null): CelGuiExpressionNode | undefined {
  if (!node) return undefined;
  if (node.kind === 'filter') {
    const sanitized = sanitizeNodeForConversion(node.root);
    return sanitized ? { kind: 'filter', root: sanitized } : undefined;
  }
  // Value expressions are always sent as-is (advanced-value with empty expression is valid)
  return node;
}
