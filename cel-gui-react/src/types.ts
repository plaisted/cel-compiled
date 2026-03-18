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
  toCelString: (node: CelGuiNode) => Promise<string>;
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

export interface CelExpressionBuilderProps {
  defaultValue?: CelGuiNode;
  value?: CelGuiNode;
  onChange?: (node: CelGuiNode) => void;
  onSourceChange?: (source: string) => void;
  onModeChange?: (mode: CelBuilderMode) => void;
  mode?: CelBuilderMode;
  readOnly?: boolean;
  conversion?: CelConversionOptions;
  schema?: CelSchema;
  errors?: CelError[];
  layout?: 'standard' | 'natural';
}
