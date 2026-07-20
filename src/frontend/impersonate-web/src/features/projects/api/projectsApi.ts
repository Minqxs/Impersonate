export type ProjectStatus = 'Active' | 'Idle' | 'Off';
export interface Project { id: string; name: string; description?: string; repositoryUrl: string; defaultBranch: string; status: ProjectStatus; createdAtUtc: string; updatedAtUtc: string; }
export interface Health { projectId: string; overallStatus: string; checks: { name: string; status: string; message: string }[]; checkedAtUtc: string; }
export interface ProjectInput { name: string; description?: string; repositoryUrl: string; defaultBranch: string; status?: ProjectStatus; }

const baseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? 'https://localhost:7001';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, { headers: { 'Content-Type': 'application/json', ...init?.headers }, ...init });
  if (!response.ok) {
    const body = await response.json().catch(() => null) as { title?: string; errors?: Record<string, string[]> } | null;
    throw new Error(body?.errors ? Object.values(body.errors).flat().join(' ') : body?.title ?? `Request failed (${response.status}).`);
  }
  return response.json() as Promise<T>;
}

export function listProjects(status?: ProjectStatus, search?: string, signal?: AbortSignal) { const params = new URLSearchParams(); if (status) params.set('status', status); if (search) params.set('search', search); return request<Project[]>(`/api/projects${params.size ? `?${params}` : ''}`, { signal }); }
export function getProject(id: string, signal?: AbortSignal) { return request<Project>(`/api/projects/${id}`, { signal }); }
export function createProject(input: ProjectInput) { return request<Project>('/api/projects', { method: 'POST', body: JSON.stringify(input) }); }
export function updateProject(id: string, input: ProjectInput) { return request<Project>(`/api/projects/${id}`, { method: 'PUT', body: JSON.stringify(input) }); }
export function changeProjectStatus(id: string, status: ProjectStatus) { return request<Project>(`/api/projects/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }); }
export function getProjectHealth(id: string, signal?: AbortSignal) { return request<Health>(`/api/projects/${id}/health`, { signal }); }
