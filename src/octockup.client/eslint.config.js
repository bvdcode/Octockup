import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    rules: {
      '@typescript-eslint/no-explicit-any': 'error',
      'max-lines': [
        'error',
        {
          max: 400,
          skipBlankLines: true,
          skipComments: true,
        },
      ],
      'no-console': 'error',
      'no-restricted-globals': [
        'error',
        {
          name: 'localStorage',
          message: 'Use an application store or query cache instead.',
        },
      ],
      'no-restricted-properties': [
        'error',
        {
          object: 'window',
          property: 'localStorage',
          message: 'Use an application store or query cache instead.',
        },
        {
          object: 'globalThis',
          property: 'localStorage',
          message: 'Use an application store or query cache instead.',
        },
      ],
      'no-restricted-syntax': [
        'error',
        {
          selector: 'TSUnknownKeyword',
          message: 'Use a precise domain type and narrow external values.',
        },
        {
          selector: 'TSAsExpression > TSAsExpression',
          message: 'Nested type assertions are not allowed.',
        },
      ],
    },
  },
])
