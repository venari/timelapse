import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Layout } from '@/components/Layout';
import { Dashboard } from '@/pages/Dashboard';
import { ImageView } from '@/pages/ImageView';
import { TelemetryGraph } from '@/pages/TelemetryGraph';
import { DeviceEdit } from '@/pages/DeviceEdit';
import { Login } from '@/pages/Login';
import { EventsIndex } from '@/pages/EventsIndex';
import { EventCreate } from '@/pages/EventCreate';
import { EventDetail } from '@/pages/EventDetail';
import { EventEdit } from '@/pages/EventEdit';

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
          <Route path="/login" element={<Login />} />
          <Route element={<Layout />}>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<Dashboard />} />
            <Route path="/device/:deviceId" element={<Dashboard />} />
            <Route path="/device/:deviceId/edit" element={<DeviceEdit />} />
            <Route path="/image-view/:deviceId" element={<ImageView />} />
            <Route path="/telemetry/:deviceId" element={<TelemetryGraph />} />
            <Route path="/event" element={<EventsIndex />} />
            <Route path="/event/new/:imageId" element={<EventCreate />} />
            <Route path="/event/:eventId" element={<EventDetail />} />
            <Route path="/event/:eventId/edit" element={<EventEdit />} />
          </Route>
        </Routes>
      </Router>
    </QueryClientProvider>
  );
}

export default App;
