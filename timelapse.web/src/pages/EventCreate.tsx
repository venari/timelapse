import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation } from '@tanstack/react-query';
import { api } from '@/api/client';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { Loader2, Flag, FlagOff } from 'lucide-react';
import { ImageScrubber } from '@/components/ImageScrubber';
import { EventTypeSelector } from '@/components/EventTypeSelector';

export function EventCreate() {
  const { imageId } = useParams<{ imageId: string }>();
  const navigate = useNavigate();

  const { data: image, isLoading: imageLoading, error: imageError } = useQuery({
    queryKey: ['image', imageId],
    queryFn: () => api.getImage(Number(imageId)),
    enabled: !!imageId,
  });

  const { data: device } = useQuery({
    queryKey: ['device', image?.deviceId],
    queryFn: () => api.getDevice(image!.deviceId),
    enabled: !!image,
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
    if (image && !currentImage) {
      setCurrentImage({ id: image.id, timestamp: image.timestamp });
      setStartTime(image.timestamp);
      setEndTime(image.timestamp);
    }
  }, [image, currentImage]);

  const mutation = useMutation({
    mutationFn: () =>
      api.createEvent({
        imageId: Number(imageId),
        startTime: startTime!,
        endTime: endTime!,
        description,
        eventTypeIds: selectedEventTypeIds,
      }),
    onSuccess: (event) => navigate(`/event/${event.id}`),
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

  if (imageLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    );
  }

  if (imageError || !currentImage) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-destructive mb-2">Error Loading Image</h2>
          <p className="text-muted-foreground">
            {imageError instanceof Error ? imageError.message : 'An unknown error occurred'}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-8 px-4 max-w-3xl">
      <div className="mb-6">
        <h1 className="text-4xl font-bold mb-2">Create Event</h1>
        <p className="text-muted-foreground">{device?.name}</p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Browse frames</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <ImageScrubber deviceId={image!.deviceId} currentImage={currentImage} onNavigate={setCurrentImage} />
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
              (mutation.error instanceof Error ? mutation.error.message : 'Failed to create event')}
          </p>
        )}

        <Button type="submit" size="lg" disabled={mutation.isPending}>
          {mutation.isPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
          Create Event
        </Button>
      </form>
    </div>
  );
}
