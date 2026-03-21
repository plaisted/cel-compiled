import React, { useCallback, useEffect, useMemo, useRef } from 'react';
import CodeMirror from '@uiw/react-codemirror';
import { history, historyKeymap, defaultKeymap } from '@codemirror/commands';
import { indentOnInput, syntaxHighlighting, HighlightStyle, bracketMatching, indentUnit } from '@codemirror/language';
import { EditorState, Compartment, Prec } from '@codemirror/state';
import { EditorView, keymap, highlightSpecialChars, drawSelection, dropCursor, rectangularSelection, crosshairCursor } from '@codemirror/view';
import { autocompletion, completionKeymap, closeBrackets, closeBracketsKeymap, acceptCompletion } from '@codemirror/autocomplete';
import { linter, lintKeymap } from '@codemirror/lint';
import { highlightSelectionMatches, searchKeymap } from '@codemirror/search';
import { tags as t } from '@lezer/highlight';

import { CelSchema, CelError } from '../types.ts';
import { celLanguage } from './cel-language.ts';
import { createCelCompletionSource } from './completion.ts';
import { createCelLintSource } from './lint.ts';

export interface CelCodeEditorProps {
  value?: string;
  onChange?: (value: string) => void;
  readOnly?: boolean;
  schema?: CelSchema;
  errors?: CelError[];
  className?: string;
  placeholder?: string;
}

const celHighlightStyle = HighlightStyle.define([
  { tag: t.keyword, color: '#0053db', fontWeight: 'bold' },
  { tag: t.operator, color: '#7c3aed', fontWeight: '500' }, // Purple/Indigo for operators
  { tag: t.string, color: '#059669' }, // Green for strings
  { tag: t.number, color: '#d97706' }, // Orange for numbers
  { tag: [t.comment, t.lineComment, t.blockComment], color: '#9ca3af', fontStyle: 'italic' },
  { tag: [t.variableName, t.name, t.propertyName], color: '#334155' }, // Slate for identifiers
]);

const celTheme = EditorView.theme({
  '&': { height: '100%', minHeight: '1.5rem', fontSize: '0.78rem' },
  '&.cm-focused': { outline: 'none' },
  '& .cm-scroller': { outline: 'none !important' },
  '& .cm-content': { 
    caretColor: '#111827 !important', 
    padding: '0.5rem 0', 
    outline: 'none !important' 
  },
  '& .cm-content:focus': { outline: 'none !important' },
  '& .cm-line': { outline: 'none !important' },
  '.cm-cursor, .cm-dropCursor': { borderLeftColor: '#111827', borderLeftWidth: '2px' },
  '.cm-gutters': { display: 'none' },
});

export const CelCodeEditor: React.FC<CelCodeEditorProps> = ({
  value,
  onChange,
  readOnly,
  schema,
  errors = [],
  className,
  placeholder,
}) => {
  const viewRef = useRef<EditorView | null>(null);
  const lintCompartment = useRef(new Compartment());

  const extensions = useMemo(
    () => [
      celLanguage,
      celTheme,
      syntaxHighlighting(celHighlightStyle),
      history(),
      highlightSpecialChars(),
      drawSelection(),
      dropCursor(),
      EditorState.allowMultipleSelections.of(true),
      indentOnInput(),
      bracketMatching(),
      closeBrackets(),
      autocompletion({ override: [createCelCompletionSource(schema)] }),
      rectangularSelection(),
      crosshairCursor(),
      highlightSelectionMatches(),
      Prec.highest(keymap.of([{ key: 'Tab', run: acceptCompletion }])),
      keymap.of([
        ...closeBracketsKeymap,
        ...defaultKeymap,
        ...searchKeymap,
        ...historyKeymap,
        ...completionKeymap,
        ...lintKeymap,
      ]),
      lintCompartment.current.of(linter(createCelLintSource([]))),
      indentUnit.of('  '),
    ],
    [schema]
  );

  useEffect(() => {
    if (!viewRef.current) return;
    viewRef.current.dispatch({
      effects: lintCompartment.current.reconfigure(
        linter(createCelLintSource(errors))
      ),
    });
  }, [errors]);

  const onCreateEditor = useCallback((view: EditorView) => {
    viewRef.current = view;
    view.dispatch({
      effects: lintCompartment.current.reconfigure(
        linter(createCelLintSource(errors))
      ),
    });
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <CodeMirror
      value={value}
      onChange={onChange}
      theme="light"
      readOnly={readOnly}
      extensions={extensions}
      className={className}
      placeholder={placeholder}
      onCreateEditor={onCreateEditor}
      indentWithTab={false}
      basicSetup={false}
    />
  );
};

export default CelCodeEditor;
