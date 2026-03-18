import {
  CompletionContext,
  CompletionResult,
  Completion,
} from '@codemirror/autocomplete';
import { CelSchema, CelFieldDefinition, CelExtensionBundle } from '../types.ts';
import { functionCatalog } from './function-catalog.ts';

const ALL_EXTENSION_BUNDLES: CelExtensionBundle[] = [
  'string',
  'list',
  'math',
  'set',
  'base64',
  'regex',
  'optional',
];

export function createCelCompletionSource(schema?: CelSchema) {
  return (context: CompletionContext): CompletionResult | null => {
    const word = context.matchBefore(/[a-zA-Z0-9_.]*/);
    if (!word || (word.from === word.to && !context.explicit)) return null;

    const options: Completion[] = [];
    const fullWord = word.text;
    const isDotCompletion = fullWord.includes('.');

    if (isDotCompletion) {
      // Handle dot-completion for schema fields and receiver methods
      const parts = fullWord.split('.');
      const prefix = parts.slice(0, -1).join('.');

      // 1. Schema fields dot-completion
      const findFields = (
        fields: CelFieldDefinition[],
        currentPath: string
      ): CelFieldDefinition[] => {
        for (const f of fields) {
          if (f.name === currentPath) return f.children || [];
          if (currentPath.startsWith(f.name + '.') && f.children) {
            const subPath = currentPath.slice(f.name.length + 1);
            return findFields(f.children, subPath);
          }
        }
        return [];
      };

      if (schema?.fields) {
        const fields = findFields(schema.fields, prefix);
        for (const f of fields) {
          options.push({
            label: f.name,
            type: 'property',
            detail: f.type,
          });
        }
      }

      // 2. Receiver methods from catalog
      const enabledBundles = schema?.extensions ?? ALL_EXTENSION_BUNDLES;
      for (const bundle of functionCatalog) {
        if (
          bundle.id === 'builtins' ||
          bundle.id === 'macros' ||
          enabledBundles.includes(bundle.id as CelExtensionBundle)
        ) {
          for (const fn of bundle.functions) {
            if (fn.isReceiver) {
              options.push({
                label: fn.name,
                type: 'method',
                detail: fn.isMacro ? 'macro' : 'function',
              });
            }
          }
        }
      }

      return {
        from: word.from + prefix.length + 1,
        options,
      };
    } else {
      // 1. Top-level schema fields
      if (schema?.fields) {
        for (const f of schema.fields) {
          options.push({
            label: f.name,
            type: 'variable',
            detail: f.type,
          });
        }
      }

      // 2. Top-level functions (not receivers)
      const enabledBundles = schema?.extensions ?? ALL_EXTENSION_BUNDLES;
      for (const bundle of functionCatalog) {
        if (
          bundle.id === 'builtins' ||
          bundle.id === 'macros' ||
          enabledBundles.includes(bundle.id as CelExtensionBundle)
        ) {
          for (const fn of bundle.functions) {
            if (!fn.isReceiver) {
              options.push({
                label: fn.name,
                type: 'function',
                detail: fn.isMacro ? 'macro' : 'function',
              });
            }
          }
        }
      }

      // 3. Keywords
      const keywords = ['true', 'false', 'null', 'in'];
      for (const kw of keywords) {
        options.push({ label: kw, type: 'keyword' });
      }

      return {
        from: word.from,
        options,
      };
    }
  };
}
