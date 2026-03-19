import React, { useState, useCallback, useRef } from 'react';
import ReactDOM from 'react-dom/client';
import {
  CelExpressionBuilder,
  CelSchema,
  CelGuiNode,
  CelError,
  CelBuilderMode,
} from '../index.ts';
import '../cel-gui.css';
import './example.css';

const API_BASE = 'http://localhost:5089';

const DEFAULT_SCHEMA_JSON = JSON.stringify(
  {
    fields: [
      {
        name: 'user',
        label: 'User',
        children: [
          { name: 'age', label: 'Age', type: 'number' },
          { name: 'name', label: 'Name', type: 'string' },
          { name: 'isActive', label: 'Is Active', type: 'boolean' },
        ],
      },
      { name: 'tags', label: 'Tags', type: 'list' },
    ],
    extensions: ['string', 'list', 'math'],
  },
  null,
  2
);

const DEFAULT_CONTEXT_JSON = JSON.stringify(
  {
    user: { age: 25, name: 'Alice', isActive: true },
    tags: ['admin', 'user'],
  },
  null,
  2
);

const DEFAULT_NODE: CelGuiNode = {
  type: 'group',
  combinator: 'and',
  not: false,
  rules: [{ type: 'rule', field: 'user.age', operator: '>=', value: 18 }],
};

const App = () => {
  const [currentNode, setCurrentNode] = useState<CelGuiNode>(DEFAULT_NODE);
  const [validationErrors, setValidationErrors] = useState<CelError[]>([]);
  const [evalErrors, setEvalErrors] = useState<CelError[]>([]);

  // Schema editor
  const [schemaJson, setSchemaJson] = useState(DEFAULT_SCHEMA_JSON);
  const [schema, setSchema] = useState<CelSchema>(() => JSON.parse(DEFAULT_SCHEMA_JSON));
  const [schemaError, setSchemaError] = useState<string | null>(null);

  // Context editor
  const [contextJson, setContextJson] = useState(DEFAULT_CONTEXT_JSON);
  const [contextError, setContextError] = useState<string | null>(null);

  // Track source text and mode so Evaluate always uses the latest CEL string
  const builderModeRef = useRef<CelBuilderMode>('auto');
  const builderSourceRef = useRef('');

  // Evaluation
  const [evalResult, setEvalResult] = useState<{ result: unknown; type: string } | null>(null);
  const [evalError, setEvalError] = useState<string | null>(null);
  const [isEvaluating, setIsEvaluating] = useState(false);

  const conversion = {
    toCelString: async (node: CelGuiNode) => {
      const res = await fetch(`${API_BASE}/api/cel/to-cel-string`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(node),
      });
      if (!res.ok) throw new Error('Failed to convert to CEL string');
      return res.text();
    },
    toGuiModel: async (source: string) => {
      const res = await fetch(`${API_BASE}/api/cel/to-gui-model`, {
        method: 'POST',
        body: source,
      });
      if (!res.ok) throw new Error('Failed to convert to GUI model');
      return res.json();
    },
  };

  const validate = useCallback(async (node: CelGuiNode) => {
    try {
      const source = await conversion.toCelString(node);
      const res = await fetch(`${API_BASE}/api/cel/validate`, {
        method: 'POST',
        body: source,
      });
      if (res.ok) setValidationErrors(await res.json());
    } catch {
      // Validation errors are non-fatal
    }
  }, []);

  const handleChange = (node: CelGuiNode) => {
    setCurrentNode(node);
    setEvalErrors([]);
    validate(node);
  };

  const handleSchemaChange = (json: string) => {
    setSchemaJson(json);
    try {
      setSchema(JSON.parse(json));
      setSchemaError(null);
    } catch {
      setSchemaError('Invalid JSON');
    }
  };

  const handleEvaluate = async () => {
    let contextObj: unknown;
    try {
      contextObj = JSON.parse(contextJson);
      setContextError(null);
    } catch {
      setContextError('Invalid JSON');
      return;
    }

    setIsEvaluating(true);
    setEvalError(null);
    setEvalResult(null);
    setEvalErrors([]);
    try {
      // In source mode use the editor text directly; otherwise convert from the node.
      const source =
        builderModeRef.current === 'source'
          ? builderSourceRef.current
          : await conversion.toCelString(currentNode);
      const res = await fetch(`${API_BASE}/api/cel/evaluate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ expression: source, context: contextObj }),
      });
      const data = await res.json();
      if (!res.ok) {
        const errs: CelError[] = data.errors ?? [{ message: data.error ?? 'Evaluation failed', severity: 'error' as const }];
        setEvalErrors(errs);
        setEvalError(errs.map((e) => e.message).join('; '));
      } else {
        setEvalResult(data);
      }
    } catch (e) {
      setEvalError(String(e));
    } finally {
      setIsEvaluating(false);
    }
  };

  return (
    <div className="ex-page">
      <div className="ex-header">
        <h2 className="ex-header__title">CEL Expression Builder</h2>
      </div>
      <p className="ex-header__subtitle">
        Edit the schema and context below, build an expression, then hit Evaluate.
      </p>

      {/* Schema + Context editors */}
      <div className="ex-editors">
        <div className="ex-editor-panel">
          <label className="ex-editor-panel__label">
            Schema <span className="ex-editor-panel__label-hint">(JSON)</span>
          </label>
          <textarea
            value={schemaJson}
            onChange={(e) => handleSchemaChange(e.target.value)}
            rows={14}
            className={`ex-editor-panel__textarea${schemaError ? ' ex-editor-panel__textarea--error' : ''}`}
            spellCheck={false}
          />
          <div aria-live="polite" role="status">
            {schemaError && (
              <div className="ex-editor-panel__error">{schemaError}</div>
            )}
          </div>
        </div>

        <div className="ex-editor-panel">
          <label className="ex-editor-panel__label">
            Context / State <span className="ex-editor-panel__label-hint">(JSON)</span>
          </label>
          <textarea
            value={contextJson}
            onChange={(e) => { setContextJson(e.target.value); setContextError(null); }}
            rows={14}
            className={`ex-editor-panel__textarea${contextError ? ' ex-editor-panel__textarea--error' : ''}`}
            spellCheck={false}
          />
          <div aria-live="polite" role="status">
            {contextError && (
              <div className="ex-editor-panel__error">{contextError}</div>
            )}
          </div>
        </div>
      </div>

      {/* Expression builder */}
      <CelExpressionBuilder
        value={currentNode}
        onChange={handleChange}
        onSourceChange={(s) => { builderSourceRef.current = s; }}
        onModeChange={(m) => { builderModeRef.current = m; }}
        schema={schema}
        conversion={conversion}
        errors={[...validationErrors, ...evalErrors]}
      />

      {/* Evaluate bar */}
      <div className="ex-eval-bar">
        <button
          className="ex-eval-btn"
          onClick={handleEvaluate}
          disabled={isEvaluating}
        >
          {isEvaluating ? 'Evaluating…' : 'Evaluate'}
        </button>

        <div className="ex-result" aria-live="polite" role="status">
          {evalResult !== null && (
            <span className="ex-result__badge">
              Result:&nbsp;
              <span className="ex-result__value">{JSON.stringify(evalResult.result)}</span>
              <span className="ex-result__type">{evalResult.type}</span>
            </span>
          )}
          {evalError && (
            <span className="ex-result__error">{evalError}</span>
          )}
        </div>
      </div>

      {/* JSON model collapsible */}
      <details className="ex-model-details">
        <summary>Current JSON Model</summary>
        <pre className="ex-model-details__pre">
          {JSON.stringify(currentNode, null, 2)}
        </pre>
      </details>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
