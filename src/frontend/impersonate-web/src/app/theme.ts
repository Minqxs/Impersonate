import { createTheme } from '@mui/material/styles';

export const theme = createTheme({
  palette: { mode:'dark', primary:{main:'#22d3ee'}, secondary:{main:'#a78bfa'}, background:{default:'#060b18',paper:'#0d1628'}, divider:'#20304a' },
  shape:{borderRadius:14},
  typography:{fontFamily:'Inter, ui-sans-serif, system-ui, sans-serif',h4:{fontWeight:700}},
  components:{MuiCard:{styleOverrides:{root:{border:'1px solid #20304a',backgroundImage:'linear-gradient(145deg, rgba(34,211,238,.035), transparent 55%)'}}},MuiButton:{styleOverrides:{containedPrimary:{boxShadow:'0 0 20px rgba(34,211,238,.18)'}}},MuiAppBar:{styleOverrides:{root:{background:'#091222',borderBottom:'1px solid #20304a'}}}}
});
