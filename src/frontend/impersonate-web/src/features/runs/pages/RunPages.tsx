import { Alert, Box, Button, Card, CardContent, Chip, CircularProgress, FormControl, InputLabel, MenuItem, Select, Stack, TextField, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { cancelRun, createRun, getPlannerReadiness, getRun, getTimeline, listRuns, runKeys, startPlanning } from '../api/runsApi';

const terminal = ['ReadyForExecution', 'WaitingForClarification', 'Completed', 'CompletedWithSkippedTasks', 'Failed', 'Cancelled'];
function State({ value }: { value: string }) { return <Chip size="small" label={value.replace(/([a-z])([A-Z])/g, '$1 $2')} color={value.includes('Completed') || value === 'ReadyForExecution' || value === 'Succeeded' ? 'success' : value === 'Failed' || value === 'ProviderFailed' ? 'error' : value === 'WaitingForClarification' || value === 'InvalidOutput' || value === 'TimedOut' ? 'warning' : 'primary'} variant="outlined" />; }

export function RunsPage() {
  const { projectId = '' } = useParams(); const [status, setStatus] = useState('');
  const query = useQuery({ queryKey: [...runKeys.all(projectId), status], queryFn: () => listRuns(projectId, status), refetchInterval: result => result.state.data?.some(run => !terminal.includes(run.status) && run.status !== 'Created') ? 5000 : false });
  return <Stack spacing={3}><Box className="flex items-center justify-between"><div><Typography variant="h4">Pipeline runs</Typography><Typography color="text.secondary">Project-scoped feature planning and delivery.</Typography></div><Button component={Link} to="new" variant="contained">Create run</Button></Box><FormControl size="small" sx={{ maxWidth: 260 }}><InputLabel>Status</InputLabel><Select label="Status" value={status} onChange={event => setStatus(event.target.value)}><MenuItem value="">All statuses</MenuItem>{['Created', 'Planning', 'ReadyForExecution', 'WaitingForClarification', 'Failed', 'Cancelled'].map(value => <MenuItem key={value} value={value}>{value}</MenuItem>)}</Select></FormControl>{query.isPending ? <CircularProgress /> : query.isError ? <Alert severity="error">{query.error.message}</Alert> : query.data?.length === 0 ? <Alert severity="info">No pipeline runs exist yet.</Alert> : query.data?.map(run => <Card key={run.id}><CardContent><Stack direction="row" justifyContent="space-between"><div><Typography variant="h6">{run.featureRequest}</Typography><Typography color="text.secondary">{run.loop.currentStage} · {run.tasks.length} tasks</Typography></div><Stack alignItems="end"><State value={run.status} /><Button component={Link} to={run.id}>Open details</Button></Stack></Stack></CardContent></Card>)}</Stack>;
}

export function CreateRunPage() {
  const { projectId = '' } = useParams(); const navigate = useNavigate(); const [value, setValue] = useState('');
  const mutation = useMutation({ mutationFn: () => createRun(projectId, value), onSuccess: run => navigate(`/projects/${projectId}/runs/${run.id}`) });
  function submit(event: FormEvent) { event.preventDefault(); if (value.trim()) mutation.mutate(); }
  return <Stack component="form" onSubmit={submit} spacing={3} maxWidth={720}><Typography variant="h4">Create pipeline run</Typography><Alert severity="info">Create a run, then explicitly start model-powered planning.</Alert><TextField label="Feature request" multiline minRows={5} value={value} onChange={event => setValue(event.target.value)} required inputProps={{ maxLength: 4000 }} helperText={`${value.length}/4000`} />{mutation.isError && <Alert severity="error">{mutation.error.message}</Alert>}<Button type="submit" variant="contained" disabled={mutation.isPending || !value.trim()}>{mutation.isPending ? 'Creating…' : 'Create run'}</Button></Stack>;
}

export function RunDetailPage() {
  const { projectId = '', pipelineRunId = '' } = useParams(); const queryClient = useQueryClient();
  const run = useQuery({ queryKey: runKeys.detail(projectId, pipelineRunId), queryFn: () => getRun(projectId, pipelineRunId), refetchInterval: query => query.state.data?.status === 'Planning' ? 3000 : false });
  const timeline = useQuery({ queryKey: runKeys.timeline(projectId, pipelineRunId), queryFn: () => getTimeline(projectId, pipelineRunId), refetchInterval: run.data?.status === 'Planning' ? 3000 : false });
  const readiness = useQuery({ queryKey: [...runKeys.readiness(),projectId], queryFn:()=>getPlannerReadiness(projectId), staleTime: 30000 });
  const refresh = () => { queryClient.invalidateQueries({ queryKey: runKeys.detail(projectId, pipelineRunId) }); queryClient.invalidateQueries({ queryKey: runKeys.timeline(projectId, pipelineRunId) }); queryClient.invalidateQueries({ queryKey: runKeys.all(projectId) }); };
  const planning = useMutation({ mutationFn: () => startPlanning(projectId, pipelineRunId), onSuccess: refresh });
  const cancel = useMutation({ mutationFn: () => cancelRun(projectId, pipelineRunId), onSuccess: refresh });
  if (run.isPending) return <CircularProgress />; if (run.isError) return <Alert severity="error">{run.error.message}</Alert>;
  const current = run.data; const plannerReady = readiness.data?.status === 'Ready';
  return <Stack spacing={3}>
    <Stack direction="row" justifyContent="space-between"><div><Typography variant="h4">Pipeline run</Typography><Typography variant="h6" mt={2}>{current.featureRequest}</Typography></div><State value={current.status} /></Stack>
    {current.status === 'Created' && <Card><CardContent><Typography variant="h6">Planner readiness</Typography>{readiness.isPending ? <CircularProgress size={20} /> : readiness.isError ? <Alert severity="warning">Planner readiness could not be checked. The API will still validate configuration.</Alert> : <Alert severity={plannerReady ? 'success' : 'warning'}>{readiness.data?.message}</Alert>}<Typography color="text.secondary" my={2}>Planning sends project metadata and this feature request to the configured model. Repository files are not inspected. Both API and Worker require matching configuration.</Typography><Button variant="contained" onClick={() => planning.mutate()} disabled={planning.isPending || readiness.isPending || !plannerReady}>{planning.isPending ? 'Starting…' : 'Start Planning'}</Button></CardContent></Card>}
    {planning.isError && <Alert severity="error">Planning could not start: {planning.error.message}</Alert>}
    {current.status === 'Planning' && <Alert icon={<CircularProgress size={18} />} severity="info">Planner execution is queued or active. This page refreshes automatically.</Alert>}
    {current.status === 'WaitingForClarification' && <Alert severity="warning"><strong>Clarification required.</strong> {current.stopReason} Create a new run with the requested detail; same-run clarification is not available yet.</Alert>}
    {current.status === 'Failed' && <Alert severity="error">Planning failed: {current.failureReason}</Alert>}
    {current.status === 'ReadyForExecution' && <Alert severity="success">The task plan has been generated. Coding-agent execution will be introduced in the next milestone.</Alert>}
    <Card><CardContent><Typography variant="h6">Planning status</Typography><Typography>Stage: {current.loop.currentStage}</Typography><Typography color="text.secondary">Run state: {current.status} · generated tasks: {current.tasks.length}</Typography></CardContent></Card>
    <Typography variant="h5">Generated task plan</Typography>{current.tasks.length === 0 ? <Alert severity="info">No planned tasks yet.</Alert> : current.tasks.slice().sort((a, b) => a.sequence - b.sequence).map(task => <Card key={task.id}><CardContent><Stack direction="row" justifyContent="space-between"><Typography variant="h6">{task.sequence}. {task.title}</Typography><State value={task.status} /></Stack><Typography my={1}>{task.description}</Typography><Typography fontWeight={600}>Acceptance criteria</Typography><Box component="ul" mt={0}>{task.acceptanceCriteria.map(criterion => <li key={criterion}>{criterion}</li>)}</Box></CardContent></Card>)}
    {current.planningAttempts.length > 0 && <><Typography variant="h5">Planning attempts</Typography>{current.planningAttempts.map(attempt => <Card key={attempt.attemptNumber}><CardContent><Stack direction="row" justifyContent="space-between"><Typography fontWeight={600}>Attempt {attempt.attemptNumber} · {attempt.provider} / {attempt.model}</Typography><State value={attempt.status} /></Stack><Typography variant="body2">Prompt: {attempt.promptVersion}</Typography>{attempt.failureMessage && <Alert severity="warning" sx={{ mt: 1 }}>{attempt.failureCode ? `${attempt.failureCode}: ` : ''}{attempt.failureMessage}</Alert>}</CardContent></Card>)}</>}
    {!terminal.includes(current.status) && current.status !== 'Created' && <Button color="error" variant="outlined" onClick={() => cancel.mutate()} disabled={cancel.isPending}>Cancel run</Button>}
    <Typography variant="h5">Timeline</Typography>{timeline.isPending ? <CircularProgress /> : timeline.data?.slice().sort((a, b) => a.sequence - b.sequence).map(event => <Box key={event.id} borderLeft={2} borderColor="divider" pl={2}><Typography fontWeight={600}>{event.eventType}</Typography><Typography>{event.message}</Typography><Typography variant="caption">{new Date(event.createdAtUtc).toLocaleString()}</Typography></Box>)}
  </Stack>;
}
