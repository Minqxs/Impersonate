import { getContrastRatio } from '@mui/material/styles';
import { describe, expect, it } from 'vitest';
import { AppLayout } from '../layouts/AppLayout';
import { theme } from './theme';

describe('application theme baseline', () => {
  it('uses a dark shell with readable primary text', () => {
    expect(theme.palette.mode).toBe('dark');
    expect(theme.palette.background.default).toBe('#060b18');
    expect(getContrastRatio(theme.palette.text.primary, theme.palette.background.default)).toBeGreaterThan(7);

    const shell = AppLayout();
    expect(shell.props.sx).toMatchObject({
      bgcolor: 'background.default',
      color: 'text.primary',
      minHeight: '100vh',
    });
    expect(shell.props.className).toBeUndefined();
  });
});
