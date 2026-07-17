import { createContext, useContext, useEffect, useState, type PropsWithChildren } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getProject, type Project } from './api/projectsApi';
const storageKey = 'impersonate.activeProjectId';
type Context = { activeProjectId: string | null; activeProject?: Project; setActiveProject: (id: string) => void; clearActiveProject: () => void; isLoading: boolean; error: Error | null };
const ActiveProjectContext = createContext<Context | undefined>(undefined);
export function ActiveProjectProvider({ children }: PropsWithChildren) { const [activeProjectId, setId] = useState<string | null>(() => localStorage.getItem(storageKey)); const query = useQuery({ queryKey: ['project', activeProjectId], queryFn: () => getProject(activeProjectId!), enabled: !!activeProjectId, retry: false }); useEffect(() => { if (query.isError) { localStorage.removeItem(storageKey); setId(null); } }, [query.isError]); const setActiveProject = (id: string) => { localStorage.setItem(storageKey, id); setId(id); }; const clearActiveProject = () => { localStorage.removeItem(storageKey); setId(null); }; return <ActiveProjectContext.Provider value={{ activeProjectId, activeProject: query.data, setActiveProject, clearActiveProject, isLoading: query.isLoading, error: query.error }}>{children}</ActiveProjectContext.Provider>; }
export function useActiveProject() { const context = useContext(ActiveProjectContext); if (!context) throw new Error('useActiveProject must be used within ActiveProjectProvider.'); return context; }
