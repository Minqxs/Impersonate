import { createTheme } from '@mui/material/styles';

export const theme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#22d3ee' },
    secondary: { main: '#a78bfa' },
    background: { default: '#060b18', paper: '#0d1628' },
    divider: '#20304a',
    text: { primary: '#f1f5f9', secondary: '#94a3b8' },
  },
  shape: { borderRadius: 14 },
  typography: {
    fontFamily: 'Inter, ui-sans-serif, system-ui, sans-serif',
    h4: { fontWeight: 700 },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        html: { backgroundColor: '#060b18' },
        body: { backgroundColor: '#060b18' },
        '#root': { minHeight: '100vh', backgroundColor: '#060b18' },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          border: '1px solid #20304a',
          backgroundImage: 'linear-gradient(145deg, rgba(34,211,238,.035), transparent 55%)',
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        containedPrimary: { boxShadow: '0 0 20px rgba(34,211,238,.18)' },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: { background: '#091222', borderBottom: '1px solid #20304a' },
      },
    },
    MuiListItemButton: {
      styleOverrides: {
        root: {
          borderRadius: 10,
          '&.active, &.Mui-selected': {
            color: '#67e8f9',
            backgroundColor: 'rgba(34, 211, 238, 0.1)',
          },
          '&.active .MuiListItemIcon-root, &.Mui-selected .MuiListItemIcon-root': {
            color: '#67e8f9',
          },
        },
      },
    },
  },
});
