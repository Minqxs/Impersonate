import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import { Chip } from '@mui/material';

export function StatusIndicator() { return <Chip icon={<CheckCircleOutlineIcon />} label="Foundation ready" color="success" size="small" variant="outlined" />; }
