import { createContext, useContext } from 'react';
import type { PipelineEvent, PipelineRun } from '../api/runsApi';
export type RunDetailContextValue={run:PipelineRun;timeline?:PipelineEvent[];refresh:()=>void;retryExecution:()=>void;retrying:boolean};
export const RunDetailContext=createContext<RunDetailContextValue|null>(null);
export const useRunDetail=()=>{const value=useContext(RunDetailContext);if(!value)throw new Error('Run detail context is unavailable.');return value;};
