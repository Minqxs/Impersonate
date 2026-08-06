import { apiBaseUrl } from './apiBaseUrl';

type ApiErrorBody = {
  title?: string;
  message?: string;
  error?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};

export async function apiRequest<T>(
  path: string,
  init?: RequestInit,
  fallbackMessage = 'Request failed'
): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers
    }
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null) as ApiErrorBody | null;
    throw new Error(errorMessage(response.status, body, fallbackMessage));
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export async function apiText(path: string, fallbackMessage = 'Request failed'): Promise<string> {
  const response = await fetch(`${apiBaseUrl}${path}`);

  if (!response.ok) {
    throw new Error(`${fallbackMessage} (${response.status}).`);
  }

  return response.text();
}

function errorMessage(status: number, body: ApiErrorBody | null, fallbackMessage: string) {
  if (body?.errors) {
    return Object.values(body.errors).flat().join(' ');
  }

  return body?.message
    ?? body?.error
    ?? body?.detail
    ?? body?.title
    ?? `${fallbackMessage} (${status}).`;
}
