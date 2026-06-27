import { useState, useEffect, useRef } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Slider } from '@/components/ui/slider';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Play, Pause, Loader2, ChevronLeft, ChevronRight } from 'lucide-react';
import { format, subHours, subDays } from 'date-fns';

export function ImageView() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const [currentIndex, setCurrentIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [timeRange, setTimeRange] = useState<'1h' | '24h' | '48h' | '7d'>('24h');
  const playIntervalRef = useRef<number | null>(null);

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
    data: images,
    isLoading: imagesLoading,
    error: imagesError,
    refetch,
  } = useQuery({
    queryKey: ['images', deviceId, timeRange],
    queryFn: () =>
      api.getImagesBetweenDates(
        Number(deviceId),
        start.toISOString(),
        end.toISOString()
      ),
    enabled: !!deviceId,
    refetchInterval: 30000, // Refetch every 30 seconds for new images
  });

  // Auto-play functionality
  useEffect(() => {
    if (isPlaying && images && images.length > 0) {
      playIntervalRef.current = window.setInterval(() => {
        setCurrentIndex((prev) => {
          if (prev >= images.length - 1) {
            setIsPlaying(false);
            return prev;
          }
          return prev + 1;
        });
      }, 50); // 50ms delay between frames (same as original)
    } else if (playIntervalRef.current) {
      clearInterval(playIntervalRef.current);
      playIntervalRef.current = null;
    }

    return () => {
      if (playIntervalRef.current) {
        clearInterval(playIntervalRef.current);
      }
    };
  }, [isPlaying, images]);

  const handlePlayPause = () => {
    setIsPlaying(!isPlaying);
  };

  const handlePrevious = () => {
    setCurrentIndex((prev) => Math.max(0, prev - 1));
  };

  const handleNext = () => {
    setCurrentIndex((prev) => Math.min(images ? images.length - 1 : 0, prev + 1));
  };

  const handleSliderChange = (value: number[]) => {
    setCurrentIndex(value[0]);
    setIsPlaying(false);
  };

  const handleTimeRangeChange = (value: string) => {
    setTimeRange(value as '1h' | '24h' | '48h' | '7d');
    setCurrentIndex(0);
    setIsPlaying(false);
  };

  if (deviceLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    );
  }

  if (deviceError || imagesError) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-destructive mb-2">Error Loading Images</h2>
          <p className="text-muted-foreground">
            {deviceError instanceof Error ? deviceError.message : imagesError instanceof Error ? imagesError.message : 'An unknown error occurred'}
          </p>
        </div>
      </div>
    );
  }

  const currentImage = images?.[currentIndex];

  return (
    <div className="container mx-auto py-8 px-4">
      <div className="mb-6">
        <h1 className="text-4xl font-bold mb-2">
          {device?.name || 'Loading...'}
        </h1>
        <p className="text-muted-foreground">Timelapse Image Viewer</p>
      </div>

      <Card>
        <CardHeader>
          <div className="flex justify-between items-center">
            <CardTitle>Images</CardTitle>
            <Tabs value={timeRange} onValueChange={handleTimeRangeChange}>
              <TabsList>
                <TabsTrigger value="1h">1 Hour</TabsTrigger>
                <TabsTrigger value="24h">24 Hours</TabsTrigger>
                <TabsTrigger value="48h">48 Hours</TabsTrigger>
                <TabsTrigger value="7d">7 Days</TabsTrigger>
              </TabsList>
            </Tabs>
          </div>
        </CardHeader>
        <CardContent>
          {imagesLoading ? (
            <div className="flex items-center justify-center h-[600px]">
              <Loader2 className="h-8 w-8 animate-spin text-primary" />
            </div>
          ) : images && images.length > 0 ? (
            <div className="space-y-4">
              {/* Image Display */}
              <div className="relative aspect-video bg-black rounded-lg overflow-hidden">
                <img
                  src={currentImage?.blobUri}
                  alt={`Image ${currentIndex + 1}`}
                  className="w-full h-full object-contain"
                />
                <div className="absolute bottom-4 left-4 bg-black/70 text-white px-3 py-1 rounded text-sm">
                  {currentImage && format(new Date(currentImage.timestamp), 'PPpp')}
                </div>
              </div>

              {/* Controls */}
              <div className="space-y-4">
                {/* Slider */}
                <div className="px-2">
                  <Slider
                    value={[currentIndex]}
                    onValueChange={handleSliderChange}
                    max={images.length - 1}
                    step={1}
                    className="w-full"
                  />
                  <div className="flex justify-between text-xs text-muted-foreground mt-1">
                    <span>{format(new Date(images[0].timestamp), 'PPp')}</span>
                    <span>
                      {currentIndex + 1} / {images.length}
                    </span>
                    <span>{format(new Date(images[images.length - 1].timestamp), 'PPp')}</span>
                  </div>
                </div>

                {/* Playback Controls */}
                <div className="flex justify-center items-center gap-2">
                  <Button
                    variant="outline"
                    size="icon"
                    onClick={handlePrevious}
                    disabled={currentIndex === 0}
                  >
                    <ChevronLeft className="h-4 w-4" />
                  </Button>
                  <Button onClick={handlePlayPause} size="lg">
                    {isPlaying ? (
                      <>
                        <Pause className="h-5 w-5 mr-2" />
                        Pause
                      </>
                    ) : (
                      <>
                        <Play className="h-5 w-5 mr-2" />
                        Play
                      </>
                    )}
                  </Button>
                  <Button
                    variant="outline"
                    size="icon"
                    onClick={handleNext}
                    disabled={currentIndex === images.length - 1}
                  >
                    <ChevronRight className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </div>
          ) : (
            <div className="flex items-center justify-center h-[600px]">
              <p className="text-muted-foreground">No images available for this time range</p>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
