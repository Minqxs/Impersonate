const baseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? 'https://localhost:7001';

export async function getApiMetadata(): Promise<{ name: string; status: string }> {
  const response = await fetch(`${baseUrl}/`);
  if (!response.ok) throw new Error(`API metadata request failed with status ${response.status}.`);
  return response.json() as Promise<{ name: string; status: string }>;
}
