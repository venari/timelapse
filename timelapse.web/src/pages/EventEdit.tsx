import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api/client';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { Loader2, Flag, FlagOff } from 'lucide-react';
import { ImageScrubber } from '@/components/ImageScrubber';
import { EventTypeSelector } from '@/components/EventTypeSelector';

export function EventEdit() {
  const { eventId } = useParams<{ eventId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: event, isLoading, error } = useQuery({
    queryKey: ['event', eventId],
    queryFn: () => api.getEvent(Number(eventId)),
    enabled: !!eventId,
  });

  const { data: eventTypes } = useQuery({
    queryKey: ['eventTypes'],
    queryFn: api.getEventTypes,
  });

  const [currentImage, setCurrentImage] = useState<{ id: number; timestamp: string } | null>(null);
  const [startTime, setStartTime] = useState<string | null>(null);
  const [endTime, setEndTime] = useState<string | null>(null);
  const [selectedEventTypeIds, setSelectedEventTypeIds] = useState<number[]>([]);
  const [description, setDescription] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    if (event && !currentImage) {
      const seed = event.startImage ?? event.eventImages[0] ?? null;
      if (seed) {
        setCurrentImage({ id: seed.id, timestamp: event.startTime });
      }
      setStartTime(event.startTime);
      setEndTime(event.endTime);
      setSelectedEventTypeIds(event.eventTypes.map((et) => et.id));
      setDescription(event.description ?? '');
    }
  }, [event, currentImage]);

  const mutation = useMutation({
    mutationFn: () =>
      api.updateEvent(Number(eventId), {
        startTime: startTime!,
        endTime: endTime!,
        description,
        eventTypeIds: selectedEventTypeIds,
      }),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: ['event', eventId] });
      queryClient.invalidateQueries({ queryKey: ['events'] });
      navigate(`/event/${updated.id}`);
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setValidationError(null);

    if (!startTime || !endTime || new Date(startTime) > new Date(endTime)) {
      setValidationError('End Time is not later than Start Time.');
      return;
    }
    if (selectedEventTypeIds.length === 0) {
      setValidationError('At least one Event Type must be selected.');
      return;
    }
    if (!description.trim()) {
      setValidationError('Description is required.');
      return;
    }

    mutation.mutate();
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    );
  }

  if (error || !event || !currentImage) {
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

  return (
    <div className="container mx-auto py-8 px-4 max-w-3xl">
      <div className="mb-6">
        <h1 className="text-4xl font-bold mb-2">Edit Event</h1>
        <p className="text-muted-foreground">{event.device.name}</p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Browse frames</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <ImageScrubber deviceId={event.device.id} currentImage={currentImage} onNavigate={setCurrentImage} />
            <div className="flex items-center justify-center gap-3">
              <Button type="button" variant="secondary" onClick={() => setStartTime(currentImage.timestamp)}>
                <Flag className="h-4 w-4 mr-2" />
                Mark as Start
              </Button>
              <Button type="button" variant="secondary" onClick={() => setEndTime(currentImage.timestamp)}>
                <FlagOff className="h-4 w-4 mr-2" />
                Mark as End
              </Button>
            </div>
            <div className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <span className="text-muted-foreground">Start: </span>
                {startTime ? new Date(startTime).toLocaleString() : '—'}
              </div>
              <div>
                <span className="text-muted-foreground">End: </span>
                {endTime ? new Date(endTime).toLocaleString() : '—'}
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Details</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label>Event Types</Label>
              <EventTypeSelector
                eventTypes={eventTypes ?? []}
                selectedIds={selectedEventTypeIds}
                onChange={setSelectedEventTypeIds}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea
                id="description"
                rows={3}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
            </div>
          </CardContent>
        </Card>

        {(validationError || mutation.isError) && (
          <p className="text-sm text-destructive">
            {validationError ||
              (mutation.error instanceof Error ? mutation.error.message : 'Failed to save event')}
          </p>
        )}

        <Button type="submit" size="lg" disabled={mutation.isPending}>
          {mutation.isPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
          Save
        </Button>
      </form>
    </div>
  );
}
