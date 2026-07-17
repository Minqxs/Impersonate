import type { PropsWithChildren } from 'react';
import { CssBaseline, ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { theme } from './theme';
import { ActiveProjectProvider } from '../features/projects/ActiveProjectContext';

const queryClient = new QueryClient();

export function AppProviders({ children }: PropsWithChildren) {
  return <QueryClientProvider client={queryClient}><ThemeProvider theme={theme}><CssBaseline /><ActiveProjectProvider>{children}</ActiveProjectProvider></ThemeProvider></QueryClientProvider>;
}
