import { Alert, Paper, Typography } from '@mui/material';

interface PlaceholderPageProps { title: string; description: string; }

export function PlaceholderPage({ title, description }: PlaceholderPageProps) {
  return <div className="space-y-6"><div><Typography variant="h3" component="h2" fontWeight={700}>{title}</Typography><Typography color="text.secondary" className="mt-2">{description}</Typography></div><Alert severity="info">Impersonate is in its foundation stage. This area intentionally contains no operational features yet.</Alert><Paper variant="outlined" className="p-6"><Typography variant="h6">Foundation status</Typography><Typography color="text.secondary" className="mt-2">The application shell, navigation, theming, API client foundation, and shared runtime conventions are ready for the next milestone.</Typography></Paper></div>;
}
