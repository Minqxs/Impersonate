import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactNode } from 'react';
import { ActiveProjectProvider, useActiveProject } from './ActiveProjectContext';
import { activeProjectStorageKey } from './activeProjectStorage';
import { ProjectSelector } from './components/ProjectSelector';
import { CreateProjectPage, ProjectsPage } from './pages/ProjectsPages';
import { ProjectWorkspaceLayout } from './layouts/ProjectWorkspaceLayout';
import { ProjectOverviewPage } from './pages/ProjectOverviewPage';

const project = { id: '11111111-1111-1111-1111-111111111111', name: 'Alpha', repositoryUrl: 'https://github.com/example/alpha', defaultBranch: 'main', status: 'Idle', createdAtUtc: '2026-07-20T00:00:00Z', updatedAtUtc: '2026-07-20T00:00:00Z' };

function response(body: unknown, ok = true) { return Promise.resolve({ ok, status: ok ? 200 : 404, json: () => Promise.resolve(body) } as Response); }
function Wrapper({ children }: { children: ReactNode }) { const client = new QueryClient({ defaultOptions: { queries: { retry: false } } }); return <QueryClientProvider client={client}><MemoryRouter><ActiveProjectProvider>{children}</ActiveProjectProvider></MemoryRouter></QueryClientProvider>; }

beforeEach(() => vi.stubGlobal('fetch', vi.fn()));

describe('project workspace frontend', () => {
  it('clears an invalid stored project ID', async () => {
    localStorage.setItem(activeProjectStorageKey, 'missing');
    vi.mocked(fetch).mockImplementation(() => response({ title: 'Not found' }, false));
    function Probe() { const context = useActiveProject(); return <span>{context.activeProjectId ?? 'none'}</span>; }
    render(<Wrapper><Probe /></Wrapper>);
    await waitFor(() => expect(screen.getByText('none')).toBeInTheDocument());
    expect(localStorage.getItem(activeProjectStorageKey)).toBeNull();
  });

  it('shows an empty selector state', async () => {
    vi.mocked(fetch).mockImplementation(() => response([]));
    render(<Wrapper><ProjectSelector /></Wrapper>);
    await waitFor(() => expect(screen.getByText('No project selected')).toBeInTheDocument());
  });

  it('switches projects and updates navigation', async () => {
    vi.mocked(fetch).mockImplementation((input) => String(input).endsWith('/api/projects') ? response([project]) : response(project));
    function Location() { return <span data-testid="location">{useLocation().pathname}</span>; }
    render(<Wrapper><ProjectSelector /><Location /></Wrapper>);
    await screen.findByText('No project selected');
    fireEvent.mouseDown(screen.getByRole('combobox'));
    fireEvent.click(await screen.findByText('Alpha · Idle'));
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(`/projects/${project.id}/dashboard`));
    expect(localStorage.getItem(activeProjectStorageKey)).toBe(project.id);
  });

  it('uses native required validation before submitting the project form', () => {
    vi.mocked(fetch).mockImplementation(() => response([]));
    render(<Wrapper><CreateProjectPage /></Wrapper>);
    fireEvent.click(screen.getByRole('button', { name: 'Create project' }));
    expect(screen.getByRole('textbox', { name: /^Name/ })).toBeInvalid();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('renders project list loading and error states', async () => {
    vi.mocked(fetch).mockImplementation(() => new Promise<Response>(() => undefined));
    const loading = render(<Wrapper><ProjectsPage /></Wrapper>);
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
    loading.unmount();
    vi.mocked(fetch).mockImplementation(() => Promise.reject(new Error('Network unavailable')));
    render(<Wrapper><ProjectsPage /></Wrapper>);
    expect(await screen.findByText('Network unavailable')).toBeInTheDocument();
  });

  it('uses current-page semantics in responsive project navigation', async () => {
    vi.mocked(fetch).mockImplementation(() => response(project));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[`/projects/${project.id}/quality`]}><ActiveProjectProvider><Routes><Route path="/projects/:projectId" element={<ProjectWorkspaceLayout />}><Route path="quality" element={<Outlet />} /></Route></Routes></ActiveProjectProvider></MemoryRouter></QueryClientProvider>);
    expect(await screen.findByRole('tab', { name: 'Code Quality' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('tablist', { name: 'Project navigation' })).toBeInTheDocument();
  });

  it('summarises project operations and exposes quick actions', async () => {
    vi.mocked(fetch).mockImplementation(input => String(input).endsWith('/health') ? response({ projectId: project.id, overallStatus: 'Healthy', checks: [{ name: 'Repository', status: 'Ready', message: 'Configured' }], checkedAtUtc: '2026-07-20' }) : String(input).includes('/pipeline-runs') ? response([{ id: 'run-1', projectId: project.id, featureRequest: 'Feature', status: 'ReadyForDelivery', createdAtUtc: '2026-07-20', loop: { currentStage: 'Committing' }, tasks: [{ id: 'task', status: 'Approved', deliveryEligible: true }] }]) : response(project));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[`/projects/${project.id}/dashboard`]}><Routes><Route path="/projects/:projectId/dashboard" element={<ProjectOverviewPage />} /></Routes></MemoryRouter></QueryClientProvider>);
    expect(await screen.findByText('Operational status')).toBeInTheDocument();
    expect(screen.getByText('Approved tasks')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Create run' })).toHaveAttribute('href', `/projects/${project.id}/runs/new`);
    expect(screen.getByRole('link', { name: 'View delivery' })).toBeInTheDocument();
  });
});
