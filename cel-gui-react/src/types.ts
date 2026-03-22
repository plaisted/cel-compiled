import type { CSSProperties, ReactNode } from 'react';

export type CelGuiNodeType = 'group' | 'rule' | 'macro' | 'advanced';

export interface CelGuiBaseNode {
  type: CelGuiNodeType;
  /** Client-side only — not part of the backend JSON contract. */
  id?: string;
  /** Client-side only — not part of the backend JSON contract. */
  metadata?: Record<string, unknown>;
}

export interface CelGuiGroup extends CelGuiBaseNode {
  type: 'group';
  combinator: 'and' | 'or';
  not: boolean;
  rules: CelGuiNode[];
}

export interface CelGuiRule extends CelGuiBaseNode {
  type: 'rule';
  field: string;
  operator: string;
  value: any;
}

export interface CelGuiMacro extends CelGuiBaseNode {
  type: 'macro';
  macro: string;
  field: string;
}

export interface CelGuiAdvanced extends CelGuiBaseNode {
  type: 'advanced';
  expression: string;
}

export type CelGuiNode = CelGuiGroup | CelGuiRule | CelGuiMacro | CelGuiAdvanced;

export type CelExtensionBundle =
  | 'string'
  | 'list'
  | 'math'
  | 'set'
  | 'base64'
  | 'regex'
  | 'optional';

export interface CelFieldDefinition {
  name: string; // dot-path, e.g. "user.age"
  label?: string; // human-readable label
  type?:
    | 'string'
    | 'number'
    | 'boolean'
    | 'duration'
    | 'timestamp'
    | 'bytes'
    | 'list'
    | 'map';
  children?: CelFieldDefinition[]; // nested fields for dot-completion
}

export interface CelSchema {
  fields: CelFieldDefinition[];
  extensions?: CelExtensionBundle[];
}

// ─── Value node types ─────────────────────────────────────────────────────────

export type CelValueType =
  | 'string'
  | 'number'
  | 'boolean'
  | 'timestamp'
  | 'duration'
  | 'bytes'
  | 'list'
  | 'map'
  | 'any';

export type CelGuiValueNodeType =
  | 'field-ref'
  | 'literal'
  | 'concat'
  | 'arithmetic'
  | 'conditional'
  | 'transform'
  | 'advanced-value';

export interface CelGuiValueBaseNode {
  type: CelGuiValueNodeType;
}

export interface CelGuiFieldRefNode extends CelGuiValueBaseNode {
  type: 'field-ref';
  field: string;
}

export interface CelGuiLiteralNode extends CelGuiValueBaseNode {
  type: 'literal';
  value: any;
  valueType: CelValueType;
}

export interface CelGuiConcatNode extends CelGuiValueBaseNode {
  type: 'concat';
  operands: CelGuiValueNode[];
}

export interface CelGuiArithmeticNode extends CelGuiValueBaseNode {
  type: 'arithmetic';
  operator: '+' | '-' | '*' | '/';
  left: CelGuiValueNode;
  right: CelGuiValueNode;
}

export interface CelGuiConditionalNode extends CelGuiValueBaseNode {
  type: 'conditional';
  condition: CelGuiNode; // filter node model
  then: CelGuiValueNode;
  otherwise: CelGuiValueNode;
}

export interface CelGuiTransformNode extends CelGuiValueBaseNode {
  type: 'transform';
  operand: CelGuiValueNode;
  transform: string;
  args: CelGuiValueNode[];
}

export interface CelGuiAdvancedValueNode extends CelGuiValueBaseNode {
  type: 'advanced-value';
  expression: string;
}

export type CelGuiValueNode =
  | CelGuiFieldRefNode
  | CelGuiLiteralNode
  | CelGuiConcatNode
  | CelGuiArithmeticNode
  | CelGuiConditionalNode
  | CelGuiTransformNode
  | CelGuiAdvancedValueNode;

// ─── Expression-family-aware root ─────────────────────────────────────────────

export interface CelGuiFilterRoot {
  kind: 'filter';
  root: CelGuiNode;
}

export interface CelGuiValueRoot {
  kind: 'value';
  resultType: CelValueType;
  root: CelGuiValueNode;
}

export type CelGuiExpressionNode = CelGuiFilterRoot | CelGuiValueRoot;

// ─── Conversion options ───────────────────────────────────────────────────────

export interface CelConversionOptions {
  toCelString: (node: CelGuiExpressionNode, pretty?: boolean) => Promise<string>;
  toGuiModel: (source: string, kind?: 'filter' | 'value', resultType?: CelValueType) => Promise<CelGuiExpressionNode>;
}

export type CelBuilderMode = 'visual' | 'source' | 'auto';

export interface CelError {
  message: string;
  line?: number;
  column?: number;
  position?: number;
  length?: number;
  severity?: 'error' | 'warning' | 'info';
}

export interface CelThemeTokens {
  surface: string;
  surfaceLow: string;
  surfaceMid: string;
  surfaceHigh: string;
  surfaceHighest: string;
  surfaceCard: string;
  surfaceCardSolid: string;
  text: string;
  textMuted: string;
  textSoft: string;
  outline: string;
  outlineStrong: string;
  primary: string;
  primaryDim: string;
  primarySoft: string;
  secondary: string;
  secondarySoft: string;
  tertiary: string;
  danger: string;
  dangerSoft: string;
  success: string;
  inverseSurface: string;
  inversePrimary: string;
  radiusSm: string;
  radius: string;
  radiusMd: string;
  shadowAmbient: string;
  shadowSoft: string;
  transition: string;
  ring: string;
}

export interface CelExpressionBuilderProps {
  kind?: 'filter' | 'value';
  /** Declared result type for value expressions. Used when `kind="value"` and no `defaultValue` is provided. */
  resultType?: CelValueType;
  defaultValue?: CelGuiExpressionNode;
  value?: CelGuiExpressionNode;
  onChange?: (node: CelGuiExpressionNode) => void;
  onSourceChange?: (source: string) => void;
  onModeChange?: (mode: CelBuilderMode) => void;
  onPrettyChange?: (pretty: boolean) => void;
  editorMode?: CelBuilderMode;
  pretty?: boolean;
  readOnly?: boolean;
  conversion?: CelConversionOptions;
  schema?: CelSchema;
  errors?: CelError[];
  className?: string;
  style?: CSSProperties;
  theme?: Partial<CelThemeTokens>;
}

export interface CelVisualBuilderProps {
  defaultValue?: CelGuiExpressionNode;
  value?: CelGuiExpressionNode;
  onChange?: (node: CelGuiExpressionNode) => void;
  readOnly?: boolean;
  schema?: CelSchema;
  className?: string;
  style?: CSSProperties;
  theme?: Partial<CelThemeTokens>;
  emptyState?: ReactNode;
}
