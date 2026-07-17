import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import PsychologyOutlinedIcon from '@mui/icons-material/PsychologyOutlined';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import WorkspacesOutlinedIcon from '@mui/icons-material/WorkspacesOutlined';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import PersonOutlineOutlinedIcon from '@mui/icons-material/PersonOutlineOutlined';
import { AppBar, Box, Drawer, List, ListItemButton, ListItemIcon, ListItemText, Toolbar, Typography } from '@mui/material';
import { NavLink, Outlet } from 'react-router-dom';
import { StatusIndicator } from '../components/StatusIndicator';

const drawerWidth = 232;
const navigation = [
  ['Projects', '/projects', <WorkspacesOutlinedIcon />], ['Dashboard', '/', <DashboardOutlinedIcon />], ['Runs', '/runs', <AccountTreeOutlinedIcon />], ['Brain', '/brain', <PsychologyOutlinedIcon />], ['Personality', '/personality', <PersonOutlineOutlinedIcon />], ['Settings', '/settings', <SettingsOutlinedIcon />],
] as const;

export function AppLayout() {
  return <Box className="min-h-screen bg-slate-50"><AppBar position="fixed" sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}><Toolbar><Typography variant="h6" component="h1" sx={{ flexGrow: 1, fontWeight: 700 }}>Impersonate</Typography><StatusIndicator /></Toolbar></AppBar><Drawer variant="permanent" sx={{ width: drawerWidth, flexShrink: 0, '& .MuiDrawer-paper': { width: drawerWidth, boxSizing: 'border-box' } }}><Toolbar /><Box className="flex h-full flex-col p-3"><Typography variant="overline" color="text.secondary" className="px-2">Navigation</Typography><List>{navigation.map(([label, path, icon]) => <ListItemButton key={path} component={NavLink} to={path} end={path === '/'}><ListItemIcon>{icon}</ListItemIcon><ListItemText primary={label} /></ListItemButton>)}</List><Box className="mt-auto rounded-lg bg-slate-100 p-3"><Typography variant="caption" color="text.secondary">Current project</Typography><Typography variant="body2">Not selected</Typography><Typography variant="caption" color="text.secondary" className="mt-3 block">Current personality</Typography><Typography variant="body2">Not configured</Typography></Box></Box></Drawer><Box component="main" sx={{ ml: `${drawerWidth}px` }}><Toolbar /><div className="mx-auto max-w-6xl p-6 md:p-10"><Outlet /></div></Box></Box>;
}
