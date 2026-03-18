import { Diagnostic } from '@codemirror/lint';
import { EditorView } from '@codemirror/view';
import { CelError } from '../types.ts';

export function createCelLintSource(errors: CelError[] = []) {
  return (view: EditorView): Diagnostic[] => {
    return errors.map((err) => {
      // Calculate from/to positions
      let from = 0;
      let to = 0;

      if (err.position !== undefined) {
        from = err.position;
        to = from + (err.length || 1);
      } else if (err.line !== undefined) {
        // Fallback to line/column calculation if position is missing
        const line = view.state.doc.line(err.line);
        from = line.from + (err.column || 0);
        to = from + (err.length || 1);
      }

      // Constrain to document bounds
      from = Math.max(0, Math.min(from, view.state.doc.length));
      to = Math.max(from, Math.min(to, view.state.doc.length));

      return {
        from,
        to,
        severity: err.severity || 'error',
        message: err.message,
      };
    });
  };
}
