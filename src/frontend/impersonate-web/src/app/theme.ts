import { createTheme } from '@mui/material/styles';

export const theme = createTheme({
  colorSchemes: { dark: true },
  palette: { primary: { main: '#2563eb' }, background: { default: '#f8fafc', paper: '#ffffff' } },
  shape: { borderRadius: 10 },
});
