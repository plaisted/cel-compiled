import React, { useCallback, useEffect, useMemo, useRef } from 'react';
import CodeMirror from '@uiw/react-codemirror';
import { acceptCompletion, autocompletion } from '@codemirror/autocomplete';
import { linter } from '@codemirror/lint';
import { Compartment, Prec } from '@codemirror/state';
import { EditorView, keymap } from '@codemirror/view';
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

  // Stable extensions — only rebuild when schema changes, not on every errors update.
  const extensions = useMemo(
    () => [
      celLanguage,
      autocompletion({ override: [createCelCompletionSource(schema)] }),
      Prec.highest(keymap.of([{ key: 'Tab', run: acceptCompletion }])),
      lintCompartment.current.of(linter(createCelLintSource([]))),
    ],
    [schema]
  );

  // Update diagnostics via compartment reconfiguration instead of rebuilding extensions.
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
    // Populate initial diagnostics now that the view exists.
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
      readOnly={readOnly}
      extensions={extensions}
      className={className}
      placeholder={placeholder}
      onCreateEditor={onCreateEditor}
      indentWithTab={false}
      basicSetup={{
        lineNumbers: false,
        foldGutter: false,
        highlightActiveLine: false,
      }}
    />
  );
};

export default CelCodeEditor;
