import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Dashboard } from '@/pages/Dashboard';
import { ImageView } from '@/pages/ImageView';
import { TelemetryGraph } from '@/pages/TelemetryGraph';
import { Camera } from 'lucide-react';

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
        <div className="min-h-screen bg-background">
          {/* Navigation */}
          <nav className="border-b">
            <div className="container mx-auto px-4 py-4">
              <div className="flex items-center gap-6">
                <Link to="/" className="flex items-center gap-2 font-bold text-xl">
                  <Camera className="h-6 w-6" />
                  Timelapse
                </Link>
                <div className="flex gap-4">
                  <Link to="/" className="text-sm hover:underline">
                    Dashboard
                  </Link>
                </div>
              </div>
            </div>
          </nav>

          {/* Routes */}
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/device/:deviceId" element={<Dashboard />} />
            <Route path="/image-view/:deviceId" element={<ImageView />} />
            <Route path="/telemetry/:deviceId" element={<TelemetryGraph />} />
          </Routes>
        </div>
      </Router>
    </QueryClientProvider>
  );
}

export default App;
