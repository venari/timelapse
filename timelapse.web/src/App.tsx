import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Layout } from '@/components/Layout';
import { Dashboard } from '@/pages/Dashboard';
import { ImageView } from '@/pages/ImageView';
import { TelemetryGraph } from '@/pages/TelemetryGraph';
import { DeviceEdit } from '@/pages/DeviceEdit';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5, // 5 minutes
      retry: 1,
    },
  },
});

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <Router>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<Dashboard />} />
            <Route path="/device/:deviceId" element={<Dashboard />} />
            <Route path="/device/:deviceId/edit" element={<DeviceEdit />} />
            <Route path="/image-view/:deviceId" element={<ImageView />} />
            <Route path="/telemetry/:deviceId" element={<TelemetryGraph />} />
          </Route>
        </Routes>
      </Router>
    </QueryClientProvider>
  );
}

export default App;
