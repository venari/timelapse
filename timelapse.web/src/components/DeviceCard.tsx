import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Battery, Thermometer, HardDrive, Camera } from 'lucide-react';
import type { Device } from '@/types';
import { formatDistanceToNow } from 'date-fns';
import { Link } from 'react-router-dom';
import { getImageUrl } from '@/lib/imageUtils';

interface DeviceCardProps {
  device: Device;
}

export function DeviceCard({ device }: DeviceCardProps) {
  const telemetry = device.latestTelemetry;
  const image = device.latestImage;

  const getBatteryColor = (percent: number) => {
    if (percent > 60) return 'text-green-600';
    if (percent > 30) return 'text-yellow-600';
    return 'text-red-600';
  };

  const getStatusBadges = () => {
    const badges = [];
    if (device.supportMode) badges.push({ label: 'Support Mode', variant: 'destructive' as const });
    if (device.monitoringMode) badges.push({ label: 'Monitoring', variant: 'default' as const });
    if (device.service) badges.push({ label: 'Service', variant: 'secondary' as const });
    if (device.hibernateMode) badges.push({ label: 'Hibernate', variant: 'outline' as const });
    if (device.powerOff) badges.push({ label: 'Power Off', variant: 'destructive' as const });
    if (device.retired) badges.push({ label: 'Retired', variant: 'outline' as const });
    return badges;
  };

  return (
    <Card className="hover:shadow-lg transition-shadow">
      <CardHeader>
        <div className="flex justify-between items-start">
          <div>
            <CardTitle className="text-xl">
              <Link to={`/device/${device.id}`} className="hover:underline">
                {device.name}
              </Link>
            </CardTitle>
            <CardDescription>{device.serialNumber}</CardDescription>
          </div>
          <div className="flex gap-2 flex-wrap justify-end">
            {getStatusBadges().map((badge) => (
              <Badge key={badge.label} variant={badge.variant}>
                {badge.label}
              </Badge>
            ))}
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* Latest Image */}
          <div className="space-y-2">
            <h4 className="text-sm font-semibold flex items-center gap-2">
              <Camera className="h-4 w-4" />
              Latest Image
            </h4>
            {image ? (
              <Link to={`/image-view/${device.id}`}>
                <div className="aspect-video bg-muted rounded-md overflow-hidden cursor-pointer hover:opacity-80 transition-opacity">
                  <img
                    src={getImageUrl(image.id)}
                    alt={`Latest from ${device.name}`}
                    className="w-full h-full object-cover"
                  />
                </div>
                <p className="text-xs text-muted-foreground mt-1">
                  {formatDistanceToNow(new Date(image.timestamp), { addSuffix: true })}
                </p>
              </Link>
            ) : (
              <div className="aspect-video bg-muted rounded-md flex items-center justify-center">
                <p className="text-sm text-muted-foreground">No image available</p>
              </div>
            )}
          </div>

          {/* Telemetry */}
          <div className="space-y-3">
            <h4 className="text-sm font-semibold">Current Status</h4>
            {telemetry ? (
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Battery className={`h-4 w-4 ${getBatteryColor(telemetry.batteryPercent)}`} />
                    <span className="text-sm">Battery</span>
                  </div>
                  <span className={`text-sm font-semibold ${getBatteryColor(telemetry.batteryPercent)}`}>
                    {telemetry.batteryPercent.toFixed(1)}%
                  </span>
                </div>

                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Thermometer className="h-4 w-4 text-blue-600" />
                    <span className="text-sm">Temperature</span>
                  </div>
                  <span className="text-sm font-semibold">{telemetry.temperatureC.toFixed(1)}°C</span>
                </div>

                {telemetry.diskSpaceFree != null && (
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <HardDrive className="h-4 w-4 text-purple-600" />
                      <span className="text-sm">Disk Space</span>
                    </div>
                    <span className="text-sm font-semibold">
                      {telemetry.diskSpaceFree.toFixed(1)} GB
                    </span>
                  </div>
                )}

                <p className="text-xs text-muted-foreground pt-2">
                  Updated {formatDistanceToNow(new Date(telemetry.timestamp), { addSuffix: true })}
                </p>

                <Link
                  to={`/telemetry/${device.id}`}
                  className="text-sm text-primary hover:underline inline-block"
                >
                  View detailed charts →
                </Link>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">No telemetry available</p>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
