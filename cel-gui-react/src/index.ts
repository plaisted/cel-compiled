export const version = '0.1.0';

// Types
export * from './types';
export type { CelCodeEditorProps } from './editor/CelCodeEditor.ts';
export type { CelFunctionMetadata, CelFunctionBundle } from './editor/function-catalog.ts';

// Hooks
export { useCelExpression } from './hooks/useCelExpression.ts';
export type { UseCelExpressionOptions } from './hooks/useCelExpression.ts';
export { useCelConversion } from './hooks/useCelConversion.ts';

// Schema context
export { CelSchemaProvider, useCelSchema } from './context/CelSchemaContext.tsx';
export type { CelSchemaProviderProps } from './context/CelSchemaContext.tsx';

// Components
export { CelExpressionBuilder } from './components/CelExpressionBuilder.tsx';
export { NodeRenderer } from './components/NodeRenderer.tsx';
export type { NodeRendererProps } from './components/NodeRenderer.tsx';
export { GroupNode } from './components/GroupNode.tsx';
export type { GroupNodeProps } from './components/GroupNode.tsx';
export { RuleNode } from './components/RuleNode.tsx';
export type { RuleNodeProps } from './components/RuleNode.tsx';
export { MacroNode } from './components/MacroNode.tsx';
export type { MacroNodeProps } from './components/MacroNode.tsx';
export { AdvancedNode } from './components/AdvancedNode.tsx';
export type { AdvancedNodeProps } from './components/AdvancedNode.tsx';
export { NaturalGroupNode } from './components/NaturalGroupNode.tsx';
export type { NaturalGroupNodeProps } from './components/NaturalGroupNode.tsx';
export { NaturalRuleNode } from './components/NaturalRuleNode.tsx';
export type { NaturalRuleNodeProps } from './components/NaturalRuleNode.tsx';

// Utilities
export { flattenFields, groupFields } from './utils/fieldUtils.ts';
