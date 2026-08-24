import { Link, Outlet } from 'react-router-dom';
import { Camera } from 'lucide-react';

export function Layout() {
  return (
    <div className="min-h-screen bg-background">
      <nav className="border-b">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center gap-6">
            <Link to="/dashboard" className="flex items-center gap-2 font-bold text-xl">
              <Camera className="h-6 w-6" />
              Envirocam
            </Link>
            <div className="flex gap-4">
              <Link to="/dashboard" className="text-sm hover:underline">
                Dashboard
              </Link>
            </div>
          </div>
        </div>
      </nav>

      <Outlet />
    </div>
  );
}
