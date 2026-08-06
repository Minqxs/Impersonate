import { apiRequest, apiText } from '../../../services/apiRequest';

export type ExecutionInvocation = {
  id: string;
  sequence: number;
  agentRole: string;
  provider: string;
  model: string;
  promptVersion: string;
  providerRequestId?: string;
  inputTokenCount?: number;
  outputTokenCount?: number;
  responseType?: string;
  toolStepCount: number;
  successfulReadCount: number;
  successfulSearchCount: number;
  successfulPatchCount: number;
  patchAttemptCount: number;
  failedPatchCount: number;
  lastPatchFailureCode?: string;
  providerRoundTripCount: number;
  consecutiveReadOnlyRounds: number;
  maximumSingleRequestInput: number;
  providerResponseStatus?: string;
  providerIncompleteReason?: string;
  structuredOutputRepairCount: number;
  noProgressCorrectionCount: number;
  paidProviderRequestCount: number;
  currentPhase: string;
  requestedProhibitedTool?: string;
  maximumRequestedOutputReservation: number;
  outputReservationReasons?: string[];
  providerCapacityWaitMilliseconds: number;
  providerResetUsed: boolean;
  lastRateLimitScope?: string;
  fallbackSequence: number;
  status: string;
  failureCode?: string;
  failureReason?: string;
  startedAtUtc: string;
  completedAtUtc: string;
};

export type TaskAttempt = {
  id: string;
  attemptNumber: number;
  attemptType: string;
  status: string;
  provider?: string;
  model?: string;
  promptVersion?: string;
  inputTokenCount?: number;
  outputTokenCount?: number;
  toolStepCount: number;
  summary?: string;
  failureCode?: string;
  failureReason?: string;
  changedFiles: string[];
  patchArtifactReference?: string;
  patchSha256?: string;
  validationSummary: string[];
  startedAtUtc: string;
  completedAtUtc?: string;
  invocations?: ExecutionInvocation[];
  sourceBaseCommitSha?: string;
  dependencyPatchCount: number;
  dependencyTaskIds?: string[];
  composedTreeFingerprint?: string;
  currentRevisionPatchApplied: boolean;
  incrementalPatchFileCount: number;
  compositionStatus?: string;
};

export type ReviewDecision = {
  id: string;
  taskAttemptId: string;
  decision: string;
  provider?: string;
  model?: string;
  promptVersion?: string;
  inputTokenCount?: number;
  outputTokenCount?: number;
  reviewedPatchSha256?: string;
  summary: string;
  feedback?: string;
  findingsJson: string;
  isCurrent: boolean;
  createdAtUtc: string;
};

export type TaskDelivery = {
  id: string;
  status: string;
  branchName?: string;
  commitSha?: string;
  remoteName?: string;
  remoteRepository?: string;
  remoteBranchName?: string;
  pushedCommitSha?: string;
  pushedAtUtc?: string;
  pullRequestProvider?: string;
  pullRequestRepository?: string;
  pullRequestNumber?: number;
  pullRequestUrl?: string;
  pullRequestHeadBranch?: string;
  pullRequestBaseBranch?: string;
  pullRequestObservedHeadSha?: string;
  pullRequestCreatedAtUtc?: string;
  failureCode?: string;
  failureMessage?: string;
};

export type RunDelivery = {
  id: string;
  status: string;
  sourceDefaultBranch: string;
  sourceBaseCommitSha: string;
  runBranchName: string;
  runBranchHeadSha?: string;
  aggregateValidationSummaryJson: string;
  finalReviewDecisionId?: string;
  finalReviewedHeadSha?: string;
  finalPullRequestRepository?: string;
  finalPullRequestNumber?: number;
  finalPullRequestUrl?: string;
  finalPullRequestHeadSha?: string;
  finalPullRequestBaseBranch?: string;
  finalPullRequestMergeableState?: string;
  requiredChecksState?: string;
  failureCode?: string;
  failureMessage?: string;
};

export type PlannedTask = {
  id: string;
  sequence: number;
  title: string;
  description: string;
  acceptanceCriteria: string[];
  status: string;
  revisionCount: number;
  maximumRevisionAttempts: number;
  coderModelOverrideId?: string;
  reviewerModelOverrideId?: string;
  attempts: TaskAttempt[];
  reviews: ReviewDecision[];
  skipReason?: string;
  failureReason?: string;
  dependsOnTaskIds: string[];
  affectedAreas: string[];
  changeType: string;
  risk: string;
  conflictRisk: string;
  executionReason?: string;
  repositoryEvidence: string[];
  originalPlannerSequence: number;
  orderAdjusted: boolean;
  orderAdjustmentReason?: string;
  establishesSharedContract: boolean;
  deliveryEligible?: boolean;
  deliveryBlockingDependencyIds?: string[];
  delivery?: TaskDelivery;
};

export type PipelineRun = {
  id: string;
  projectId: string;
  featureRequest: string;
  status: string;
  createdAtUtc: string;
  startedAtUtc?: string;
  completedAtUtc?: string;
  cancelledAtUtc?: string;
  failureReason?: string;
  stopReason?: string;
  infrastructureFailureCode?: string;
  infrastructureFailureMessage?: string;
  infrastructureBlockedTaskId?: string;
  planningWarnings?: string[];
  runDelivery?: RunDelivery;
  loop: {
    status: string;
    currentStage: string;
    maximumRevisionAttempts: number;
    continueOnTaskFailure: boolean;
    retryCount: number;
  };
  tasks: PlannedTask[];
  planningAttempts: Array<{
    attemptNumber: number;
    provider: string;
    model: string;
    promptVersion: string;
    status: string;
    startedAtUtc: string;
    completedAtUtc?: string;
    failureCode?: string;
    failureMessage?: string;
    inputTokenCount?: number;
    outputTokenCount?: number;
  }>;
};

export type PipelineEvent = {
  id: string;
  plannedTaskId?: string;
  eventType: string;
  previousState?: string;
  newState: string;
  message: string;
  createdAtUtc: string;
  sequence: number;
};

export type ProjectAiReadiness = {
  connectedProviderCount: number;
  validProviderCount: number;
  discoveredEligiblePlannerModels: number;
  routingStatus: 'Ready' | 'Incomplete';
  blockers: string[];
};

export type ScoreComponent = {
  name: string;
  score: number;
  reason: string;
};

export type TaskProfile = {
  role: string;
  complexity: string;
  risk: string;
  taskType?: string;
  languages?: string[];
  frameworks?: string[];
  affectedAreas?: string[];
  changeType?: string;
  conflictRisk?: string;
  requiresCoding: boolean;
  requiresReasoning: boolean;
  requiresStructuredOutput: boolean;
  requiresTools: boolean;
  estimatedContextSize: number;
  costSensitivity: string;
  latencySensitivity: string;
  reasons: string[];
};

export type SelectedModel = {
  connectionId?: string;
  discoveredModelId?: string;
  providerType: string;
  providerModelId: string;
  source: string;
  score: number;
  explanation: string;
  scoreBreakdown?: ScoreComponent[];
  metadataVersion?: string;
  rankedLowerReason?: string;
  canonicalFamily?: string;
  generation?: string;
  specialisation?: string;
};

export type ModelSelectionResult = {
  succeeded: boolean;
  profile: TaskProfile;
  selection?: SelectedModel;
  eligibleAlternatives: SelectedModel[];
  failureCode?: string;
  failureMessage?: string;
};

export type RoutingModelIdentity = {
  discoveredModelId?: string;
  provider: string;
  providerModelId: string;
  canonicalFamily: string;
  generation: string;
  specialisation: string;
};

export type ModelPreview = {
  ready: boolean;
  modelId?: string;
  provider?: string;
  model?: string;
  selectionSource?: string;
  explanation?: string;
  blocker?: string;
  totalScore: number;
  scoreBreakdown?: ScoreComponent[];
  metadataVersion?: string;
  profile?: TaskProfile;
  alternatives?: SelectedModel[];
  identity?: RoutingModelIdentity;
};

export type ExecutionReadiness = {
  ready: boolean;
  coder: ModelPreview;
  reviewer: ModelPreview;
  blockers: string[];
  tasks?: Array<{
    taskId: string;
    sequence: number;
    coder: ModelPreview;
    reviewer: ModelPreview;
  }>;
  distinctCoderModels: number;
  distinctReviewerModels: number;
  tasksUsingOverrides: number;
};

export type PipelineIntelligence = {
  pipelineRunId: string;
  repositoryContextSummary?: string;
  languages: string[];
  frameworks: string[];
  dependencyGraph: PlannedTask[];
  routing: ExecutionReadiness;
  activeStage: string;
  activeTaskId?: string;
  preferReviewerDiversity: boolean;
  reviewerDiversityWeight: number;
  historicalOutcomeMessage: string;
};

export type ModelOption = {
  id: string;
  providerConnectionId: string;
  providerType: string;
  providerModelId: string;
  displayName: string;
  isAvailable: boolean;
};

export type FinalRunMergeResult = {
  repository: string;
  pullRequestNumber: number;
  pullRequestHeadSha: string;
  mergeCommitSha: string;
};

const runBasePath = (projectId: string) =>
  `/api/projects/${projectId}/pipeline-runs`;

const runPath = (projectId: string, runId: string, suffix = '') =>
  `${runBasePath(projectId)}/${runId}${suffix}`;

const taskPath = (projectId: string, runId: string, taskId: string, suffix = '') =>
  `${runPath(projectId, runId)}/tasks/${taskId}${suffix}`;

export const runKeys = {
  all: (projectId: string) => ['pipeline-runs', projectId] as const,
  detail: (projectId: string, id: string) => ['pipeline-run', projectId, id] as const,
  timeline: (projectId: string, id: string) => ['pipeline-timeline', projectId, id] as const,
  aiReadiness: (projectId: string) => ['project-ai-readiness', projectId] as const,
  modelPreview: (projectId: string, description: string, override?: string) =>
    ['planner-model-preview', projectId, description, override ?? null] as const,
  executionReadiness: (projectId: string, id: string) =>
    ['execution-readiness', projectId, id] as const
};

export const listRuns = (projectId: string, status = '') =>
  apiRequest<PipelineRun[]>(`${runBasePath(projectId)}${status ? `?status=${status}` : ''}`);

export const getRun = (projectId: string, id: string) =>
  apiRequest<PipelineRun>(runPath(projectId, id));

export const getTimeline = (projectId: string, id: string) =>
  apiRequest<PipelineEvent[]>(runPath(projectId, id, '/timeline'));

export const createRun = (projectId: string, featureRequest: string) =>
  apiRequest<PipelineRun>(runBasePath(projectId), {
    method: 'POST',
    body: JSON.stringify({ featureRequest })
  });

export const cancelRun = (projectId: string, id: string) =>
  apiRequest<PipelineRun>(runPath(projectId, id, '/cancel'), {
    method: 'POST'
  });

export const deleteRun = (projectId: string, id: string) =>
  apiRequest<void>(runPath(projectId, id), {
    method: 'DELETE'
  });

export const startPlanning = (projectId: string, id: string) =>
  apiRequest<PipelineRun>(runPath(projectId, id, '/planning/start'), {
    method: 'POST'
  });

export const getExecutionReadiness = (projectId: string, id: string) =>
  apiRequest<ExecutionReadiness>(runPath(projectId, id, '/execution/readiness'));

export const getPipelineIntelligence = (projectId: string, id: string) =>
  apiRequest<PipelineIntelligence>(runPath(projectId, id, '/intelligence'));

export const startExecution = (projectId: string, id: string) =>
  apiRequest<PipelineRun>(runPath(projectId, id, '/execution/start'), {
    method: 'POST'
  });

export const retryExecution = (projectId: string, id: string) =>
  apiRequest<PipelineRun>(runPath(projectId, id, '/execution/retry'), {
    method: 'POST'
  });

export const retryDelivery = (projectId: string, id: string, deliveryId: string) =>
  apiRequest<TaskDelivery>(runPath(projectId, id, `/deliveries/${deliveryId}/retry`), {
    method: 'POST'
  });

export const mergeRunToMain = (projectId: string, id: string) =>
  apiRequest<FinalRunMergeResult>(runPath(projectId, id, '/delivery/merge-to-main'), {
    method: 'POST'
  });

export const runTask = (projectId: string, id: string, taskId: string) =>
  apiRequest<boolean>(taskPath(projectId, id, taskId, '/execution/start'), {
    method: 'POST'
  });

export const retryTask = (projectId: string, id: string, taskId: string) =>
  apiRequest<boolean>(taskPath(projectId, id, taskId, '/execution/retry'), {
    method: 'POST'
  });

export const setTaskModelOverrides = (
  projectId: string,
  id: string,
  taskId: string,
  coderModelId?: string,
  reviewerModelId?: string
) =>
  apiRequest<PipelineRun>(taskPath(projectId, id, taskId, '/model-overrides'), {
    method: 'PUT',
    body: JSON.stringify({
      coderModelId: coderModelId ?? null,
      reviewerModelId: reviewerModelId ?? null
    })
  });

export const getAttemptDiff = (
  projectId: string,
  id: string,
  taskId: string,
  attemptId: string
) =>
  apiText(
    taskPath(projectId, id, taskId, `/attempts/${attemptId}/diff`),
    'Diff request failed'
  );

export const listAvailableModels = async () => {
  const providers = await apiRequest<{ connections: Array<{ id: string; status: string }> }>('/api/ai/providers');
  const connectedProviders = providers.connections.filter(provider => provider.status === 'Connected');

  const modelGroups = await Promise.all(
    connectedProviders.map(provider =>
      apiRequest<ModelOption[]>(`/api/ai/provider-connections/${provider.id}/models`)
    )
  );

  return modelGroups.flat().filter(model => model.isAvailable);
};

export const getProjectAiReadiness = (projectId: string) =>
  apiRequest<ProjectAiReadiness>(`/api/projects/${projectId}/ai/readiness`);

export const previewPlannerModel = (
  projectId: string,
  description: string,
  manualModelOverrideId?: string
) =>
  apiRequest<ModelSelectionResult>(`/api/projects/${projectId}/ai/model-selection/preview`, {
    method: 'POST',
    body: JSON.stringify({
      role: 'Planner',
      description,
      manualModelOverrideId: manualModelOverrideId ?? null
    })
  });
