import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, Card, CardActions, CardContent, Dialog, DialogActions, DialogContent, DialogTitle, Grid, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material';
import { connectProvider, disableProvider, getModelUsage, listProviders, removeProvider, replaceProviderCredentials, syncProvider, validateProvider, type ModelUsageSummary, type ProviderConnection, type ProviderType } from '../api/aiApi';

const providers: ProviderType[] = ['Anthropic', 'OpenAI', 'GoogleGemini', 'OpenRouter'];
const label = (type: ProviderType) => type === 'GoogleGemini' ? 'Google Gemini' : type;
type CredentialDialogState = { mode: 'connect'; providerType: ProviderType } | { mode: 'replace'; connection: ProviderConnection };

export function AiProvidersPage() {
  const client = useQueryClient();
  const query = useQuery({ queryKey: ['ai-providers'], queryFn: listProviders });
  const usage = useQuery({ queryKey: ['ai-model-usage', 30], queryFn: () => getModelUsage(30) });
  const [dialog, setDialog] = useState<CredentialDialogState>();
  const [apiKey, setApiKey] = useState('');
  const refresh = () => Promise.all([
    client.invalidateQueries({ queryKey: ['ai-providers'] }),
    client.invalidateQueries({ queryKey: ['project-ai-readiness'] }),
    client.invalidateQueries({ queryKey: ['planner-model-preview'] }),
  ]);
  const save = useMutation({
    mutationFn: () => dialog?.mode === 'replace' ? replaceProviderCredentials(dialog.connection.id, apiKey) : connectProvider(dialog!.providerType, apiKey),
    onSuccess: async () => { setApiKey(''); setDialog(undefined); await refresh(); },
  });
  const close = () => { if (!save.isPending) { setApiKey(''); setDialog(undefined); save.reset(); } };
  if (query.isLoading) return <Typography>Loading AI providers…</Typography>;

  return <Box>
    <Typography variant="h4" gutterBottom>AI Providers</Typography>
    <Typography color="text.secondary" sx={{ mb: 3 }}>Connect provider access once. Replace credentials later without changing the connection or model history.</Typography>
    {query.error && <Alert severity="error">Providers could not be loaded.</Alert>}
    <Grid container spacing={2}>{providers.map(type => <Grid key={type} size={{ xs: 12, md: 6 }}><ProviderCard type={type} connection={query.data?.connections.find(x => x.providerType === type)} refresh={refresh} connect={() => setDialog({ mode: 'connect', providerType: type })} replace={connection => setDialog({ mode: 'replace', connection })} /></Grid>)}</Grid>
    <ModelUsage usage={usage.data?.models} failed={!!usage.error} />
    <Dialog open={!!dialog} onClose={close} fullWidth maxWidth="sm">
      <DialogTitle>{dialog?.mode === 'replace' ? 'Replace credentials' : `Connect ${dialog ? label(dialog.providerType) : ''}`}</DialogTitle>
      <DialogContent>
        <Alert severity="info" sx={{ mb: 2 }}>{dialog?.mode === 'replace' ? 'The connection will require validation after replacement.' : 'The credential is encrypted after submission and is never displayed again.'}</Alert>
        {save.isError && <Alert severity="error" sx={{ mb: 2 }}>{save.error.message}</Alert>}
        <TextField autoFocus fullWidth type="password" label="API key" value={apiKey} onChange={event => setApiKey(event.target.value)} autoComplete="new-password" />
      </DialogContent>
      <DialogActions><Button onClick={close}>Cancel</Button><Button variant="contained" disabled={!apiKey.trim() || save.isPending} onClick={() => save.mutate()}>{save.isPending ? 'Saving…' : 'Save securely'}</Button></DialogActions>
    </Dialog>
  </Box>;
}

function ModelUsage({ usage, failed }: { usage?: ModelUsageSummary[]; failed: boolean }) {
  return <Box sx={{ mt: 4 }}>
    <Typography variant="h5" gutterBottom>Planner model usage · last 30 days</Typography>
    <Typography color="text.secondary" sx={{ mb: 2 }}>Valid-plan rate measures schema-valid task generation, not downstream implementation quality. Compare it with tokens and latency before choosing a model override.</Typography>
    {failed && <Alert severity="warning">Model usage could not be loaded.</Alert>}
    {usage && <TableContainer component={Paper} variant="outlined"><Table size="small">
      <TableHead><TableRow><TableCell>Model</TableCell><TableCell align="right">Attempts</TableCell><TableCell align="right">Valid plans</TableCell><TableCell align="right">Input tokens</TableCell><TableCell align="right">Output tokens</TableCell><TableCell align="right">Avg latency</TableCell><TableCell align="right">Invalid / failed</TableCell></TableRow></TableHead>
      <TableBody>{usage.map(item => <TableRow key={`${item.provider}/${item.model}`}><TableCell>{item.provider} / {item.model}</TableCell><TableCell align="right">{item.attemptCount}</TableCell><TableCell align="right">{item.validPlanRate.toFixed(0)}%</TableCell><TableCell align="right">{item.inputTokenCount.toLocaleString()}</TableCell><TableCell align="right">{item.outputTokenCount.toLocaleString()}</TableCell><TableCell align="right">{(item.averageDurationMilliseconds / 1000).toFixed(1)}s</TableCell><TableCell align="right">{item.invalidOutputCount} / {item.providerFailureCount + item.timedOutCount}</TableCell></TableRow>)}</TableBody>
    </Table></TableContainer>}
  </Box>;
}

function ProviderCard({ type, connection, refresh, connect, replace }: { type: ProviderType; connection?: ProviderConnection; refresh: () => Promise<unknown>; connect: () => void; replace: (connection: ProviderConnection) => void }) {
  const action = (operation: (id: string) => Promise<unknown>) => async () => { await operation(connection!.id); await refresh(); };
  const message = connection?.lastFailureCode === 'credentials_missing' ? 'No saved credential is available. Replace credentials to repair this connection.' : connection?.lastFailureCode === 'credentials_unreadable' ? 'The saved credential cannot be decrypted. Replace credentials after checking the shared key-ring configuration.' : connection?.lastSafeFailureMessage;
  return <Card variant="outlined"><CardContent><Typography variant="h6">{label(type)}</Typography><Typography color={connection?.status === 'Connected' ? 'success.main' : 'text.secondary'}>{connection?.status === 'PendingValidation' ? 'Pending validation' : connection?.status ?? 'Not connected'}</Typography>{connection && <Box sx={{ mt: 2 }}><Typography variant="body2">Last validated: {connection.lastValidatedAtUtc ? new Date(connection.lastValidatedAtUtc).toLocaleString() : 'Never'}</Typography><Typography variant="body2">Available models: {connection.availableModelCount}</Typography><Typography variant="body2">Last synchronised: {connection.lastModelSyncAtUtc ? new Date(connection.lastModelSyncAtUtc).toLocaleString() : 'Never'}</Typography>{message && <Alert severity="warning" sx={{ mt: 1 }}>{message}</Alert>}</Box>}</CardContent><CardActions>{!connection ? <Button onClick={connect}>Connect</Button> : <><Button onClick={action(validateProvider)}>Validate</Button><Button disabled={connection.status !== 'Connected'} onClick={action(syncProvider)}>Synchronise models</Button><Button onClick={() => replace(connection)}>Replace credentials</Button><Button onClick={action(disableProvider)}>Disable</Button><Button color="error" onClick={action(removeProvider)}>Remove</Button></>}</CardActions></Card>;
}
