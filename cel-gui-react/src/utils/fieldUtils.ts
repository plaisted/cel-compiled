import { CelFieldDefinition } from '../types.ts';

export function flattenFields(
  fields: CelFieldDefinition[],
  prefix = ''
): CelFieldDefinition[] {
  const result: CelFieldDefinition[] = [];
  for (const field of fields) {
    const fullPath = prefix ? `${prefix}.${field.name}` : field.name;
    result.push({ ...field, name: fullPath });
    if (field.children) {
      result.push(...flattenFields(field.children, fullPath));
    }
  }
  return result;
}

export function groupFields(fields: CelFieldDefinition[]): {
  groups: Record<string, CelFieldDefinition[]>;
  ungrouped: CelFieldDefinition[];
} {
  const groups: Record<string, CelFieldDefinition[]> = {};
  const ungrouped: CelFieldDefinition[] = [];
  for (const field of fields) {
    const dot = field.name.indexOf('.');
    if (dot !== -1) {
      const topLevel = field.name.slice(0, dot);
      if (!groups[topLevel]) groups[topLevel] = [];
      groups[topLevel].push(field);
    } else {
      ungrouped.push(field);
    }
  }
  return { groups, ungrouped };
}
