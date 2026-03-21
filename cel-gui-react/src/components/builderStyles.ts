import type { CSSProperties } from 'react';
import type { CelThemeTokens } from '../types.ts';

export const THEME_TOKEN_TO_CSS_VAR: Record<keyof CelThemeTokens, string> = {
  surface: '--cel-surface',
  surfaceLow: '--cel-surface-low',
  surfaceMid: '--cel-surface-mid',
  surfaceHigh: '--cel-surface-high',
  surfaceHighest: '--cel-surface-highest',
  surfaceCard: '--cel-surface-card',
  surfaceCardSolid: '--cel-surface-card-solid',
  text: '--cel-text',
  textMuted: '--cel-text-muted',
  textSoft: '--cel-text-soft',
  outline: '--cel-outline',
  outlineStrong: '--cel-outline-strong',
  primary: '--cel-primary',
  primaryDim: '--cel-primary-dim',
  primarySoft: '--cel-primary-soft',
  secondary: '--cel-secondary',
  secondarySoft: '--cel-secondary-soft',
  tertiary: '--cel-tertiary',
  danger: '--cel-danger',
  dangerSoft: '--cel-danger-soft',
  success: '--cel-success',
  inverseSurface: '--cel-inverse-surface',
  inversePrimary: '--cel-inverse-primary',
  radiusSm: '--cel-radius-sm',
  radius: '--cel-radius',
  radiusMd: '--cel-radius-md',
  shadowAmbient: '--cel-shadow-ambient',
  shadowSoft: '--cel-shadow-soft',
  transition: '--cel-transition',
  ring: '--cel-ring',
};

export function buildCelRootStyle(
  theme?: Partial<CelThemeTokens>,
  style?: CSSProperties
): CSSProperties {
  const themeStyle: CSSProperties & Record<string, string> = {};

  if (theme) {
    for (const [token, value] of Object.entries(theme) as [keyof CelThemeTokens, string][]) {
      if (value === undefined) continue;
      themeStyle[THEME_TOKEN_TO_CSS_VAR[token]] = value;
    }
  }

  return { ...themeStyle, ...style };
}
