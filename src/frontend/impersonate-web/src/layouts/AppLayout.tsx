import WorkspacesOutlinedIcon from '@mui/icons-material/WorkspacesOutlined';
import {
  AppBar,
  Box,
  Drawer,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Typography,
} from '@mui/material';
import { NavLink, Outlet } from 'react-router-dom';
import { StatusIndicator } from '../components/StatusIndicator';
import { ProjectSelector } from '../features/projects/components/ProjectSelector';

const drawerWidth = 232;
const navigation = [['Projects', '/projects', <WorkspacesOutlinedIcon />]] as const;

export function AppLayout() {
  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default', color: 'text.primary' }}>
      <AppBar position="fixed" sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography variant="h6" component="h1" sx={{ flexGrow: 1, fontWeight: 700 }}>
            Impersonate
          </Typography>
          <ProjectSelector />
          <Box sx={{ width: 12 }} />
          <StatusIndicator />
        </Toolbar>
      </AppBar>

      <Drawer
        variant="permanent"
        sx={{
          width: drawerWidth,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: drawerWidth,
            boxSizing: 'border-box',
            bgcolor: 'background.paper',
            borderColor: 'divider',
          },
        }}
      >
        <Toolbar />
        <Box className="flex h-full flex-col p-3">
          <Typography variant="overline" color="text.secondary" className="px-2">
            Navigation
          </Typography>
          <List>
            {navigation.map(([label, path, icon]) => (
              <ListItemButton key={path} component={NavLink} to={path}>
                <ListItemIcon>{icon}</ListItemIcon>
                <ListItemText primary={label} />
              </ListItemButton>
            ))}
          </List>
          <Box
            className="mt-auto rounded-lg p-3"
            sx={{ bgcolor: 'action.hover', border: 1, borderColor: 'divider' }}
          >
            <Typography variant="caption" color="text.secondary">
              Project scope
            </Typography>
            <Typography variant="body2">Choose a project from the header.</Typography>
          </Box>
        </Box>
      </Drawer>

      <Box component="main" sx={{ ml: `${drawerWidth}px`, minHeight: '100vh' }}>
        <Toolbar />
        <Box className="mx-auto max-w-6xl p-6 md:p-10">
          <Outlet />
        </Box>
      </Box>
    </Box>
  );
}
