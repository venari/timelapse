import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { DeviceCard } from '@/components/DeviceCard';
import { Loader2 } from 'lucide-react';

export function Dashboard() {
  const { data: devices, isLoading, error } = useQuery({
    queryKey: ['devices'],
    queryFn: api.getDevices,
    refetchInterval: 30000, // Refetch every 30 seconds for real-time updates
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-destructive mb-2">Error Loading Devices</h2>
          <p className="text-muted-foreground">
            {error instanceof Error ? error.message : 'An unknown error occurred'}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-8 px-4">
      <div className="mb-8">
        <h1 className="text-4xl font-bold mb-2">Envirocam Dashboard</h1>
        <p className="text-muted-foreground">
          Monitor your envirocam units and view their latest status
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {devices && devices.length > 0 ? (
          devices.map((device) => <DeviceCard key={device.id} device={device} />)
        ) : (
          <div className="col-span-full text-center py-12">
            <p className="text-muted-foreground">No devices found</p>
          </div>
        )}
      </div>
    </div>
  );
}
