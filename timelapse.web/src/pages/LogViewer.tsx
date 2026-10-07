import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { getLogUrl, getCoreDumpUrl } from '@/lib/logUtils';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Loader2, Download, RefreshCw, FileWarning } from 'lucide-react';

const WORD_WRAP_STORAGE_KEY = 'logViewer.wordWrap';

// A per-browser display preference, not device data - localStorage rather than component state
// alone so it survives a refresh/revisit. Guarded against private-browsing/blocked storage.
function loadWordWrapPreference(): boolean {
  try {
    return localStorage.getItem(WORD_WRAP_STORAGE_KEY) !== 'off';
  } catch {
    return true;
  }
}

// Splits `line` on (case-insensitive) occurrences of `needle`, wrapping matches in <mark>.
// Returns plain phrasing content (strings/<mark>) so it stays valid nested inside the <pre>.
function highlightMatches(line: string, needle: string) {
  if (!needle) {
    return line;
  }
  const lower = line.toLowerCase();
  const parts: ReactNode[] = [];
  let start = 0;
  let idx = lower.indexOf(needle);
  if (idx === -1) {
    return line;
  }
  let key = 0;
  while (idx !== -1) {
    if (idx > start) {
      parts.push(line.slice(start, idx));
    }
    parts.push(
      <mark key={key++} className="bg-yellow-300 text-black rounded-sm">
        {line.slice(idx, idx + needle.length)}
      </mark>
    );
    start = idx + needle.length;
    idx = lower.indexOf(needle, start);
  }
  if (start < line.length) {
    parts.push(line.slice(start));
  }
  return parts;
}

export function LogViewer() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const id = Number(deviceId);

  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const [filter, setFilter] = useState('');
  const [wordWrap, setWordWrap] = useState(loadWordWrapPreference);

  useEffect(() => {
    try {
      localStorage.setItem(WORD_WRAP_STORAGE_KEY, wordWrap ? 'on' : 'off');
    } catch {
      // Private browsing / blocked storage - the toggle still works for this session, it just
      // won't be remembered next visit.
    }
  }, [wordWrap]);

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
              <div className="flex items-center gap-2">
                <Switch id="word-wrap" checked={wordWrap} onCheckedChange={setWordWrap} />
                <Label htmlFor="word-wrap" className="cursor-pointer">
                  Wrap
                </Label>
              </div>
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
              <pre
                className={`bg-muted rounded-md p-4 text-xs leading-relaxed overflow-auto max-h-[65vh] ${
                  wordWrap ? 'whitespace-pre-wrap break-all' : 'whitespace-pre'
                }`}
              >
                {filteredLines.length > 0
                  ? filteredLines.map((line, i) => (
                      <span key={i}>
                        {highlightMatches(line, filter.trim().toLowerCase())}
                        {i < filteredLines.length - 1 ? '\n' : ''}
                      </span>
                    ))
                  : 'No lines match the filter.'}
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
