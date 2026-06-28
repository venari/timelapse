import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Loader2, Battery, Thermometer, HardDrive, Clock, Database } from 'lucide-react';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  Area,
  AreaChart,
  ComposedChart,
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

  // Generate tick values aligned to sensible time boundaries
  const getTickConfig = () => {
    const ticks: number[] = [];
    let current: Date;
    let interval: number;
    let formatStr: string;

    switch (timeRange) {
      case '1h':
        // Align to 10-minute marks
        current = new Date(start);
        current.setMinutes(Math.floor(current.getMinutes() / 10) * 10, 0, 0);
        interval = 10 * 60 * 1000; // 10 minutes in ms
        formatStr = 'HH:mm';
        break;
      case '24h':
        // Align to hours
        current = new Date(start);
        current.setMinutes(0, 0, 0);
        interval = 2 * 60 * 60 * 1000; // 2 hours in ms
        formatStr = 'HH:mm';
        break;
      case '48h':
        // Align to 6-hour marks (0, 6, 12, 18)
        current = new Date(start);
        current.setHours(Math.floor(current.getHours() / 6) * 6, 0, 0, 0);
        interval = 6 * 60 * 60 * 1000; // 6 hours in ms
        formatStr = 'EEE HH:mm';
        break;
      case '7d':
        // Align to midnight
        current = new Date(start);
        current.setHours(0, 0, 0, 0);
        interval = 24 * 60 * 60 * 1000; // 1 day in ms
        formatStr = 'EEE dd MMM';
        break;
      default:
        current = new Date(start);
        interval = 60 * 60 * 1000;
        formatStr = 'HH:mm';
    }

    // Generate ticks from start to end
    while (current.getTime() <= end.getTime()) {
      ticks.push(current.getTime());
      current = new Date(current.getTime() + interval);
    }

    return {
      ticks,
      format: formatStr,
    };
  };

  const tickConfig = getTickConfig();

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
  const rawChartData =
    telemetry?.map((t) => ({
      timestamp: new Date(t.timestamp).getTime(),
      timestampLabel: format(new Date(t.timestamp), 'MMM dd HH:mm'),
      battery: t.batteryPercent,
      temperature: t.temperatureC,
      diskSpace: t.diskSpaceFree ? t.diskSpaceFree / 1024 / 1024 / 1024 : null, // Convert to GB
      voltage: t.batteryVoltage != null ? t.batteryVoltage / 1000 : null, // Convert mV to V
      current: t.batteryCurrent != null ? t.batteryCurrent : null,
      // Boolean values
      charging: t.charging === true ? 1 : 0,
      powerSwitch: t.powerSwitch === true ? 1 : 0,
      connectedWifi: t.connectedToWirelessNetwork === true ? 1 : 0,
      connectedInternet: t.connectedToInternet === true ? 1 : 0,
      // Numeric values
      uptimeHours: t.uptimeSeconds != null ? t.uptimeSeconds / 3600 : null, // Convert to hours
      pendingImages: t.pendingImages != null ? t.pendingImages : null,
      pendingTelemetry: t.pendingTelemetry != null ? t.pendingTelemetry : null,
    })) || [];

  // Calculate max voltage for full-height charging background
  const maxVoltage = Math.max(...rawChartData.map(d => d.voltage || 0).filter(v => v > 0), 5); // Default to 5V minimum
  
  // Calculate max pending uploads for full-height connection status background
  const maxPendingUploads = Math.max(
    ...rawChartData.map(d => Math.max(d.pendingImages || 0, d.pendingTelemetry || 0)),
    10 // Default minimum
  );
  
  const chartData = rawChartData.map(d => ({
    ...d,
    chargingBackground: d.charging === 1 ? maxVoltage * 1.1 : 0, // Extend 10% above max voltage
    powerNoWifiBackground: d.powerSwitch === 1 && d.connectedWifi === 0 ? maxPendingUploads * 1.1 : 0, // Red for power but no WiFi
    wifiOnlyBackground: d.connectedWifi === 1 && d.connectedInternet === 0 ? maxPendingUploads * 1.1 : 0, // Yellow for WiFi only
    internetBackground: d.connectedInternet === 1 ? maxPendingUploads * 1.1 : 0, // Green for Internet
  }));

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
                    ticks={tickConfig.ticks}
                    tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
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
                    ticks={tickConfig.ticks}
                    tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
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
                      ticks={tickConfig.ticks}
                      tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
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
          {chartData.some((d) => d.voltage != null || d.current != null) && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Battery className="h-5 w-5 text-amber-600" />
                  Battery Voltage & Current
                  <span className="text-sm font-normal text-muted-foreground ml-2">
                    (Green background = Charging)
                  </span>
                </CardTitle>
              </CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={300}>
                  <ComposedChart data={chartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis
                      dataKey="timestamp"
                      type="number"
                      domain={['dataMin', 'dataMax']}
                      ticks={tickConfig.ticks}
                      tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
                    />
                    <YAxis yAxisId="left" label={{ value: 'Voltage (V)', angle: -90, position: 'insideLeft' }} />
                    <YAxis yAxisId="right" orientation="right" label={{ value: 'Current (mA)', angle: 90, position: 'insideRight' }} />
                    <Tooltip
                      labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                      formatter={(value: number, name: string) => {
                        if (name === 'Charging') return null;
                        if (name === 'Voltage (V)') {
                          return [`${value.toFixed(2)} V`, name];
                        }
                        return [`${value.toFixed(0)} mA`, name];
                      }}
                    />
                    <Legend 
                      formatter={(value) => {
                        if (value === 'Charging') return null;
                        return value;
                      }}
                    />
                    {/* Background area showing charging status - full height */}
                    <Area
                      yAxisId="left"
                      type="stepAfter"
                      dataKey="chargingBackground"
                      stroke="none"
                      fill="#10b981"
                      fillOpacity={0.15}
                      name="Charging"
                      legendType="none"
                    />
                    {/* Voltage line */}
                    <Line
                      yAxisId="left"
                      type="monotone"
                      dataKey="voltage"
                      stroke="#f59e0b"
                      strokeWidth={2}
                      name="Voltage (V)"
                      dot={false}
                    />
                    {/* Current line */}
                    <Line
                      yAxisId="right"
                      type="monotone"
                      dataKey="current"
                      stroke="#ef4444"
                      name="Current (mA)"
                      dot={false}
                    />
                  </ComposedChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          )}

          {/* Uptime Chart */}
          {chartData.some((d) => d.uptimeHours != null) && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Clock className="h-5 w-5 text-indigo-600" />
                  Uptime
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
                      ticks={tickConfig.ticks}
                      tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
                    />
                    <YAxis unit=" hrs" />
                    <Tooltip
                      labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                      formatter={(value: number) => [`${value.toFixed(1)} hours`, 'Uptime']}
                    />
                    <Legend />
                    <Line
                      type="linear"
                      dataKey="uptimeHours"
                      stroke="#6366f1"
                      name="Uptime (hours)"
                      dot={false}
                    />
                  </LineChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          )}

          {/* Pending Images & Telemetry Chart */}
          {chartData.some((d) => d.pendingImages != null || d.pendingTelemetry != null) && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Database className="h-5 w-5 text-orange-600" />
                  Pending Uploads
                  <span className="text-sm font-normal text-muted-foreground ml-2">
                    (Red = Power but no WiFi, Yellow = WiFi only, Green = Internet)
                  </span>
                </CardTitle>
              </CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={300}>
                  <ComposedChart data={chartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis
                      dataKey="timestamp"
                      type="number"
                      domain={['dataMin', 'dataMax']}
                      ticks={tickConfig.ticks}
                      tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
                    />
                    <YAxis />
                    <Tooltip
                      labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                      formatter={(value: number, name: string) => {
                        if (name === 'Power No WiFi' || name === 'WiFi Only' || name === 'Internet') return null;
                        return [`${value}`, name];
                      }}
                    />
                    <Legend 
                      formatter={(value) => {
                        if (value === 'Power No WiFi' || value === 'WiFi Only' || value === 'Internet') return null;
                        return value;
                      }}
                    />
                    {/* Background areas showing connection status */}
                    {/* Red background for power but no WiFi */}
                    <Area
                      type="stepAfter"
                      dataKey="powerNoWifiBackground"
                      stroke="none"
                      fill="#ef4444"
                      fillOpacity={0.15}
                      name="Power No WiFi"
                      legendType="none"
                    />
                    {/* Yellow background for WiFi only (no internet) */}
                    <Area
                      type="stepAfter"
                      dataKey="wifiOnlyBackground"
                      stroke="none"
                      fill="#eab308"
                      fillOpacity={0.15}
                      name="WiFi Only"
                      legendType="none"
                    />
                    {/* Green background for Internet connection */}
                    <Area
                      type="stepAfter"
                      dataKey="internetBackground"
                      stroke="none"
                      fill="#10b981"
                      fillOpacity={0.15}
                      name="Internet"
                      legendType="none"
                    />
                    {/* Data lines */}
                    <Line
                      type="linear"
                      dataKey="pendingImages"
                      stroke="#f97316"
                      strokeWidth={2}
                      name="Pending Images"
                      dot={false}
                    />
                    <Line
                      type="linear"
                      dataKey="pendingTelemetry"
                      stroke="#fb923c"
                      strokeWidth={2}
                      name="Pending Telemetry"
                      dot={false}
                    />
                  </ComposedChart>
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
