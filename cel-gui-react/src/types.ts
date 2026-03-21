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

export interface CelConversionOptions {
  toCelString: (node: CelGuiNode, pretty?: boolean) => Promise<string>;
  toGuiModel: (source: string) => Promise<CelGuiNode>;
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
  defaultValue?: CelGuiNode;
  value?: CelGuiNode;
  onChange?: (node: CelGuiNode) => void;
  onSourceChange?: (source: string) => void;
  onModeChange?: (mode: CelBuilderMode) => void;
  onPrettyChange?: (pretty: boolean) => void;
  mode?: CelBuilderMode;
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
  defaultValue?: CelGuiNode;
  value?: CelGuiNode;
  onChange?: (node: CelGuiNode) => void;
  readOnly?: boolean;
  schema?: CelSchema;
  className?: string;
  style?: CSSProperties;
  theme?: Partial<CelThemeTokens>;
  emptyState?: ReactNode;
}
