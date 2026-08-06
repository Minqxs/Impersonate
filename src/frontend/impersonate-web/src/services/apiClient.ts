import { apiRequest } from './apiRequest';

export async function getApiMetadata(): Promise<{ name: string; status: string }> {
  return apiRequest<{ name: string; status: string }>('/api/metadata', undefined, 'API metadata request failed');
}
