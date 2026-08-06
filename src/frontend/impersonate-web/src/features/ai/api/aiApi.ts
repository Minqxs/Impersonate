import { apiRequest } from '../../../services/apiRequest';

export type ProviderType = 'Anthropic' | 'OpenAI' | 'GoogleGemini' | 'OpenRouter';

export interface ProviderConnection {
  id: string;
  providerType: ProviderType;
  displayName: string;
  status: string;
  lastValidatedAtUtc?: string;
  lastModelSyncAtUtc?: string;
  availableModelCount: number;
  lastFailureCode?: string;
  lastSafeFailureMessage?: string;
}

export interface ModelUsageSummary {
  provider: string;
  model: string;
  attemptCount: number;
  successfulPlanCount: number;
  invalidOutputCount: number;
  providerFailureCount: number;
  timedOutCount: number;
  inputTokenCount: number;
  outputTokenCount: number;
  averageDurationMilliseconds: number;
  validPlanRate: number;
}

type ProvidersResponse = {
  supportedProviders: ProviderType[];
  connections: ProviderConnection[];
};

type ModelUsageResponse = {
  days: number;
  models: ModelUsageSummary[];
};

const aiRequest = <T>(path: string, init?: RequestInit) =>
  apiRequest<T>(path, init, 'AI provider request failed');

export const listProviders = () =>
  aiRequest<ProvidersResponse>('/api/ai/providers');

export const getModelUsage = (days = 30) =>
  aiRequest<ModelUsageResponse>(`/api/ai/usage/models?days=${days}`);

export const connectProvider = (type: ProviderType, apiKey: string) =>
  aiRequest<ProviderConnection>(`/api/ai/providers/${type}/connections`, {
    method: 'POST',
    body: JSON.stringify({
      displayName: type === 'GoogleGemini' ? 'Google Gemini' : type,
      apiKey
    })
  });

export const replaceProviderCredentials = (id: string, apiKey: string) =>
  aiRequest<ProviderConnection>(`/api/ai/provider-connections/${id}/credentials`, {
    method: 'PUT',
    body: JSON.stringify({
      apiKey,
      organisation: null,
      project: null
    })
  });

export const validateProvider = (id: string) =>
  aiRequest<ProviderConnection>(`/api/ai/provider-connections/${id}/validate`, {
    method: 'POST'
  });

export const syncProvider = (id: string) =>
  aiRequest<ProviderConnection>(`/api/ai/provider-connections/${id}/sync-models`, {
    method: 'POST'
  });

export const disableProvider = (id: string) =>
  aiRequest<void>(`/api/ai/provider-connections/${id}/disable`, {
    method: 'PUT'
  });

export const removeProvider = (id: string) =>
  aiRequest<void>(`/api/ai/provider-connections/${id}`, {
    method: 'DELETE'
  });
