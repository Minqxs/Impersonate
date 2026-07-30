import { useEffect } from 'react';
import { Box, Chip, CircularProgress, Paper, Stack, Tab, Tabs, Typography } from '@mui/material';
import { NavLink, Navigate, Outlet, useLocation, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getProject } from '../api/projectsApi';
import { useActiveProject } from '../ActiveProjectContext';

const navigation = [
  ['Overview', 'dashboard'], ['Runs', 'runs'], ['Delivery', 'delivery'],
  ['Code Quality', 'quality'], ['Health', 'health'], ['Settings', 'settings'],
] as const;

export function ProjectWorkspaceLayout() {
  const { projectId } = useParams();
  const location = useLocation();
  const { activeProjectId, setActiveProject } = useActiveProject();
  const project = useQuery({ queryKey: ['project', projectId], queryFn: ({ signal }) => getProject(projectId!, signal), enabled: !!projectId, retry: false });
  useEffect(() => { if (project.data && activeProjectId !== projectId) setActiveProject(project.data.id); }, [activeProjectId, project.data, projectId, setActiveProject]);
  if (!projectId || project.isError) return <Navigate to="/projects" replace />;
  if (project.isLoading) return <CircularProgress aria-label="Loading project workspace" />;
  const segment = navigation.find(([, path]) => location.pathname.includes(`/projects/${projectId}/${path}`))?.[1] ?? 'dashboard';
  return <Stack spacing={3} minWidth={0}>
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      <Stack direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" spacing={2} p={{ xs: 2, md: 2.5 }}>
        <Box minWidth={0}>
          <Typography variant="overline" color="text.secondary">Project workspace</Typography>
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap">
            <Typography variant="h5">{project.data?.name}</Typography><Chip size="small" label={project.data?.status} />
          </Stack>
          <Typography variant="body2" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>{project.data?.repositoryUrl} · {project.data?.defaultBranch}</Typography>
        </Box>
      </Stack>
      <Tabs value={segment} variant="scrollable" scrollButtons="auto" allowScrollButtonsMobile aria-label="Project navigation" sx={{ borderTop: 1, borderColor: 'divider', '& .MuiTabs-flexContainer': { minWidth: 'max-content' } }}>
        {navigation.map(([label, path]) => <Tab key={path} value={path} label={label} component={NavLink} to={path} />)}
      </Tabs>
    </Paper>
    <Outlet />
  </Stack>;
}
