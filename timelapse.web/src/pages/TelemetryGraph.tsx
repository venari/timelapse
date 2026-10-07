import { useState, useMemo } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { api } from '@/api/client';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import {
  Loader2,
  Battery,
  Thermometer,
  HardDrive,
  Clock,
  Database,
  ZoomIn,
  ZoomOut,
  ChevronLeft,
  ChevronRight,
  SkipForward,
} from 'lucide-react';
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
  ComposedChart,
} from 'recharts';
import { format } from 'date-fns';

const MINUTE = 60 * 1000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

// Zoom/pan bounds for the shared x-axis window.
const MIN_WINDOW_MS = 15 * MINUTE;
const MAX_WINDOW_MS = 120 * DAY;
const ZOOM_FACTOR = 2;
const PAN_FRACTION = 0.5; // pan by half the visible window per click

const PRESETS = [
  { key: '1h', label: 'Last Hour', ms: HOUR },
  { key: '24h', label: 'Last 24 Hours', ms: 24 * HOUR },
  { key: '48h', label: 'Last 48 Hours', ms: 48 * HOUR },
  { key: '7d', label: 'Last 7 Days', ms: 7 * DAY },
] as const;

const clamp = (value: number, min: number, max: number) =>
  Math.min(max, Math.max(min, value));

// Snap a timestamp down to a boundary that suits the given tick interval, so
// ticks land on tidy local times (:00, midnight, ...) rather than arbitrary offsets.
function alignDown(ts: number, interval: number) {
  const d = new Date(ts);
  if (interval >= DAY) {
    d.setHours(0, 0, 0, 0);
  } else if (interval >= HOUR) {
    const step = interval / HOUR;
    d.setHours(Math.floor(d.getHours() / step) * step, 0, 0, 0);
  } else {
    const step = interval / MINUTE;
    d.setMinutes(Math.floor(d.getMinutes() / step) * step, 0, 0);
  }
  return d.getTime();
}

// Derive tick spacing + label format from however wide the visible window is
// (moved outside the component to avoid recreating it every render).
function getTickConfig(start: number, end: number) {
  const spanHours = (end - start) / HOUR;

  let interval: number;
  let formatStr: string;
  if (spanHours <= 2) {
    interval = 15 * MINUTE;
    formatStr = 'HH:mm';
  } else if (spanHours <= 8) {
    interval = HOUR;
    formatStr = 'HH:mm';
  } else if (spanHours <= 30) {
    interval = 2 * HOUR;
    formatStr = 'HH:mm';
  } else if (spanHours <= 78) {
    interval = 6 * HOUR;
    formatStr = 'EEE HH:mm';
  } else if (spanHours <= 24 * 16) {
    interval = DAY;
    formatStr = 'EEE dd MMM';
  } else {
    interval = 7 * DAY;
    formatStr = 'dd MMM';
  }

  const ticks: number[] = [];
  let current = alignDown(start, interval);
  while (current < start) current += interval;
  while (current <= end) {
    ticks.push(current);
    current += interval;
  }

  return { ticks, format: formatStr };
}

export function TelemetryGraph() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const [fullDetail, setFullDetail] = useState(false);

  // The visible x-axis window, shared by every chart. `anchorEnd === null` means
  // "track now" so the charts keep advancing with live data; panning back in time
  // pins the right edge to a fixed timestamp until the user returns to Latest.
  const [view, setView] = useState<{ windowMs: number; anchorEnd: number | null }>({
    windowMs: 24 * HOUR,
    anchorEnd: null,
  });

  const isLive = view.anchorEnd === null;

  // Round "now" to 30s so a live window doesn't thrash query keys / memoised ticks
  // on every render while still advancing.
  const nowRounded = Math.floor(Date.now() / (30 * 1000)) * (30 * 1000);
  const end = view.anchorEnd ?? nowRounded;
  const start = end - view.windowMs;

  const activePreset = isLive
    ? PRESETS.find((p) => p.ms === view.windowMs)?.key ?? ''
    : '';

  const applyPreset = (ms: number) => setView({ windowMs: ms, anchorEnd: null });

  const applyZoom = (factor: number) =>
    setView((v) => {
      const windowMs = clamp(v.windowMs * factor, MIN_WINDOW_MS, MAX_WINDOW_MS);
      if (v.anchorEnd === null) return { windowMs, anchorEnd: null }; // keep tracking now
      // Otherwise zoom about the centre of the current window.
      const centre = v.anchorEnd - v.windowMs / 2;
      return { windowMs, anchorEnd: centre + windowMs / 2 };
    });

  const applyPan = (direction: -1 | 1) =>
    setView((v) => {
      const currentEnd = v.anchorEnd ?? Date.now();
      const nextEnd = currentEnd + direction * v.windowMs * PAN_FRACTION;
      // Panned up to (or past) the present -> resume live tracking.
      if (nextEnd >= Date.now()) return { windowMs: v.windowMs, anchorEnd: null };
      return { windowMs: v.windowMs, anchorEnd: nextEnd };
    });

  // Tick spacing/format follow the window width; memoised on the rounded bounds.
  const tickConfig = useMemo(() => getTickConfig(start, end), [start, end]);

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
    queryKey: ['telemetry', deviceId, view.windowMs, view.anchorEnd, fullDetail],
    queryFn: () =>
      api.getTelemetryBetweenDates(
        Number(deviceId),
        new Date(start).toISOString(),
        new Date(end).toISOString(),
        fullDetail
      ),
    enabled: !!deviceId,
    // Only auto-refresh while tracking "now"; a pinned historical window is static.
    refetchInterval: isLive ? 30000 : false,
    staleTime: 25000, // Consider data stale after 25 seconds (just before refetch)
    gcTime: 60000, // Keep unused data in cache for only 1 minute before garbage collection
    // Keep the previous window's charts on screen while a zoom/pan fetch resolves.
    placeholderData: keepPreviousData,
  });

  // Prepare chart data - memoized to prevent memory leaks from recreating large arrays
  // Must be called before any conditional returns (Rules of Hooks)
  const { chartData, voltageMin, voltageMax } = useMemo(() => {
    const rawChartData =
      telemetry?.map((t) => ({
        timestamp: new Date(t.timestamp).getTime(),
        timestampLabel: format(new Date(t.timestamp), 'MMM dd HH:mm'),
        battery: t.batteryPercent,
        temperature: t.temperatureC,
        diskSpace: t.diskSpaceFree ? t.diskSpaceFree : null, // Convert to GB
        voltage: t.batteryVoltage != null ? t.batteryVoltage / 1000 : null, // Convert mV to V
        current: t.batteryCurrent != null ? t.batteryCurrent : null,
        ioVoltage: t.ioVoltage != null ? t.ioVoltage / 1000 : null, // Convert mV to V
        // Boolean values
        // ESP32 devices never populate the Pi-only Status_Battery/Charge_State fields
        // that `charging` is otherwise derived from, so treat a solar/IO voltage above
        // 4.2V as a charging signal too.
        charging: t.charging === true || (t.ioVoltage != null && t.ioVoltage > 4200) ? 1 : 0,
        powerSwitch: t.powerSwitch === true ? 1 : 0,
        connectedWifi: t.connectedToWirelessNetwork === true ? 1 : 0,
        connectedInternet: t.connectedToInternet === true ? 1 : 0,
        // Numeric values
        uptimeHours: t.uptimeSeconds != null ? t.uptimeSeconds / 3600 : null, // Convert to hours
        pendingImages: t.pendingImages != null ? t.pendingImages : null,
        pendingTelemetry: t.pendingTelemetry != null ? t.pendingTelemetry : null,
      })) || [];

    // Calculate min and max voltage for y-axis domain (battery + IO voltage share this axis)
    const voltageValues = [...rawChartData.map(d => d.voltage), ...rawChartData.map(d => d.ioVoltage)]
      .filter((v): v is number => v != null && v > 0);
    const minVoltage = voltageValues.length > 0 ? Math.min(...voltageValues) : 0;
    const maxVoltage = voltageValues.length > 0 ? Math.max(...voltageValues) : 5;
    
    // Add 5% padding to voltage range for better visualization
    const voltagePadding = (maxVoltage - minVoltage) * 0.05;
    const voltageMin = Math.max(0, minVoltage - voltagePadding);
    const voltageMax = maxVoltage + voltagePadding;
    
    // Calculate max pending uploads for full-height connection status background
    const maxPendingUploads = Math.max(
      ...rawChartData.map(d => Math.max(d.pendingImages || 0, d.pendingTelemetry || 0)),
      10 // Default minimum
    );
    
    const chartData = rawChartData.map(d => ({
      ...d,
      chargingBackground: d.charging === 1 ? voltageMax : null, // Use voltageMax for full-height background
      powerNoWifiBackground: d.powerSwitch === 1 && d.connectedWifi === 0 ? maxPendingUploads * 1.1 : 0, // Red for power but no WiFi
      wifiOnlyBackground: d.connectedWifi === 1 && d.connectedInternet === 0 ? maxPendingUploads * 1.1 : 0, // Yellow for WiFi only
      internetBackground: d.connectedInternet === 1 ? maxPendingUploads * 1.1 : 0, // Green for Internet
    }));

    return { chartData, voltageMin, voltageMax };
  }, [telemetry]);

  // In full-detail mode, plot straight segments between every reading (no spline
  // smoothing) so the chart shows exactly what was recorded.
  const lineType = fullDetail ? 'linear' : 'monotone';

  // Pin the x-axis to the selected window so gaps in the data read as gaps
  // rather than the line being stretched across the full chart width.
  const xDomain: [number, number] = [start, end];

  // ESP32 devices can't report battery current - hide that series entirely when absent.
  const hasCurrent = chartData.some((d) => d.current != null);

  // For short uptimes, minutes read better than "0.3 hrs".
  const maxUptimeHours = Math.max(0, ...chartData.map((d) => d.uptimeHours ?? 0));
  const uptimeInMinutes = maxUptimeHours > 0 && maxUptimeHours < 1;

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

  return (
    <div className="container mx-auto py-8 px-4">
      <div className="mb-6">
        <h1 className="text-4xl font-bold mb-2">{device?.name || 'Loading...'}</h1>
        <p className="text-muted-foreground">Telemetry Graphs</p>
      </div>

      <div className="mb-6 space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <Tabs
            value={activePreset}
            onValueChange={(v) => {
              const preset = PRESETS.find((p) => p.key === v);
              if (preset) applyPreset(preset.ms);
            }}
          >
            <TabsList>
              {PRESETS.map((p) => (
                <TabsTrigger key={p.key} value={p.key}>
                  {p.label}
                </TabsTrigger>
              ))}
            </TabsList>
          </Tabs>

          <div className="flex items-center gap-2">
            <Switch
              id="full-detail"
              checked={fullDetail}
              onCheckedChange={setFullDetail}
            />
            <Label htmlFor="full-detail" className="cursor-pointer">
              Full detail
              <span className="block text-xs font-normal text-muted-foreground">
                Plot every reading instead of averaging into time buckets
              </span>
            </Label>
          </div>
        </div>

        {/* Shared zoom / pan controls - every chart uses the same x-axis window */}
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-1">
            <Button
              variant="outline"
              size="sm"
              onClick={() => applyPan(-1)}
              title="Pan to older data"
            >
              <ChevronLeft className="h-4 w-4" />
              Older
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => applyZoom(1 / ZOOM_FACTOR)}
              disabled={view.windowMs <= MIN_WINDOW_MS}
              title="Zoom in"
            >
              <ZoomIn className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => applyZoom(ZOOM_FACTOR)}
              disabled={view.windowMs >= MAX_WINDOW_MS}
              title="Zoom out"
            >
              <ZoomOut className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => applyPan(1)}
              disabled={isLive}
              title="Pan to newer data"
            >
              Newer
              <ChevronRight className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => applyPreset(view.windowMs)}
              disabled={isLive}
              title="Jump back to the latest data"
            >
              <SkipForward className="h-4 w-4" />
              Latest
            </Button>
          </div>

          <span className="text-sm text-muted-foreground">
            {format(new Date(start), 'PP p')} &ndash;{' '}
            {isLive ? 'now' : format(new Date(end), 'PP p')}
          </span>
        </div>
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
                    domain={xDomain}
                    ticks={tickConfig.ticks}
                    tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
                  />
                  <YAxis domain={[0, 100]} unit="%" />
                  <Tooltip
                    labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                    formatter={(value) => [`${Number(value).toFixed(1)}%`, 'Battery']}
                  />
                  <Legend />
                  <Line
                    type={lineType}
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
                    domain={xDomain}
                    ticks={tickConfig.ticks}
                    tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
                  />
                  <YAxis unit="°C" />
                  <Tooltip
                    labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                    formatter={(value) => [`${Number(value).toFixed(1)}°C`, 'Temperature']}
                  />
                  <Legend />
                  <Line
                    type={lineType}
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
                      domain={xDomain}
                      ticks={tickConfig.ticks}
                      tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
                    />
                    <YAxis unit=" GB" />
                    <Tooltip
                      labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                      formatter={(value) => [`${Number(value).toFixed(2)} GB`, 'Free Space']}
                    />
                    <Legend />
                    <Line
                      type={lineType}
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
          {chartData.some((d) => d.voltage != null || d.current != null || d.ioVoltage != null) && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Battery className="h-5 w-5 text-amber-600" />
                  {hasCurrent ? 'Battery Voltage & Current' : 'Battery Voltage'}
                  <span className="text-sm font-normal text-muted-foreground ml-2">
                    (Green background = Charging, or IO Voltage &gt; 4.2V)
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
                      domain={xDomain}
                      ticks={tickConfig.ticks}
                      tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
                    />
                    <YAxis yAxisId="left" domain={[voltageMin, voltageMax]} label={{ value: 'Voltage (V)', angle: -90, position: 'insideLeft' }} />
                    {hasCurrent && (
                      <YAxis yAxisId="right" orientation="right" label={{ value: 'Current (mA)', angle: 90, position: 'insideRight' }} />
                    )}
                    <Tooltip
                      labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                      formatter={(value, name) => {
                        if (name === 'Charging') return null;
                        if (name === 'Voltage (V)' || name === 'IO Voltage (V)') {
                          return [`${Number(value).toFixed(2)} V`, name];
                        }
                        return [`${Number(value).toFixed(0)} mA`, name];
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
                      type={lineType}
                      dataKey="voltage"
                      stroke="#f59e0b"
                      strokeWidth={2}
                      name="Voltage (V)"
                      dot={false}
                    />
                    {/* IO (solar) voltage line - also drives the Charging background above 4.2V */}
                    <Line
                      yAxisId="left"
                      type={lineType}
                      dataKey="ioVoltage"
                      stroke="#8b5cf6"
                      strokeWidth={2}
                      name="IO Voltage (V)"
                      dot={false}
                    />
                    {/* Current line - ESP32 devices don't report this */}
                    {hasCurrent && (
                      <Line
                        yAxisId="right"
                        type={lineType}
                        dataKey="current"
                        stroke="#ef4444"
                        name="Current (mA)"
                        dot={false}
                      />
                    )}
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
                      domain={xDomain}
                      ticks={tickConfig.ticks}
                      tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
                    />
                    <YAxis unit={uptimeInMinutes ? ' min' : ' hrs'} />
                    <Tooltip
                      labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                      formatter={(value) =>
                        uptimeInMinutes
                          ? [`${Number(value).toFixed(0)} minutes`, 'Uptime']
                          : [`${Number(value).toFixed(1)} hours`, 'Uptime']
                      }
                    />
                    <Legend />
                    <Line
                      type="linear"
                      dataKey={(d) => {
                        const h = d.uptimeHours;
                        return h == null ? null : uptimeInMinutes ? h * 60 : h;
                      }}
                      stroke="#6366f1"
                      name={uptimeInMinutes ? 'Uptime (minutes)' : 'Uptime (hours)'}
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
                      domain={xDomain}
                      ticks={tickConfig.ticks}
                      tickFormatter={(timestamp) => format(new Date(timestamp), tickConfig.format)}
                    />
                    <YAxis />
                    <Tooltip
                      labelFormatter={(timestamp) => format(new Date(timestamp), 'PPpp')}
                      formatter={(value, name) => {
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
