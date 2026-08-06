import { apiRequest } from '../../../services/apiRequest';

export type ProjectStatus = 'Active' | 'Idle' | 'Off';

export interface Project {
  id: string;
  name: string;
  description?: string;
  repositoryUrl: string;
  defaultBranch: string;
  status: ProjectStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface Health {
  projectId: string;
  overallStatus: string;
  checks: Array<{ name: string; status: string; message: string }>;
  checkedAtUtc: string;
}

export interface ProjectInput {
  name: string;
  description?: string;
  repositoryUrl: string;
  defaultBranch: string;
  status?: ProjectStatus;
}

export function listProjects(status?: ProjectStatus, search?: string, signal?: AbortSignal) {
  const params = new URLSearchParams();
  if (status) params.set('status', status);
  if (search) params.set('search', search);

  return apiRequest<Project[]>(`/api/projects${params.size ? `?${params}` : ''}`, { signal });
}

export function getProject(id: string, signal?: AbortSignal) {
  return apiRequest<Project>(`/api/projects/${id}`, { signal });
}

export function createProject(input: ProjectInput) {
  return apiRequest<Project>('/api/projects', {
    method: 'POST',
    body: JSON.stringify(input)
  });
}

export function updateProject(id: string, input: ProjectInput) {
  return apiRequest<Project>(`/api/projects/${id}`, {
    method: 'PUT',
    body: JSON.stringify(input)
  });
}

export function changeProjectStatus(id: string, status: ProjectStatus) {
  return apiRequest<Project>(`/api/projects/${id}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ status })
  });
}

export function getProjectHealth(id: string, signal?: AbortSignal) {
  return apiRequest<Health>(`/api/projects/${id}/health`, { signal });
}
