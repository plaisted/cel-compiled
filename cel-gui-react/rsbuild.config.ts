import { defineConfig } from '@rsbuild/core';
import { pluginReact } from '@rsbuild/plugin-react';

export default defineConfig({
  source: {
    entry: {
      index: './src/example/main.tsx',
    },
  },
  plugins: [pluginReact()],
});
