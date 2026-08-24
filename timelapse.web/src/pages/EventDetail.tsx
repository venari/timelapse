import { useEffect, useRef, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api/client';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Loader2, Play, Pause, Pencil, Trash2 } from 'lucide-react';
import { format, intervalToDuration, formatDuration } from 'date-fns';
import { getImageUrl } from '@/lib/imageUtils';

export function EventDetail() {
  const { eventId } = useParams<{ eventId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: event, isLoading, error } = useQuery({
    queryKey: ['event', eventId],
    queryFn: () => api.getEvent(Number(eventId)),
    enabled: !!eventId,
  });

  const [frameIndex, setFrameIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [loadedFrames, setLoadedFrames] = useState<Set<number>>(new Set());
  const [preloadProgress, setPreloadProgress] = useState(0);
  const intervalRef = useRef<number | null>(null);

  // Preload every frame in the event's window before allowing playback - swapping
  // <img src> on a 200ms timer without this meant slower-loading frames just
  // rendered broken/blank mid-animation.
  useEffect(() => {
    if (!event || event.eventImages.length === 0) return;

    setLoadedFrames(new Set());
    setPreloadProgress(0);
    setFrameIndex(0);
    setIsPlaying(false);

    let loadedCount = 0;
    const total = event.eventImages.length;

    event.eventImages.forEach((frame, index) => {
      const img = new Image();
      img.src = getImageUrl(frame.id);

      const onDone = () => {
        loadedCount++;
        setLoadedFrames((prev) => new Set(prev).add(index));
        setPreloadProgress((loadedCount / total) * 100);
        if (loadedCount === total) {
          setIsPlaying(true);
        }
      };

      img.onload = onDone;
      img.onerror = onDone;
    });
  }, [event]);

  useEffect(() => {
    if (isPlaying && event && event.eventImages.length > 0) {
      intervalRef.current = window.setInterval(() => {
        setFrameIndex((prev) => {
          const next = prev + 1 >= event.eventImages.length ? 0 : prev + 1;
          // Skip over any frame that failed to preload rather than showing it broken.
          return loadedFrames.has(next) ? next : prev;
        });
      }, 200);
    }
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [isPlaying, event, loadedFrames]);

  const deleteMutation = useMutation({
    mutationFn: () => api.deleteEvent(Number(eventId)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['events'] });
      navigate('/event');
    },
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    );
  }

  if (error || !event) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-destructive mb-2">Error Loading Event</h2>
          <p className="text-muted-foreground">
            {error instanceof Error ? error.message : 'An unknown error occurred'}
          </p>
        </div>
      </div>
    );
  }

  const duration = formatDuration(
    intervalToDuration({ start: new Date(event.startTime), end: new Date(event.endTime) })
  ) || 'less than a minute';
  const currentFrame = event.eventImages[frameIndex];

  return (
    <div className="container mx-auto py-8 px-4 max-w-4xl">
      <div className="mb-6 flex items-start justify-between">
        <div>
          <h1 className="text-4xl font-bold mb-2">{event.device.name}</h1>
          <div className="flex gap-2 flex-wrap">
            {event.eventTypes.map((et) => (
              <Badge key={et.id} variant="secondary">
                {et.name}
              </Badge>
            ))}
          </div>
        </div>
        <div className="flex gap-2">
          <Link to={`/device/${event.device.id}/edit`}>
            <Button variant="outline" size="icon" title="Edit device">
              <Pencil className="h-4 w-4" />
            </Button>
          </Link>
          <Link to={`/event/${event.id}/edit`}>
            <Button variant="outline">
              <Pencil className="h-4 w-4 mr-2" />
              Edit Event
            </Button>
          </Link>
          <Button
            variant="destructive"
            onClick={() => {
              if (confirm('Are you sure you wish to delete this Event?')) {
                deleteMutation.mutate();
              }
            }}
            disabled={deleteMutation.isPending}
          >
            {deleteMutation.isPending ? (
              <Loader2 className="h-4 w-4 mr-2 animate-spin" />
            ) : (
              <Trash2 className="h-4 w-4 mr-2" />
            )}
            Delete
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <CardTitle>Details</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div>
              <span className="text-muted-foreground">Start: </span>
              {format(new Date(event.startTime), 'PPpp')}
            </div>
            <div>
              <span className="text-muted-foreground">End: </span>
              {format(new Date(event.endTime), 'PPpp')}
            </div>
            <div>
              <span className="text-muted-foreground">Duration: </span>
              {duration}
            </div>
            <div>
              <span className="text-muted-foreground">Description: </span>
              {event.description || '—'}
            </div>
            <div>
              <span className="text-muted-foreground">Created by </span>
              {event.createdBy} on {format(new Date(event.createdDate), 'PPp')}
            </div>
            {event.lastEditedBy && (
              <div>
                <span className="text-muted-foreground">Last edited by </span>
                {event.lastEditedBy} on {format(new Date(event.lastEditedDate), 'PPp')}
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Playback</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {currentFrame ? (
              <>
                <div className="relative aspect-video bg-black rounded-lg overflow-hidden">
                  {loadedFrames.has(frameIndex) ? (
                    <img
                      src={getImageUrl(currentFrame.id)}
                      alt={`Frame at ${format(new Date(currentFrame.timestamp), 'PPpp')}`}
                      className="w-full h-full object-contain"
                    />
                  ) : (
                    <div className="w-full h-full flex items-center justify-center">
                      <Loader2 className="h-8 w-8 animate-spin text-white" />
                    </div>
                  )}
                  <div className="absolute bottom-2 left-2 bg-black/70 text-white px-2 py-1 rounded text-xs">
                    {format(new Date(currentFrame.timestamp), 'PPpp')}
                  </div>
                </div>

                {preloadProgress < 100 && (
                  <div className="space-y-1">
                    <div className="flex items-center justify-between text-xs text-muted-foreground">
                      <span>Loading frames...</span>
                      <span>{Math.round(preloadProgress)}%</span>
                    </div>
                    <div className="w-full bg-secondary rounded-full h-1.5">
                      <div
                        className="bg-primary rounded-full h-1.5 transition-all duration-300"
                        style={{ width: `${preloadProgress}%` }}
                      />
                    </div>
                  </div>
                )}

                <div className="flex items-center justify-center gap-2">
                  <Button
                    type="button"
                    variant="outline"
                    size="icon"
                    disabled={preloadProgress < 100}
                    onClick={() => setIsPlaying((p) => !p)}
                  >
                    {isPlaying ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
                  </Button>
                  <span className="text-xs text-muted-foreground">
                    {frameIndex + 1} / {event.eventImages.length}
                  </span>
                </div>
              </>
            ) : (
              <p className="text-sm text-muted-foreground">No images available for this event window</p>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="mt-6">
        <Link to="/event" className="text-sm text-primary hover:underline">
          &larr; Back to Events
        </Link>
      </div>
    </div>
  );
}
