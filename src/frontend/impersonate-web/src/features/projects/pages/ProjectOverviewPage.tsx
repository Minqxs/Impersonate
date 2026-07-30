import { Alert, Box, Button, Card, CardContent, CircularProgress, Stack, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { getProject, getProjectHealth } from '../api/projectsApi';
import { listRuns, runKeys } from '../../runs/api/runsApi';
import { MetricCard } from '../components/MetricCard';

export function ProjectOverviewPage() {
  const { projectId = '' } = useParams();
  const project = useQuery({ queryKey: ['project', projectId], queryFn: () => getProject(projectId), enabled: !!projectId });
  const health = useQuery({ queryKey: ['project-health', projectId], queryFn: () => getProjectHealth(projectId), enabled: !!projectId });
  const runs = useQuery({ queryKey: runKeys.all(projectId), queryFn: () => listRuns(projectId), enabled: !!projectId });
  if (project.isPending || runs.isPending) return <CircularProgress aria-label="Loading project overview" />;
  if (project.isError || !project.data) return <Alert severity="error">This project is unavailable.</Alert>;
  const allRuns = runs.data ?? [];
  const tasks = allRuns.flatMap(run => run.tasks);
  const latest = allRuns.slice().sort((a, b) => Date.parse(b.createdAtUtc) - Date.parse(a.createdAtUtc))[0];
  const approved = tasks.filter(task => task.status === 'Approved').length;
  const blocked = allRuns.filter(run => ['WaitingForClarification', 'WaitingForInfrastructure', 'Failed'].includes(run.status)).length + tasks.filter(task => task.status === 'Failed').length;
  const deliveries = tasks.filter(task => task.delivery).length;
  const deliveryReady = tasks.filter(task => task.deliveryEligible && !task.delivery).length;
  return <Stack spacing={3}>
    <Box><Typography variant="h4">Overview</Typography><Typography color="text.secondary">Operational progress for {project.data.name}.</Typography></Box>
    <Box display="grid" gridTemplateColumns={{ xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(4, 1fr)' }} gap={2}>
      <MetricCard label="Operational status" value={project.data.status} detail={`${project.data.defaultBranch} default branch`} />
      <MetricCard label="Configuration health" value={health.isError ? 'Unavailable' : health.data?.overallStatus ?? 'Loading'} detail={`${health.data?.checks.length ?? 0} configuration checks`} />
      <MetricCard label="Pipeline runs" value={allRuns.length} detail={`${allRuns.filter(run => run.status === 'Executing').length} executing · ${allRuns.filter(run => run.status === 'ReadyForDelivery').length} ready for delivery`} />
      <MetricCard label="Approved tasks" value={approved} detail={`${tasks.length} total planned tasks`} />
      <MetricCard label="Latest run" value={latest ? readableState(latest.status) : 'None'} detail={latest ? `${latest.loop.currentStage} · ${summarise(latest.featureRequest)}` : 'Create the first pipeline run'} action={latest && <Button component={Link} to={`/projects/${projectId}/runs/${latest.id}/overview`} size="small" sx={{ px: 0 }}>Open latest run</Button>} />
      <MetricCard label="Delivery" value={deliveries} detail={`${deliveryReady} eligible · read-only foundation`} />
      <MetricCard label="Blocked work" value={blocked} detail={blocked ? 'Review runs and project health' : 'No blocked work'} />
      <MetricCard label="Code quality" value="Not configured" detail="Optional analytics arrive in Quick Win B" />
    </Box>
    <Card variant="outlined"><CardContent><Typography variant="h6">Project identity</Typography><Typography sx={{ overflowWrap: 'anywhere' }}>{project.data.repositoryUrl}</Typography><Typography color="text.secondary">Default branch: {project.data.defaultBranch}</Typography></CardContent></Card>
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} flexWrap="wrap">
      <Button component={Link} to={`/projects/${projectId}/runs/new`} variant="contained">Create run</Button>
      <Button component={Link} to={`/projects/${projectId}/runs`}>View runs</Button>
      <Button component={Link} to={`/projects/${projectId}/delivery`}>View delivery</Button>
      <Button component={Link} to={`/projects/${projectId}/health`}>View health</Button>
    </Stack>
  </Stack>;
}

function readableState(value: string) { return value.replace(/([a-z])([A-Z])/g, '$1 $2'); }
function summarise(value: string) { const normalised=value.replace(/\s+/g,' ').trim(); return normalised.length>110 ? `${normalised.slice(0,107)}…` : normalised; }
