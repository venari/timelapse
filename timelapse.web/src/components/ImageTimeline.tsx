import { useMemo, useRef, useState } from 'react';
import { format } from 'date-fns';

interface TimelineImage {
  id: number;
  timestamp: string;
}

interface NightSchedule {
  /** Local hour the device wakes and daytime capture resumes (DaytimeStartsAtH). */
  startHour: number;
  /** Local hour the device stops for the night (DaytimeEndsAtH). */
  endHour: number;
  /** Fixed offset the device uses to read those hours as local wall-clock time. */
  utcOffsetMinutes: number;
}

interface ImageTimelineProps {
  images: TimelineImage[];
  currentIndex: number;
  /** Left edge of the timeline, in epoch ms - the start of the selected window. */
  windowStart: number;
  /** Right edge of the timeline, in epoch ms - usually "now". */
  windowEnd: number;
  onSeek: (index: number) => void;
  /** When the device sleeps overnight, the schedule so those gaps read as expected. */
  night?: NightSchedule;
  /** A gap with no frames longer than this counts as missing footage. */
  gapThresholdMs?: number;
}

const MINUTE = 60 * 1000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

// How many slots the coverage bar is divided into. Each slot is "covered" if at
// least one image falls inside it, so empty slots stand out as gaps in footage.
const SLOTS = 240;

const DEFAULT_GAP_THRESHOLD_MS = 15 * MINUTE;

// Diagonal hatch used to mark "device asleep overnight" stretches.
const NIGHT_STRIPES =
  'repeating-linear-gradient(45deg, rgba(99,102,241,0.28) 0 4px, transparent 4px 8px)';
const NIGHT_STRIPES_SWATCH =
  'repeating-linear-gradient(45deg, rgba(99,102,241,0.5) 0 2px, transparent 2px 4px)';

const clamp = (v: number, min: number, max: number) => Math.min(max, Math.max(min, v));

// Night windows (device asleep) overlapping [windowStart, windowEnd], in epoch ms.
// Night runs from endHour one day to startHour the next, read in the device's
// fixed-offset local time.
function computeNightIntervals(
  windowStart: number,
  windowEnd: number,
  { startHour, endHour, utcOffsetMinutes }: NightSchedule
): Array<[number, number]> {
  if (startHour === endHour) return [];
  const offset = utcOffsetMinutes * MINUTE;
  const intervals: Array<[number, number]> = [];

  // Walk local midnights spanning the window (with a day of slack each side).
  let localMidnight = Math.floor((windowStart + offset) / DAY) * DAY - DAY;
  const localWindowEnd = windowEnd + offset;
  for (; localMidnight <= localWindowEnd + DAY; localMidnight += DAY) {
    const nightStart = localMidnight + endHour * HOUR - offset;
    const nightEnd = localMidnight + DAY + startHour * HOUR - offset;
    const a = Math.max(windowStart, nightStart);
    const b = Math.min(windowEnd, nightEnd);
    if (b > a) intervals.push([a, b]);
  }
  return intervals;
}

function overlapMs(a0: number, a1: number, intervals: Array<[number, number]>): number {
  let total = 0;
  for (const [b0, b1] of intervals) {
    total += Math.max(0, Math.min(a1, b1) - Math.max(a0, b0));
  }
  return total;
}

/**
 * A time-proportional replacement for the plain index slider: the bar spans the
 * whole selected window so gaps in footage show up as blank stretches, the
 * playhead sits where the current frame actually falls in time, overnight
 * downtime is shaded, and daytime gaps are called out and counted.
 */
export function ImageTimeline({
  images,
  currentIndex,
  windowStart,
  windowEnd,
  onSeek,
  night,
  gapThresholdMs = DEFAULT_GAP_THRESHOLD_MS,
}: ImageTimelineProps) {
  const trackRef = useRef<HTMLDivElement>(null);
  const [hoverMs, setHoverMs] = useState<number | null>(null);
  const span = Math.max(1, windowEnd - windowStart);
  const pct = (ms: number) => clamp(((ms - windowStart) / span) * 100, 0, 100);

  const times = useMemo(
    () => images.map((img) => new Date(img.timestamp).getTime()).sort((a, b) => a - b),
    [images]
  );

  const nightIntervals = useMemo(
    () => (night ? computeNightIntervals(windowStart, windowEnd, night) : []),
    [night, windowStart, windowEnd]
  );

  // Contiguous covered / empty runs, for a compact coverage bar.
  const runs = useMemo(() => {
    const covered = new Array<boolean>(SLOTS).fill(false);
    for (const t of times) {
      const frac = (t - windowStart) / span;
      if (frac < 0 || frac > 1) continue;
      covered[clamp(Math.floor(frac * SLOTS), 0, SLOTS - 1)] = true;
    }
    const out: Array<{ covered: boolean; slots: number }> = [];
    for (const c of covered) {
      const last = out[out.length - 1];
      if (last && last.covered === c) last.slots += 1;
      else out.push({ covered: c, slots: 1 });
    }
    return { out, coveredCount: covered.filter(Boolean).length };
  }, [times, windowStart, span]);

  // Real gaps, derived from actual frame spacing (not the coarse slots), split
  // into "expected" (mostly overnight) and daytime anomalies.
  const gaps = useMemo(() => {
    const edges = [windowStart, ...times.filter((t) => t >= windowStart && t <= windowEnd), windowEnd];
    const all: Array<{ start: number; end: number; expected: boolean }> = [];
    for (let i = 0; i < edges.length - 1; i++) {
      const start = edges[i];
      const end = edges[i + 1];
      if (end - start <= gapThresholdMs) continue;
      const expected = overlapMs(start, end, nightIntervals) >= (end - start) * 0.8;
      all.push({ start, end, expected });
    }
    return { all, anomalies: all.filter((g) => !g.expected) };
  }, [times, windowStart, windowEnd, gapThresholdMs, nightIntervals]);

  const nearestIndexToTime = (t: number) => {
    if (times.length === 0) return 0;
    // `times` is sorted, but it's a copy - map back to the original order by id/time.
    let best = 0;
    let bestDiff = Infinity;
    for (let i = 0; i < images.length; i++) {
      const diff = Math.abs(new Date(images[i].timestamp).getTime() - t);
      if (diff < bestDiff) {
        bestDiff = diff;
        best = i;
      }
    }
    return best;
  };

  const msAtClientX = (clientX: number) => {
    const rect = trackRef.current?.getBoundingClientRect();
    if (!rect) return null;
    const frac = clamp((clientX - rect.left) / rect.width, 0, 1);
    return windowStart + frac * span;
  };

  const seekToClientX = (clientX: number) => {
    const t = msAtClientX(clientX);
    if (t != null) onSeek(nearestIndexToTime(t));
  };

  const handlePointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    e.currentTarget.setPointerCapture(e.pointerId);
    seekToClientX(e.clientX);
  };

  const handlePointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
    setHoverMs(msAtClientX(e.clientX));
    if (e.buttons === 1) seekToClientX(e.clientX);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    const keys: Record<string, number> = {
      ArrowLeft: Math.max(0, currentIndex - 1),
      ArrowRight: Math.min(images.length - 1, currentIndex + 1),
      Home: 0,
      End: images.length - 1,
    };
    if (e.key in keys) {
      e.preventDefault();
      onSeek(keys[e.key]);
    }
  };

  const currentTime =
    images[currentIndex] != null
      ? new Date(images[currentIndex].timestamp).getTime()
      : null;
  const coveragePct = Math.round((runs.coveredCount / SLOTS) * 100);
  const anomalyCount = gaps.anomalies.length;

  return (
    <div className="px-2">
      <div
        ref={trackRef}
        role="slider"
        tabIndex={0}
        aria-label="Footage timeline"
        aria-valuemin={0}
        aria-valuemax={Math.max(0, images.length - 1)}
        aria-valuenow={currentIndex}
        aria-valuetext={
          currentTime != null ? format(new Date(currentTime), 'PPpp') : undefined
        }
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerLeave={() => setHoverMs(null)}
        onKeyDown={handleKeyDown}
        className="relative h-8 w-full cursor-pointer touch-none select-none rounded-md ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
      >
        {/* Base + coverage: filled = footage present, blank = gap */}
        <div className="absolute inset-0 flex overflow-hidden rounded-md border bg-muted">
          {runs.out.map((run, i) => (
            <div
              key={i}
              style={{ width: `${(run.slots / SLOTS) * 100}%` }}
              className={run.covered ? 'bg-primary/70' : 'bg-transparent'}
            />
          ))}
        </div>

        {/* Overnight downtime shading */}
        {nightIntervals.map(([a, b], i) => (
          <div
            key={`night-${i}`}
            className="pointer-events-none absolute inset-y-0"
            style={{
              left: `${pct(a)}%`,
              width: `${pct(b) - pct(a)}%`,
              backgroundImage: NIGHT_STRIPES,
            }}
          />
        ))}

        {/* Daytime gap callouts */}
        {gaps.anomalies.map((g, i) => (
          <div
            key={`gap-${i}`}
            className="pointer-events-none absolute inset-y-0 border-x border-destructive bg-destructive/15"
            style={{ left: `${pct(g.start)}%`, width: `${Math.max(0.4, pct(g.end) - pct(g.start))}%` }}
          />
        ))}

        {/* Hover guide + time tooltip */}
        {hoverMs != null && (
          <>
            <div
              className="pointer-events-none absolute inset-y-0 w-px bg-foreground/40"
              style={{ left: `${pct(hoverMs)}%` }}
            />
            <div
              className="pointer-events-none absolute -top-7 z-10 -translate-x-1/2 whitespace-nowrap rounded bg-foreground px-1.5 py-0.5 text-xs text-background"
              style={{ left: `${clamp(pct(hoverMs), 4, 96)}%` }}
            >
              {format(new Date(hoverMs), 'EEE d MMM, HH:mm')}
            </div>
          </>
        )}

        {/* Playhead */}
        <div
          className="pointer-events-none absolute top-0 h-full w-0.5 -translate-x-1/2 bg-foreground"
          style={{ left: `${currentTime != null ? pct(currentTime) : 0}%` }}
        >
          <div className="absolute -top-1 left-1/2 h-3 w-3 -translate-x-1/2 rounded-full border-2 border-background bg-foreground" />
        </div>
      </div>

      <div className="mt-1 flex items-center justify-between text-xs text-muted-foreground">
        <span>{format(new Date(windowStart), 'PPp')}</span>
        <span>
          {images.length > 0 ? currentIndex + 1 : 0} / {images.length}
        </span>
        <span>{format(new Date(windowEnd), 'PPp')}</span>
      </div>

      {/* Legend + gap summary */}
      <div className="mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-2 w-3 rounded-sm bg-primary/70" />
          Footage ({coveragePct}%)
        </span>
        {night && (
          <span className="flex items-center gap-1.5">
            <span
              className="inline-block h-2 w-3 rounded-sm"
              style={{ backgroundImage: NIGHT_STRIPES_SWATCH }}
            />
            Overnight (asleep)
          </span>
        )}
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-2 w-3 rounded-sm border border-destructive bg-destructive/15" />
          {anomalyCount === 0
            ? 'No unexpected gaps'
            : `${anomalyCount} unexpected gap${anomalyCount === 1 ? '' : 's'}`}
        </span>
      </div>
    </div>
  );
}
