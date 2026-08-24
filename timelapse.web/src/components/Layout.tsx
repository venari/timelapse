import { Link, Outlet } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Camera } from 'lucide-react';
import { api } from '@/api/client';
import { Button } from '@/components/ui/button';

export function Layout() {
  const queryClient = useQueryClient();
  const { data: user } = useQuery({
    queryKey: ['auth', 'me'],
    queryFn: api.getCurrentUser,
  });

  const handleLogout = async () => {
    await api.logout();
    queryClient.clear();
    window.location.href = '/login';
  };

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
              <Link to="/event" className="text-sm hover:underline">
                Events
              </Link>
            </div>
            {user && (
              <div className="ml-auto flex items-center gap-3">
                <span className="text-sm text-muted-foreground">{user.email}</span>
                <Button variant="outline" size="sm" onClick={handleLogout}>
                  Log out
                </Button>
              </div>
            )}
          </div>
        </div>
      </nav>

      <Outlet />
    </div>
  );
}
