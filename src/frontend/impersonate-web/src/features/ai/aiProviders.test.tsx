import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AiProvidersPage } from './pages/AiProvidersPage';

const connection = {
  id: '48c34376-0455-4813-992c-03a99af3bbbe',
  providerType: 'OpenAI',
  displayName: 'OpenAI',
  status: 'InvalidCredentials',
  availableModelCount: 2,
  lastFailureCode: 'invalid_key',
  lastSafeFailureMessage: 'The credential was rejected.',
};

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}><AiProvidersPage /></QueryClientProvider>);
}

describe('AI provider credential actions', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input).includes('/api/ai/usage/models')) return new Response(JSON.stringify({ days: 30, models: [] }), { status: 200, headers: { 'Content-Type': 'application/json' } });
      if (init?.method === 'PUT') return new Response(JSON.stringify({ ...connection, status: 'PendingValidation', lastFailureCode: null, lastSafeFailureMessage: null }), { status: 200, headers: { 'Content-Type': 'application/json' } });
      return new Response(JSON.stringify({ supportedProviders: ['OpenAI'], connections: [connection] }), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }));
  });

  it('replaces credentials on the existing connection without creating another connection', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Replace credentials' }));
    await user.type(screen.getByLabelText('API key'), 'replacement-secret');
    await user.click(screen.getByRole('button', { name: 'Save securely' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const calls = vi.mocked(fetch).mock.calls;
    const replacement = calls.find(([, init]) => init?.method === 'PUT');
    expect(replacement?.[0]).toContain(`/api/ai/provider-connections/${connection.id}/credentials`);
    expect(replacement?.[1]?.body).toBe(JSON.stringify({ apiKey: 'replacement-secret', organisation: null, project: null }));
    expect(calls.some(([, init]) => init?.method === 'POST')).toBe(false);
  });

  it('shows a repair action and safe message for an unreadable credential', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify({ supportedProviders: ['OpenAI'], connections: [{ ...connection, status: 'Unavailable', lastFailureCode: 'credentials_unreadable', lastSafeFailureMessage: undefined }] }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    renderPage();

    expect(await screen.findByText(/saved credential cannot be decrypted/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Replace credentials' })).toBeEnabled();
  });
});
