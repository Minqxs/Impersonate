import { apiRequest } from '../../../services/apiRequest';

export type QualityState =
  | 'NotConfigured'
  | 'Loading'
  | 'Available'
  | 'Passed'
  | 'Failed'
  | 'TemporarilyUnavailable'
  | 'AuthenticationRequired'
  | 'ProjectNotFound';

export type QualityMetric = {
  value?: number;
  rating?: string;
};

export type QualitySummary = {
  state: QualityState;
  qualityGate?: string;
  coverage: QualityMetric;
  newCoverage: QualityMetric;
  bugs: QualityMetric;
  vulnerabilities: QualityMetric;
  codeSmells: QualityMetric;
  reliability: QualityMetric;
  security: QualityMetric;
  maintainability: QualityMetric;
  duplicatedLines: QualityMetric;
  linesOfCode: QualityMetric;
  cognitiveComplexity: QualityMetric;
  lastSuccessfulRefreshAtUtc?: string;
  failureCode?: string;
  safeMessage?: string;
  projectUrl?: string;
};

export type QualityConfiguration = {
  configured: boolean;
  enabled: boolean;
  baseUrl?: string;
  projectKey?: string;
  displayName?: string;
  credentialConfigured: boolean;
  lastSuccessfulRefreshAtUtc?: string;
  lastFailureCode?: string;
  lastSafeFailureMessage?: string;
};

export type QualityConfigurationInput = {
  enabled: boolean;
  baseUrl: string;
  projectKey: string;
  displayName?: string;
  token?: string;
};

const qualityPath = (projectId: string, suffix: string) =>
  `/api/projects/${projectId}/quality/${suffix}`;

export const qualityKeys = {
  configuration: (id: string) => ['project-quality-configuration', id] as const,
  summary: (id: string) => ['project-quality-summary', id] as const
};

export const getQualityConfiguration = (id: string) =>
  apiRequest<QualityConfiguration>(qualityPath(id, 'configuration'));

export const saveQualityConfiguration = (id: string, input: QualityConfigurationInput) =>
  apiRequest<QualityConfiguration>(qualityPath(id, 'configuration'), {
    method: 'PUT',
    body: JSON.stringify(input)
  });

export const removeQualityConfiguration = (id: string) =>
  apiRequest<void>(qualityPath(id, 'configuration'), {
    method: 'DELETE'
  });

export const getQualitySummary = (id: string) =>
  apiRequest<QualitySummary>(qualityPath(id, 'summary'));

export const refreshQualitySummary = (id: string) =>
  apiRequest<QualitySummary>(qualityPath(id, 'refresh'), {
    method: 'POST'
  });

export const validateQualityConnection = (id: string) =>
  apiRequest<QualitySummary>(qualityPath(id, 'validate'), {
    method: 'POST'
  });
