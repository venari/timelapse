import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Loader2 } from 'lucide-react';
import { format } from 'date-fns';
import { getImageUrl } from '@/lib/imageUtils';

const DAY_RANGES = {
  week: 7,
  month: 31,
  all: 999,
} as const;

export function EventsIndex() {
  const [range, setRange] = useState<keyof typeof DAY_RANGES>('week');

  const { data: events, isLoading, error } = useQuery({
    queryKey: ['events', range],
    queryFn: () => api.getEvents(DAY_RANGES[range]),
  });

  return (
    <div className="container mx-auto py-8 px-4">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-4xl font-bold mb-2">Events</h1>
          <p className="text-muted-foreground">Notable events captured across your devices</p>
        </div>
        <Tabs value={range} onValueChange={(v) => setRange(v as keyof typeof DAY_RANGES)}>
          <TabsList>
            <TabsTrigger value="week">Last week</TabsTrigger>
            <TabsTrigger value="month">Last month</TabsTrigger>
            <TabsTrigger value="all">All</TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center min-h-[400px]">
          <Loader2 className="h-8 w-8 animate-spin text-primary" />
        </div>
      ) : error ? (
        <div className="flex items-center justify-center min-h-[400px]">
          <div className="text-center">
            <h2 className="text-2xl font-bold text-destructive mb-2">Error Loading Events</h2>
            <p className="text-muted-foreground">
              {error instanceof Error ? error.message : 'An unknown error occurred'}
            </p>
          </div>
        </div>
      ) : events && events.length > 0 ? (
        <div className="space-y-3">
          {events.map((event) => (
            <Link key={event.id} to={`/event/${event.id}`}>
              <Card className="hover:shadow-lg transition-shadow">
                <CardContent className="py-4 flex items-center gap-4">
                  <div className="flex gap-2 shrink-0">
                    {event.startImage && (
                      <img
                        src={getImageUrl(event.startImage.id)}
                        alt="Start"
                        className="h-16 w-24 object-cover rounded bg-muted"
                      />
                    )}
                    {event.endImage && event.endImage.id !== event.startImage?.id && (
                      <img
                        src={getImageUrl(event.endImage.id)}
                        alt="End"
                        className="h-16 w-24 object-cover rounded bg-muted"
                      />
                    )}
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap mb-1">
                      <span className="font-semibold">{event.device.name}</span>
                      {event.eventTypes.map((et) => (
                        <Badge key={et.id} variant="secondary">
                          {et.name}
                        </Badge>
                      ))}
                    </div>
                    <p className="text-sm text-muted-foreground truncate">{event.description}</p>
                    <p className="text-xs text-muted-foreground mt-1">
                      {format(new Date(event.startTime), 'PPp')} &rarr; {format(new Date(event.endTime), 'PPp')} &middot; by{' '}
                      {event.createdBy}
                    </p>
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      ) : (
        <div className="text-center py-12">
          <p className="text-muted-foreground">No events found in this range</p>
        </div>
      )}
    </div>
  );
}
