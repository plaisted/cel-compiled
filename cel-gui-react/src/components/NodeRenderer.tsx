import React from 'react';
import { CelGuiNode } from '../types.ts';
import { MacroNode } from './MacroNode.tsx';
import { AdvancedNode } from './AdvancedNode.tsx';
import { NaturalGroupNode } from './NaturalGroupNode.tsx';
import { NaturalRuleNode } from './NaturalRuleNode.tsx';

export interface NodeRendererProps {
  node: CelGuiNode;
  onChange: (node: CelGuiNode) => void;
  onRemove?: () => void;
  onPromote?: () => void;
  depth?: number;
}

export const NodeRenderer: React.FC<NodeRendererProps> = (props) => {
  const { node, depth = 0 } = props;

  switch (node.type) {
    case 'group':
      return <NaturalGroupNode {...props} node={node} depth={depth} />;
    case 'rule':
      return <NaturalRuleNode {...props} node={node} />;
    case 'macro':
      return <MacroNode {...props} node={node} />;
    case 'advanced':
      return <AdvancedNode {...props} node={node} />;
    default:
      return <div>Unknown node type: {(node as any).type}</div>;
  }
};
