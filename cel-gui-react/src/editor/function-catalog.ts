export interface CelFunctionMetadata {
  name: string;
  description?: string;
  isMacro?: boolean;
  isReceiver?: boolean;
}

export interface CelFunctionBundle {
  id: string;
  functions: CelFunctionMetadata[];
}

export const functionCatalog: CelFunctionBundle[] = [
  {
    id: 'builtins',
    functions: [
      { name: 'size', isReceiver: true },
      { name: 'has' },
      { name: 'int' },
      { name: 'uint' },
      { name: 'double' },
      { name: 'string' },
      { name: 'bytes' },
      { name: 'bool' },
      { name: 'duration' },
      { name: 'timestamp' },
      { name: 'type' },
      { name: 'contains', isReceiver: true },
      { name: 'startsWith', isReceiver: true },
      { name: 'endsWith', isReceiver: true },
      { name: 'matches', isReceiver: true },
    ],
  },
  {
    id: 'macros',
    functions: [
      { name: 'all', isMacro: true, isReceiver: true },
      { name: 'exists', isMacro: true, isReceiver: true },
      { name: 'exists_one', isMacro: true, isReceiver: true },
      { name: 'map', isMacro: true, isReceiver: true },
      { name: 'filter', isMacro: true, isReceiver: true },
    ],
  },
  {
    id: 'string',
    functions: [
      { name: 'trim', isReceiver: true },
      { name: 'lowerAscii', isReceiver: true },
      { name: 'upperAscii', isReceiver: true },
      { name: 'replace', isReceiver: true },
      { name: 'split', isReceiver: true },
      { name: 'join', isReceiver: true },
      { name: 'charAt', isReceiver: true },
      { name: 'indexOf', isReceiver: true },
      { name: 'lastIndexOf', isReceiver: true },
      { name: 'reverse', isReceiver: true },
      { name: 'format' },
    ],
  },
  {
    id: 'list',
    functions: [
      { name: 'flatten', isReceiver: true },
      { name: 'sort', isReceiver: true },
      { name: 'range' },
    ],
  },
  {
    id: 'math',
    functions: [
      { name: 'sqrt' },
      { name: 'ceil' },
      { name: 'floor' },
      { name: 'abs' },
      { name: 'round' },
      { name: 'math.greatest' },
      { name: 'math.least' },
    ],
  },
  {
    id: 'set',
    functions: [
      { name: 'sets.contains' },
      { name: 'sets.equivalent' },
      { name: 'sets.intersects' },
    ],
  },
  {
    id: 'base64',
    functions: [{ name: 'base64.encode' }, { name: 'base64.decode' }],
  },
  {
    id: 'regex',
    functions: [{ name: 'regex.extract' }, { name: 'regex.replace' }],
  },
  {
    id: 'optional',
    functions: [
      { name: 'optional.of' },
      { name: 'optional.ofNonZero' },
      { name: 'optional.none' },
      { name: 'hasValue', isReceiver: true },
      { name: 'orValue', isReceiver: true },
      { name: 'or', isReceiver: true },
    ],
  },
];
