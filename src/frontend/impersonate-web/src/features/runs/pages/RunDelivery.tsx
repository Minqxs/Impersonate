import { Alert, Box, Button, Stack, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { StateChip } from '../components/StateChip';
import { mergeRunToMain, retryDelivery } from '../api/runsApi';
import { useRunDetail } from '../layouts/runDetailContext';

export function RunDelivery() {
  const { run, refresh } = useRunDetail();
  const retry = useMutation({ mutationFn: (deliveryId: string) => retryDelivery(run.projectId, run.id, deliveryId), onSuccess: refresh });
  const merge = useMutation({ mutationFn: () => mergeRunToMain(run.projectId, run.id), onSuccess: refresh });
  return <Stack spacing={2}>
    <Typography variant="h5">Delivery</Typography>
    <Alert severity="info">This run owns one integration branch. Each task is reviewed and integrated independently; one aggregate pull request targets the default branch only after final validation and exact-head review.</Alert>
    {run.runDelivery ? <Box border={1} borderColor="divider" borderRadius={1} p={2}>
      <Stack direction="row" justifyContent="space-between"><Typography fontWeight={600}>Run integration</Typography><StateChip value={run.runDelivery.status} /></Stack>
      <Typography>Run branch: {run.runDelivery.runBranchName}</Typography>
      <Typography>Target branch: {run.runDelivery.sourceDefaultBranch}</Typography>
      {run.runDelivery.runBranchHeadSha && <Typography>Current head: {run.runDelivery.runBranchHeadSha}</Typography>}
      {run.runDelivery.finalPullRequestUrl && <Typography component="a" href={run.runDelivery.finalPullRequestUrl} target="_blank" rel="noreferrer">Final pull request: {run.runDelivery.finalPullRequestRepository} #{run.runDelivery.finalPullRequestNumber}</Typography>}
      {run.runDelivery.finalPullRequestMergeableState && <Typography>Mergeability: {run.runDelivery.finalPullRequestMergeableState}; required checks: {run.runDelivery.requiredChecksState}</Typography>}
      {run.runDelivery.failureMessage && <Alert severity="error" sx={{ mt: 1 }}>{run.runDelivery.failureCode}: {run.runDelivery.failureMessage}</Alert>}
      {run.runDelivery.status === 'ReadyForMain' ? <Button sx={{ mt: 1 }} variant="contained" color="success" disabled={merge.isPending} onClick={() => window.confirm(`Merge the reviewed aggregate pull request into ${run.runDelivery!.sourceDefaultBranch}?`) && merge.mutate()}>Merge to main</Button> : <Alert severity="info" sx={{ mt: 1 }}>{run.runDelivery.status === 'Merged' ? 'The final pull request is merged.' : 'Merge to main becomes available only after the final pull request is mergeable and all required checks pass.'}</Alert>}
    </Box> : <Alert severity="warning">Run integration delivery has not been materialised. No run branch or final pull request has been created.</Alert>}
    {run.tasks.length === 0 ? <Alert severity="info">No task delivery state is available.</Alert> : run.tasks.map(task =>
      <Box key={task.id} border={1} borderColor="divider" borderRadius={1} p={2}>
        <Stack direction="row" justifyContent="space-between"><Typography fontWeight={600}>{task.sequence}. {task.title}</Typography><StateChip value={task.delivery?.status ?? (task.deliveryEligible ? 'Eligible' : 'Blocked')} /></Stack>
        {(task.deliveryBlockingDependencyIds?.length ?? 0) > 0 && <Typography>Waiting for merged dependencies: {task.deliveryBlockingDependencyIds?.join(', ')}</Typography>}
        {task.delivery?.branchName && <Typography>Branch: {task.delivery.branchName}</Typography>}
        {task.delivery?.commitSha && <Typography>Commit: {task.delivery.commitSha}</Typography>}
        {task.delivery?.remoteBranchName && <Typography>Remote: {task.delivery.remoteRepository} / {task.delivery.remoteBranchName}</Typography>}
        {task.delivery?.pullRequestNumber && <Typography>Pull request: {task.delivery.pullRequestRepository} #{task.delivery.pullRequestNumber}</Typography>}
        {task.delivery?.failureMessage && <Alert severity="error">{task.delivery.failureCode}: {task.delivery.failureMessage}</Alert>}
        {task.delivery && ['Blocked', 'Failed'].includes(task.delivery.status) && <Button sx={{ mt: 1 }} variant="outlined" disabled={retry.isPending} onClick={() => window.confirm('Resume this approved delivery from its saved checkpoint? This does not rerun Planner, Coder, or Reviewer.') && retry.mutate(task.delivery!.id)}>Retry delivery</Button>}
      </Box>)}
  </Stack>;
}
