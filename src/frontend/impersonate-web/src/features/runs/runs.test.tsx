import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import userEvent from '@testing-library/user-event';
import { RunDetailPage } from './pages/RunPages';

const run = { id: 'run-1', projectId: 'project-1', featureRequest: 'Add project notes', status: 'Created', createdAtUtc: '2026-07-20T00:00:00Z', loop: { status: 'Pending', currentStage: 'Planning', maximumRevisionAttempts: 3, continueOnTaskFailure: true, retryCount: 0 }, tasks: [], planningAttempts: [] };
function response(body: unknown) { return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(body) } as Response); }
function renderDetail() { const client = new QueryClient({ defaultOptions: { queries: { retry: false } } }); return render(<QueryClientProvider client={client}><MemoryRouter initialEntries={['/projects/project-1/runs/run-1']}><Routes><Route path="/projects/:projectId/runs/:pipelineRunId" element={<RunDetailPage />} /></Routes></MemoryRouter></QueryClientProvider>); }

beforeEach(() => vi.stubGlobal('fetch', vi.fn()));
describe('planner completion UI', () => {
  it('uses project routing, displays automatic selection, and enables planning', async () => {
    vi.mocked(fetch).mockImplementation(input => String(input).endsWith('/ai/readiness') ? response({ connectedProviderCount: 1, validProviderCount: 1, discoveredEligiblePlannerModels: 2, routingStatus: 'Ready', blockers: [] }) : String(input).endsWith('/ai/model-selection/preview') ? response({ succeeded: true, profile: { role: 'Planner', complexity: 'Moderate' }, selection: { providerType: 'OpenAI', providerModelId: 'gpt-test', source: 'AutomaticRouting', score: 140, explanation: 'Matched reasoning and structured output.' }, eligibleAlternatives: [{}] }) : String(input).endsWith('/timeline') ? response([]) : response(run));
    renderDetail();
    expect(await screen.findByText('gpt-test')).toBeInTheDocument();
    expect(screen.getByText('OpenAI')).toBeInTheDocument();
    expect(screen.getByText('Matched reasoning and structured output.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Start Planning' })).toBeEnabled();
    expect(vi.mocked(fetch).mock.calls.some(([input])=>String(input).includes('/api/planner/readiness'))).toBe(false);
  });

  it('shows routing blockers and disables planning when no model is eligible', async () => {
    vi.mocked(fetch).mockImplementation(input => String(input).endsWith('/ai/readiness') ? response({ connectedProviderCount: 1, validProviderCount: 1, discoveredEligiblePlannerModels: 0, routingStatus: 'Incomplete', blockers: ['No eligible Planner model satisfies this project routing policy.'] }) : String(input).endsWith('/timeline') ? response([]) : response(run));
    renderDetail();
    expect(await screen.findByText('No eligible Planner model satisfies this project routing policy.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Start Planning' })).toBeDisabled();
  });

  it('renders ordered tasks, attempt failures, and terminal state without polling controls', async () => {
    const completed = { ...run, status: 'ReadyForExecution', tasks: [{ id: 'two', sequence: 2, title: 'Expose API', description: 'Add operations.', acceptanceCriteria: ['Endpoints are scoped.'], status: 'Pending', revisionCount: 0, maximumRevisionAttempts: 3, attempts: [], reviews: [] }, { id: 'one', sequence: 1, title: 'Add domain', description: 'Add persistence.', acceptanceCriteria: ['Notes persist.'], status: 'Pending', revisionCount: 0, maximumRevisionAttempts: 3, attempts: [], reviews: [] }], planningAttempts: [{ attemptNumber: 1, provider: 'Anthropic', model: 'configured-model', promptVersion: 'planner-v1', status: 'InvalidOutput', startedAtUtc: '2026-07-20T00:00:00Z', completedAtUtc: '2026-07-20T00:00:01Z', failureCode: 'invalid_output', failureMessage: 'Sequences must be contiguous from 1.' }, { attemptNumber: 2, provider: 'Anthropic', model: 'configured-model', promptVersion: 'planner-v1', status: 'Succeeded', startedAtUtc: '2026-07-20T00:00:02Z', completedAtUtc: '2026-07-20T00:00:03Z' }] };
    vi.mocked(fetch).mockImplementation(input => String(input).endsWith('/execution/readiness') ? response({ ready: true, coder: { ready: true, provider: 'OpenAI', model: 'coder-model', selectionSource: 'AutomaticRouting' }, reviewer: { ready: true, provider: 'OpenAI', model: 'reviewer-model', selectionSource: 'AutomaticRouting' }, blockers: [], tasks: completed.tasks.map(task=>({taskId:task.id,sequence:task.sequence,coder:{ready:true,provider:'OpenAI',model:'coder-model',selectionSource:'AutomaticRouting'},reviewer:{ready:true,provider:'OpenAI',model:'reviewer-model',selectionSource:'AutomaticRouting'}})) }) : String(input).endsWith('/api/ai/providers') ? response({ connections: [] }) : String(input).endsWith('/timeline') ? response([]) : response(completed));
    renderDetail();
    expect(await screen.findByText('Coder and Reviewer routing is ready.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Start Execution' })).toBeEnabled();
    const headings = screen.getAllByRole('heading', { level: 6 }).map(element => element.textContent);
    expect(headings.indexOf('1. Add domain')).toBeLessThan(headings.indexOf('2. Expose API'));
    expect(screen.getByText(/invalid_output: Sequences/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start Planning' })).not.toBeInTheDocument();
  });

  it('shows the honest ReadyForDelivery handoff without claiming commit or pull request creation', async () => {
    const delivered = { ...run, status: 'ReadyForDelivery', loop: { ...run.loop, currentStage: 'Committing' }, tasks: [], planningAttempts: [] };
    vi.mocked(fetch).mockImplementation(input => String(input).endsWith('/timeline') ? response([]) : response(delivered));
    renderDetail();
    expect(await screen.findByText(/Git commit and pull-request delivery will be introduced in Milestone 6/)).toBeInTheDocument();
    expect(screen.queryByText(/code was committed/i)).not.toBeInTheDocument();
  });

  it('shows discarded repository evidence as a planning warning', async () => {
    const warning='Some repository evidence proposed by the Planner was discarded because it was not present in the bounded snapshot.';const completed={...run,status:'ReadyForExecution',planningWarnings:[warning],tasks:[],planningAttempts:[]};vi.mocked(fetch).mockImplementation(input=>String(input).endsWith('/execution/readiness')?response({ready:false,blockers:['No pending task is available.']}):String(input).endsWith('/api/ai/providers')?response({connections:[]}):String(input).endsWith('/timeline')?response([]):response(completed));renderDetail();expect(await screen.findByText(warning)).toBeInTheDocument();
  });

  it('shows one infrastructure blocker and retries the same run', async () => {
    const user=userEvent.setup();const blocked={...run,status:'WaitingForInfrastructure',infrastructureFailureCode:'repository_dns_failed',infrastructureFailureMessage:'Repository DNS resolution failed while preparing the isolated workspace.',infrastructureBlockedTaskId:'one',tasks:[{id:'one',sequence:1,title:'Add domain',description:'Add persistence.',acceptanceCriteria:['Notes persist.'],status:'Pending',revisionCount:0,maximumRevisionAttempts:3,attempts:[],reviews:[]} ]};vi.mocked(fetch).mockImplementation((input,init)=>String(input).endsWith('/execution/retry')&&init?.method==='POST'?response({...blocked,status:'Executing'}):String(input).endsWith('/timeline')?response([]):response(blocked));renderDetail();expect(await screen.findByText(/Execution is blocked because the isolated repository workspace could not be prepared/)).toBeInTheDocument();expect(screen.getByText(/repository_dns_failed/)).toBeInTheDocument();expect(screen.getByText(/Affected task: Add domain/)).toBeInTheDocument();await user.click(screen.getByRole('button',{name:'Retry execution'}));expect(vi.mocked(fetch).mock.calls.some(([input])=>String(input).endsWith('/execution/retry'))).toBe(true);expect(screen.getByRole('link',{name:'Configuration health'})).toHaveAttribute('href','/projects/project-1/health');
  });
  it('runs a pending task individually from the task card',async()=>{const user=userEvent.setup();const ready={...run,status:'ReadyForExecution',tasks:[{id:'one',sequence:1,title:'Add domain',description:'Add persistence.',acceptanceCriteria:['Notes persist.'],status:'Pending',revisionCount:0,maximumRevisionAttempts:3,dependsOnTaskIds:[],attempts:[],reviews:[]}],planningAttempts:[]};vi.mocked(fetch).mockImplementation((input,init)=>String(input).endsWith('/tasks/one/execution/start')&&init?.method==='POST'?response(true):String(input).endsWith('/execution/readiness')?response({ready:true,coder:{ready:true},reviewer:{ready:true},blockers:[],tasks:[]}):String(input).endsWith('/api/ai/providers')?response({connections:[]}):String(input).endsWith('/timeline')?response([]):response(ready));renderDetail();await user.click(await screen.findByRole('button',{name:'Run task'}));expect(vi.mocked(fetch).mock.calls.some(([input])=>String(input).endsWith('/tasks/one/execution/start'))).toBe(true);});
  it('keeps retry controls usable when no pending task remains',async()=>{const ready={...run,status:'ReadyForExecution',tasks:[{id:'one',sequence:1,title:'Add domain',description:'Add persistence.',acceptanceCriteria:['Notes persist.'],status:'Skipped',revisionCount:0,maximumRevisionAttempts:3,dependsOnTaskIds:[],attempts:[],reviews:[]}],planningAttempts:[]};vi.mocked(fetch).mockImplementation(input=>String(input).endsWith('/execution/readiness')?response({ready:false,coder:{ready:false},reviewer:{ready:false},blockers:['No pending task is available for full execution. Retry a skipped or failed task individually below.'],tasks:[]}):String(input).endsWith('/api/ai/providers')?response({connections:[]}):String(input).endsWith('/timeline')?response([]):response(ready));renderDetail();expect(await screen.findByText(/Retry a skipped or failed task individually below/)).toBeInTheDocument();expect(screen.getByRole('button',{name:'Retry task'})).toBeEnabled();});
  it('deletes a non-active run after explicit confirmation',async()=>{const user=userEvent.setup();const confirm=vi.spyOn(window,'confirm').mockReturnValue(true);vi.mocked(fetch).mockImplementation((input,init)=>String(input).endsWith('/pipeline-runs/run-1')&&init?.method==='DELETE'?Promise.resolve({ok:true,status:204} as Response):String(input).endsWith('/timeline')?response([]):String(input).endsWith('/ai/readiness')?response({routingStatus:'Incomplete',blockers:[]}):response(run));renderDetail();await user.click(await screen.findByRole('button',{name:'Delete run'}));expect(confirm).toHaveBeenCalledOnce();expect(vi.mocked(fetch).mock.calls.some(([input,init])=>String(input).endsWith('/pipeline-runs/run-1')&&init?.method==='DELETE')).toBe(true);});
});
