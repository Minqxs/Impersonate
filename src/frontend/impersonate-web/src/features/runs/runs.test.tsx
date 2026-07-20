import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RunDetailPage } from './pages/RunPages';

const run = { id: 'run-1', projectId: 'project-1', featureRequest: 'Add project notes', status: 'Created', createdAtUtc: '2026-07-20T00:00:00Z', loop: { status: 'Pending', currentStage: 'Planning', maximumRevisionAttempts: 3, continueOnTaskFailure: true, retryCount: 0 }, tasks: [], planningAttempts: [] };
function response(body: unknown) { return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(body) } as Response); }
function renderDetail() { const client = new QueryClient({ defaultOptions: { queries: { retry: false } } }); return render(<QueryClientProvider client={client}><MemoryRouter initialEntries={['/projects/project-1/runs/run-1']}><Routes><Route path="/projects/:projectId/runs/:pipelineRunId" element={<RunDetailPage />} /></Routes></MemoryRouter></QueryClientProvider>); }

beforeEach(() => vi.stubGlobal('fetch', vi.fn()));
describe('planner completion UI', () => {
  it('shows incomplete readiness and disables planning', async () => {
    vi.mocked(fetch).mockImplementation(input => String(input).endsWith('/api/planner/readiness') ? response({ status: 'Incomplete', providerConfigured: true, modelConfigured: false, credentialsConfigured: true, message: 'Planner model is not configured.' }) : String(input).endsWith('/timeline') ? response([]) : response(run));
    renderDetail();
    expect(await screen.findByText('Planner model is not configured.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Start Planning' })).toBeDisabled();
  });

  it('renders ordered tasks, attempt failures, and terminal state without polling controls', async () => {
    const completed = { ...run, status: 'ReadyForExecution', tasks: [{ id: 'two', sequence: 2, title: 'Expose API', description: 'Add operations.', acceptanceCriteria: ['Endpoints are scoped.'], status: 'Pending', attemptCount: 0, revisionCount: 0 }, { id: 'one', sequence: 1, title: 'Add domain', description: 'Add persistence.', acceptanceCriteria: ['Notes persist.'], status: 'Pending', attemptCount: 0, revisionCount: 0 }], planningAttempts: [{ attemptNumber: 1, provider: 'Anthropic', model: 'configured-model', promptVersion: 'planner-v1', status: 'InvalidOutput', startedAtUtc: '2026-07-20T00:00:00Z', completedAtUtc: '2026-07-20T00:00:01Z', failureCode: 'invalid_output', failureMessage: 'Sequences must be contiguous from 1.' }, { attemptNumber: 2, provider: 'Anthropic', model: 'configured-model', promptVersion: 'planner-v1', status: 'Succeeded', startedAtUtc: '2026-07-20T00:00:02Z', completedAtUtc: '2026-07-20T00:00:03Z' }] };
    vi.mocked(fetch).mockImplementation(input => String(input).endsWith('/timeline') ? response([]) : String(input).endsWith('/api/planner/readiness') ? response({ status: 'Ready' }) : response(completed));
    renderDetail();
    expect(await screen.findByText('The task plan has been generated. Coding-agent execution will be introduced in the next milestone.')).toBeInTheDocument();
    const headings = screen.getAllByRole('heading', { level: 6 }).map(element => element.textContent);
    expect(headings.indexOf('1. Add domain')).toBeLessThan(headings.indexOf('2. Expose API'));
    expect(screen.getByText(/invalid_output: Sequences/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start Planning' })).not.toBeInTheDocument();
  });
});
