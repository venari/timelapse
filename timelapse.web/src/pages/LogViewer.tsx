import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { getLogUrl, getCoreDumpUrl } from '@/lib/logUtils';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Loader2, Download, RefreshCw, FileWarning } from 'lucide-react';

export function LogViewer() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const id = Number(deviceId);

  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const [filter, setFilter] = useState('');

  const { data: device } = useQuery({
    queryKey: ['device', deviceId],
    queryFn: () => api.getDevice(id),
    enabled: !!deviceId,
  });

  const { data: dates, isLoading: datesLoading } = useQuery({
    queryKey: ['logDates', deviceId],
    queryFn: () => api.getLogDates(id),
    enabled: !!deviceId,
    refetchInterval: 60000, // a new day's blob can appear at any wake once the device rolls over
  });

  // Default to the most recent day once the date list loads, without stomping on a
  // date the user's since picked themselves.
  useEffect(() => {
    if (dates && dates.length > 0 && selectedDate === null) {
      setSelectedDate(dates[0]);
    }
  }, [dates, selectedDate]);

  const isLatest = !!dates && !!selectedDate && dates[0] === selectedDate;

  const {
    data: logText,
    isLoading: logLoading,
    isFetching: logFetching,
    refetch: refetchLog,
  } = useQuery({
    queryKey: ['log', deviceId, selectedDate],
    queryFn: () => api.getLog(id, selectedDate as string),
    enabled: !!deviceId && !!selectedDate,
    // Only the most recent day is still being appended to on the device - no point
    // polling a day that's already rotated and done.
    refetchInterval: isLatest ? 15000 : false,
  });

  const { data: coreDumps } = useQuery({
    queryKey: ['coreDumps', deviceId],
    queryFn: () => api.getCoreDumps(id),
    enabled: !!deviceId,
    refetchInterval: 60000,
  });

  const lines = useMemo(() => (logText ?? '').split('\n'), [logText]);
  const filteredLines = useMemo(() => {
    if (!filter.trim()) return lines;
    const needle = filter.toLowerCase();
    return lines.filter((line) => line.toLowerCase().includes(needle));
  }, [lines, filter]);

  return (
    <div className="container mx-auto py-8 px-4 max-w-5xl">
      <div className="mb-6">
        <h1 className="text-4xl font-bold mb-2">{device?.name || 'Loading...'}</h1>
        <p className="text-muted-foreground">Device Logs</p>
      </div>

      <Card className="mb-6">
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-4">
            <CardTitle>Log</CardTitle>
            <div className="flex flex-wrap items-center gap-2">
              <Select
                value={selectedDate ?? undefined}
                onValueChange={setSelectedDate}
                disabled={datesLoading || !dates || dates.length === 0}
              >
                <SelectTrigger className="w-[180px]">
                  <SelectValue placeholder={datesLoading ? 'Loading...' : 'No logs available'} />
                </SelectTrigger>
                <SelectContent>
                  {dates?.map((date) => (
                    <SelectItem key={date} value={date}>
                      {date}
                      {dates[0] === date ? ' (latest)' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Input
                placeholder="Filter lines..."
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
                className="w-[200px]"
              />
              <Button variant="outline" size="sm" onClick={() => refetchLog()} disabled={logFetching}>
                <RefreshCw className={`h-4 w-4 ${logFetching ? 'animate-spin' : ''}`} />
              </Button>
              {selectedDate && (
                <Button variant="outline" size="sm" asChild>
                  <a href={getLogUrl(id, selectedDate)} target="_blank" rel="noreferrer">
                    <Download className="h-4 w-4" />
                    Raw
                  </a>
                </Button>
              )}
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {logLoading ? (
            <div className="flex items-center justify-center h-[200px]">
              <Loader2 className="h-6 w-6 animate-spin text-primary" />
            </div>
          ) : !selectedDate ? (
            <p className="text-muted-foreground text-sm">No logs have been pushed for this device yet.</p>
          ) : (
            <>
              <pre className="bg-muted rounded-md p-4 text-xs leading-relaxed overflow-auto max-h-[65vh] whitespace-pre-wrap break-all">
                {filteredLines.length > 0 ? filteredLines.join('\n') : 'No lines match the filter.'}
              </pre>
              <p className="text-xs text-muted-foreground mt-2">
                {filter.trim()
                  ? `${filteredLines.length} of ${lines.length} lines`
                  : `${lines.length} lines`}
                {isLatest ? ' · refreshing every 15s' : ''}
              </p>
            </>
          )}
        </CardContent>
      </Card>

      {coreDumps && coreDumps.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <FileWarning className="h-5 w-5 text-destructive" />
              Core Dumps
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground mb-3">
              Extracted automatically after a crash - download and decode with{' '}
              <code className="text-xs bg-muted px-1 py-0.5 rounded">espcoredump.py info_corefile</code>{' '}
              against the matching firmware build's <code className="text-xs bg-muted px-1 py-0.5 rounded">firmware.elf</code>.
              The crashing task, PC and backtrace addresses are also logged as plain text in the log
              above - search for "Core dump:".
            </p>
            <ul className="space-y-1">
              {coreDumps.map((fileName) => (
                <li key={fileName}>
                  <a
                    href={getCoreDumpUrl(id, fileName)}
                    className="text-sm text-primary hover:underline inline-flex items-center gap-1"
                  >
                    <Download className="h-3 w-3" />
                    {fileName}
                  </a>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
