const baseUrl=import.meta.env.VITE_API_BASE_URL?.replace(/\/$/,'')??'https://localhost:7001';
export type ProviderType='Anthropic'|'OpenAI'|'GoogleGemini'|'OpenRouter';
export interface ProviderConnection{id:string;providerType:ProviderType;displayName:string;status:string;lastValidatedAtUtc?:string;lastModelSyncAtUtc?:string;availableModelCount:number;lastFailureCode?:string;lastSafeFailureMessage?:string}
export interface ModelUsageSummary{provider:string;model:string;attemptCount:number;successfulPlanCount:number;invalidOutputCount:number;providerFailureCount:number;timedOutCount:number;inputTokenCount:number;outputTokenCount:number;averageDurationMilliseconds:number;validPlanRate:number}
async function request<T>(path:string,init?:RequestInit){const response=await fetch(`${baseUrl}${path}`,{...init,headers:{'Content-Type':'application/json',...init?.headers}});if(!response.ok){const error=await response.json().catch(()=>null) as {message?:string}|null;throw new Error(error?.message??`AI provider request failed (${response.status}).`)}if(response.status===204)return undefined as T;return response.json() as Promise<T>}
export const listProviders=()=>request<{supportedProviders:ProviderType[];connections:ProviderConnection[]}>('/api/ai/providers');
export const getModelUsage=(days=30)=>request<{days:number;models:ModelUsageSummary[]}>(`/api/ai/usage/models?days=${days}`);
export const connectProvider=(type:ProviderType,apiKey:string)=>request<ProviderConnection>(`/api/ai/providers/${type}/connections`,{method:'POST',body:JSON.stringify({displayName:type==='GoogleGemini'?'Google Gemini':type,apiKey})});
export const replaceProviderCredentials=(id:string,apiKey:string)=>request<ProviderConnection>(`/api/ai/provider-connections/${id}/credentials`,{method:'PUT',body:JSON.stringify({apiKey,organisation:null,project:null})});
export const validateProvider=(id:string)=>request<ProviderConnection>(`/api/ai/provider-connections/${id}/validate`,{method:'POST'});
export const syncProvider=(id:string)=>request<ProviderConnection>(`/api/ai/provider-connections/${id}/sync-models`,{method:'POST'});
export const disableProvider=(id:string)=>request<void>(`/api/ai/provider-connections/${id}/disable`,{method:'PUT'});
export const removeProvider=(id:string)=>request<void>(`/api/ai/provider-connections/${id}`,{method:'DELETE'});
