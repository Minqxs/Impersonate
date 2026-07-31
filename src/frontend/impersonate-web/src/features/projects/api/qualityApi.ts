const baseUrl=import.meta.env.VITE_API_BASE_URL?.replace(/\/$/,'')??'https://localhost:7001';
export type QualityState='NotConfigured'|'Loading'|'Available'|'Passed'|'Failed'|'TemporarilyUnavailable'|'AuthenticationRequired'|'ProjectNotFound';
export type QualityMetric={value?:number;rating?:string};
export type QualitySummary={state:QualityState;qualityGate?:string;coverage:QualityMetric;newCoverage:QualityMetric;bugs:QualityMetric;vulnerabilities:QualityMetric;codeSmells:QualityMetric;reliability:QualityMetric;security:QualityMetric;maintainability:QualityMetric;duplicatedLines:QualityMetric;linesOfCode:QualityMetric;cognitiveComplexity:QualityMetric;lastSuccessfulRefreshAtUtc?:string;failureCode?:string;safeMessage?:string;projectUrl?:string};
export type QualityConfiguration={configured:boolean;enabled:boolean;baseUrl?:string;projectKey?:string;displayName?:string;credentialConfigured:boolean;lastSuccessfulRefreshAtUtc?:string;lastFailureCode?:string;lastSafeFailureMessage?:string};
export type QualityConfigurationInput={enabled:boolean;baseUrl:string;projectKey:string;displayName?:string;token?:string};
async function request<T>(path:string,init?:RequestInit){const response=await fetch(`${baseUrl}${path}`,{...init,headers:{'Content-Type':'application/json',...init?.headers}});if(!response.ok){const body=await response.json().catch(()=>null)as{message?:string}|null;throw new Error(body?.message??`Request failed (${response.status}).`)}if(response.status===204)return undefined as T;return response.json()as Promise<T>}
export const qualityKeys={configuration:(id:string)=>['project-quality-configuration',id]as const,summary:(id:string)=>['project-quality-summary',id]as const};
export const getQualityConfiguration=(id:string)=>request<QualityConfiguration>(`/api/projects/${id}/quality/configuration`);
export const saveQualityConfiguration=(id:string,input:QualityConfigurationInput)=>request<QualityConfiguration>(`/api/projects/${id}/quality/configuration`,{method:'PUT',body:JSON.stringify(input)});
export const removeQualityConfiguration=(id:string)=>request<void>(`/api/projects/${id}/quality/configuration`,{method:'DELETE'});
export const getQualitySummary=(id:string)=>request<QualitySummary>(`/api/projects/${id}/quality/summary`);
export const refreshQualitySummary=(id:string)=>request<QualitySummary>(`/api/projects/${id}/quality/refresh`,{method:'POST'});
export const validateQualityConnection=(id:string)=>request<QualitySummary>(`/api/projects/${id}/quality/validate`,{method:'POST'});
