import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Loader2, Battery, Thermometer, HardDrive } from 'lucide-react';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from 'recharts';
import { format, subHours, subDays } from 'date-fns';

export function TelemetryGraph() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const [timeRange, setTimeRange] = useState<'1h' | '24h' | '48h' | '7d'>('24h');

  const getTimeRange = () => {
    const now = new Date();
    switch (timeRange) {
      case '1h':
        return { start: subHours(now, 1), end: now };
      case '24h':
        return { start: subHours(now, 24), end: now };
      case '48h':
        return { start: subHours(now, 48), end: now };
      case '7d':
        return { start: subDays(now, 7), end: now };
    }
  };

  const { start, end } = getTimeRange();

  const {
    data: device,
    isLoading: deviceLoading,
    error: deviceError,
  } = useQuery({
    queryKey: ['device', deviceId],
    queryFn: () => api.getDevice(Number(deviceId)),
    enabled: !!deviceId,
  });

  const {
    data: telemetry,
    isLoading: telemetryLoading,
    error: telemetryError,
  } = useQuery({
    queryKey: ['telemetry', deviceId, timeRange],
    queryFn: () =>
      api.getTelemetryBetweenDates(
        Number(deviceId),
        start.toISOString(),
        end.toISOString()
      ),
    enabled: !!deviceId,
    refetchInterval: 30000, // Refetch every 30 seconds for real-time updates
  });

  const handleTimeRangeChange = (value: string) => {
    setTimeRange(value as '1h' | '24h' | '48h' | '7d');
  };

  if (deviceLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    );
  }

  if (deviceError || telemetryError) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-destructive mb-2">Error Loading Telemetry</h2>
          <p className="text-muted-foreground">
            {deviceError instanceof Error
              ? deviceError.message
              : telemetryError instanceof Error
              ? telemetryError.message
              : 'An unknown error occurred'}
          </p>
        </div>
      </div>
    );
  }

  // Prepare chart data
  const chartData =
    telemetry?.map((t) => ({
      timestamp: new Date(t.timestamp).getTime(),
      timestampLabel: format(new Date(t.timestamp), 'MMM dd HH:mm'),
      battery: t.batteryPercent,
      temperature: t.temperatureC,
      diskSpace: t.diskSpaceFree ? t.diskSpaceFree / 1024 / 1024 / 1024 : null, // Convert to GB
      voltage: t.status?.batteryVoltage,
      current: t.status?.batteryCurrent,
    })) || [];

  return (
    <div className="container mx-auto py-8 px-4">
      <div className="mb-6">
        <h1 className="text-4xl font-bold mb-2">{device?.name || 'Loading...'}</h1>
        <p className="text-muted-foreground">Telemetry Graphs</p>
      </div>

      <div className="mb-6">
        <Tabs value={timeRange} onValueChange={handleTimeRangeChange}>
          <TabsList>
            <TabsTrigger value="1h">Last Hour</TabsTrigger>
            <TabsTrigger value="24h">Last 24 Hours</TabsTrigger>
            <TabsTrigger value="48h">Last 48 Hours</TabsTrigger>
            <TabsTrigger value="7d">Last 7 Days</TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      {telemetryLoading ? (
        <div className="flex items-center justify-center min-h-[400px]">
          <Loader2 className="h-8 w-8 animate-spin text-primary" />
        </div>
      ) : chartData.length > 0 ? (
        <div className="space-y-6">
          {/* Battery Chart */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Battery className="h-5 w-5 text-green-600" />
                Battery Level
              </CardTitle>
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <LineChart data={chartData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis
                    dataKey="timestamp"
                    type="number"
                    domain={['dataMin', 'dataMax']}
                    tickFormatter={(timestamp) => format(new Date(timestamp), 'HH:mm')}
                  />
                  <YAxis domain={[0, 100]} unit="%" />
                  <Tooltip
                    labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                    formatter={(value: number) => [`${value.toFixed(1)}%`, 'Battery']}
                  />
                  <Legend />
                  <Line
                    type="monotone"
                    dataKey="battery"
                    stroke="#10b981"
                    name="Battery %"
                    dot={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>

          {/* Temperature Chart */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Thermometer className="h-5 w-5 text-blue-600" />
                Temperature
              </CardTitle>
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <LineChart data={chartData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis
                    dataKey="timestamp"
                    type="number"
                    domain={['dataMin', 'dataMax']}
                    tickFormatter={(timestamp) => format(new Date(timestamp), 'HH:mm')}
                  />
                  <YAxis unit="°C" />
                  <Tooltip
                    labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                    formatter={(value: number) => [`${value.toFixed(1)}°C`, 'Temperature']}
                  />
                  <Legend />
                  <Line
                    type="monotone"
                    dataKey="temperature"
                    stroke="#3b82f6"
                    name="Temperature °C"
                    dot={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>

          {/* Disk Space Chart */}
          {chartData.some((d) => d.diskSpace !== null) && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <HardDrive className="h-5 w-5 text-purple-600" />
                  Disk Space
                </CardTitle>
              </CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={300}>
                  <LineChart data={chartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis
                      dataKey="timestamp"
                      type="number"
                      domain={['dataMin', 'dataMax']}
                      tickFormatter={(timestamp) => format(new Date(timestamp), 'HH:mm')}
                    />
                    <YAxis unit=" GB" />
                    <Tooltip
                      labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                      formatter={(value: number) => [`${value.toFixed(2)} GB`, 'Free Space']}
                    />
                    <Legend />
                    <Line
                      type="monotone"
                      dataKey="diskSpace"
                      stroke="#a855f7"
                      name="Free Space GB"
                      dot={false}
                    />
                  </LineChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          )}

          {/* Voltage & Current Chart */}
          {chartData.some((d) => d.voltage !== null || d.current !== null) && (
            <Card>
              <CardHeader>
                <CardTitle>Battery Voltage & Current</CardTitle>
              </CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={300}>
                  <LineChart data={chartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis
                      dataKey="timestamp"
                      type="number"
                      domain={['dataMin', 'dataMax']}
                      tickFormatter={(timestamp) => format(new Date(timestamp), 'HH:mm')}
                    />
                    <YAxis yAxisId="left" />
                    <YAxis yAxisId="right" orientation="right" />
                    <Tooltip
                      labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                    />
                    <Legend />
                    <Line
                      yAxisId="left"
                      type="monotone"
                      dataKey="voltage"
                      stroke="#f59e0b"
                      name="Voltage (V)"
                      dot={false}
                    />
                    <Line
                      yAxisId="right"
                      type="monotone"
                      dataKey="current"
                      stroke="#ef4444"
                      name="Current (mA)"
                      dot={false}
                    />
                  </LineChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          )}
        </div>
      ) : (
        <Card>
          <CardContent className="flex items-center justify-center h-[400px]">
            <p className="text-muted-foreground">No telemetry data available for this time range</p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
