import { StreamLanguage } from '@codemirror/language';
import { tags as t } from '@lezer/highlight';

const keywords = ['true', 'false', 'null', 'in'];
const logicOperators = ['&&', '||', '!'];
const operators = [
  '==',
  '!=',
  '>=',
  '<=',
  '>',
  '<',
  '?.',
  '??',
  '?',
  ':',
  '.',
  ',',
  '(',
  ')',
  '[',
  ']',
  '{',
  '}',
];

export const celLanguage = StreamLanguage.define({
  name: 'cel',
  token(stream) {
    if (stream.eatSpace()) return null;

    // Comments
    if (stream.match('//')) {
      stream.skipToEnd();
      return 'comment';
    }

    // Strings
    if (stream.match(/^"([^"\\]|\\.)*"/)) return 'string';
    if (stream.match(/^'([^'\\]|\\.)*'/)) return 'string';

    // Bytes
    if (stream.match(/^b"([^"\\]|\\.)*"/)) return 'string';
    if (stream.match(/^b'([^'\\]|\\.)*'/)) return 'string';

    // Numbers
    if (stream.match(/^[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?/)) return 'number';
    if (stream.match(/^0x[0-9a-fA-F]+/)) return 'number';

    // Logic Operators
    for (const op of logicOperators) {
      if (stream.match(op)) return 'logicOperator';
    }

    // Operators
    for (const op of operators) {
      if (stream.match(op)) return 'operator';
    }

    // Keywords and Identifiers
    if (stream.match(/^[a-zA-Z_][a-zA-Z0-9_]*/)) {
      const word = stream.current();
      if (keywords.includes(word)) return 'keyword';
      return 'variableName';
    }

    stream.next();
    return null;
  },
  tokenTable: {
    comment: t.comment,
    string: t.string,
    number: t.number,
    operator: t.operator,
    logicOperator: t.keyword,
    keyword: t.keyword,
    variableName: t.variableName,
  },
});
