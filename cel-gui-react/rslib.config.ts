import { defineConfig } from '@rslib/core';
import { pluginReact } from '@rsbuild/plugin-react';

export default defineConfig({
  source: {
    entry: {
      index: './src/index.ts',
      editor: './src/editor/index.ts',
    },
  },
  lib: [
    {
      format: 'esm',
      dts: true,
    },
    {
      format: 'cjs',
    },
  ],
  output: {
    target: 'web',
  },
  plugins: [pluginReact()],
});
