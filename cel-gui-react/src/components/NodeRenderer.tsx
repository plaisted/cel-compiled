import React from 'react';
import { CelGuiNode } from '../types.ts';
import { useCelBuilder } from '../context/CelBuilderContext.tsx';
import { GroupNode } from './GroupNode.tsx';
import { RuleNode } from './RuleNode.tsx';
import { MacroNode } from './MacroNode.tsx';
import { AdvancedNode } from './AdvancedNode.tsx';
import { NaturalGroupNode } from './NaturalGroupNode.tsx';
import { NaturalRuleNode } from './NaturalRuleNode.tsx';

export interface NodeRendererProps {
  node: CelGuiNode;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
}

export const NodeRenderer: React.FC<NodeRendererProps> = (props) => {
  const { node } = props;
  const { layout } = useCelBuilder();

  if (layout === 'natural') {
    switch (node.type) {
      case 'group':
        return <NaturalGroupNode {...props} node={node} />;
      case 'rule':
        return <NaturalRuleNode {...props} node={node} />;
      case 'macro':
        return <MacroNode {...props} node={node} />;
      case 'advanced':
        return <AdvancedNode {...props} node={node} />;
      default:
        return <div>Unknown node type: {(node as any).type}</div>;
    }
  }

  switch (node.type) {
    case 'group':
      return <GroupNode {...props} node={node} />;
    case 'rule':
      return <RuleNode {...props} node={node} />;
    case 'macro':
      return <MacroNode {...props} node={node} />;
    case 'advanced':
      return <AdvancedNode {...props} node={node} />;
    default:
      return <div>Unknown node type: {(node as any).type}</div>;
  }
};
