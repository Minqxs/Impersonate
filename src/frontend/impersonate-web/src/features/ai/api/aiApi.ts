const baseUrl=import.meta.env.VITE_API_BASE_URL?.replace(/\/$/,'')??'https://localhost:7001';
export type ProviderType='Anthropic'|'OpenAI'|'GoogleGemini'|'OpenRouter';
export interface ProviderConnection{id:string;providerType:ProviderType;displayName:string;status:string;lastValidatedAtUtc?:string;lastModelSyncAtUtc?:string;availableModelCount:number;lastSafeFailureMessage?:string}
async function request<T>(path:string,init?:RequestInit){const response=await fetch(`${baseUrl}${path}`,{...init,headers:{'Content-Type':'application/json',...init?.headers}});if(!response.ok)throw new Error(`AI provider request failed (${response.status}).`);if(response.status===204)return undefined as T;return response.json() as Promise<T>}
export const listProviders=()=>request<{supportedProviders:ProviderType[];connections:ProviderConnection[]}>('/api/ai/providers');
export const connectProvider=(type:ProviderType,apiKey:string)=>request<ProviderConnection>(`/api/ai/providers/${type}/connections`,{method:'POST',body:JSON.stringify({displayName:type==='GoogleGemini'?'Google Gemini':type,apiKey})});
export const validateProvider=(id:string)=>request<ProviderConnection>(`/api/ai/provider-connections/${id}/validate`,{method:'POST'});
export const syncProvider=(id:string)=>request<ProviderConnection>(`/api/ai/provider-connections/${id}/sync-models`,{method:'POST'});
export const disableProvider=(id:string)=>request<void>(`/api/ai/provider-connections/${id}/disable`,{method:'PUT'});
export const removeProvider=(id:string)=>request<void>(`/api/ai/provider-connections/${id}`,{method:'DELETE'});
