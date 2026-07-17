import { createBrowserRouter } from 'react-router-dom';
import { AppLayout } from '../layouts/AppLayout';
import { PlaceholderPage } from '../pages/PlaceholderPage';

const page = (title: string, description: string) => <PlaceholderPage title={title} description={description} />;

export const router = createBrowserRouter([{ path: '/', element: <AppLayout />, children: [
  { index: true, element: page('Dashboard', 'A foundation-stage overview will be introduced as operational capabilities are added.') },
  { path: 'projects', element: page('Projects', 'Project and workspace capabilities are planned for the next milestone.') },
  { path: 'runs', element: page('Runs', 'Pipeline execution is intentionally deferred.') },
  { path: 'brain', element: page('Brain', 'Operational insights will be introduced after there is real system activity.') },
  { path: 'personality', element: page('Personality', 'Engineering personality capabilities are not part of the foundation.') },
  { path: 'settings', element: page('Settings', 'Application settings will be designed with their owning features.') },
] }]);
