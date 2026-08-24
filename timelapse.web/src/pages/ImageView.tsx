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
import { getImageUrl } from '@/lib/imageUtils';

export function ImageView() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const [currentIndex, setCurrentIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [timeRange, setTimeRange] = useState<'1h' | '24h' | '48h' | '7d'>('24h');
  const [loadedImages, setLoadedImages] = useState<Set<number>>(new Set());
  const [preloadProgress, setPreloadProgress] = useState(0);
  const playIntervalRef = useRef<number | null>(null);
  const imageRefs = useRef<Map<number, HTMLImageElement>>(new Map());
  const previousImageIdsRef = useRef<number[]>([]);

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
  } = useQuery({
    queryKey: ['images', deviceId, timeRange],
    queryFn: () => {
      // Computed fresh on every fetch (not just every render) so the
      // window actually slides forward on each background refetch below.
      const { start, end } = getTimeRange();
      return api.getImagesBetweenDates(
        Number(deviceId),
        start.toISOString(),
        end.toISOString()
      );
    },
    enabled: !!deviceId,
    refetchInterval: 30000, // Refetch every 30 seconds for new images
  });

  // Preload images when data changes - prioritize latest image first
  useEffect(() => {
    if (!images || images.length === 0) {
      setLoadedImages(new Set());
      setPreloadProgress(0);
      imageRefs.current.clear();
      previousImageIdsRef.current = [];
      return;
    }

    const previousIds = previousImageIdsRef.current;
    const currentIds = images.map((image) => image.id);
    const isBackgroundRefresh =
      previousIds.length > 0 &&
      currentIds.length >= previousIds.length &&
      previousIds.every((id, i) => id === currentIds[i]);

    const loadImage = (image: typeof images[0], index: number, onProgress: () => void) => {
      const img = new Image();
      img.src = getImageUrl(image.id);

      img.onload = () => {
        imageRefs.current.set(index, img);
        setLoadedImages((prev) => {
          const newSet = new Set(prev);
          newSet.add(index);
          return newSet;
        });
        onProgress();
      };

      img.onerror = () => {
        console.error(`Failed to load image ${image.id}`);
        onProgress();
      };
    };

    if (isBackgroundRefresh) {
      // A periodic poll returned the same images, possibly with new ones
      // appended. Don't disturb the user's playback position or re-download
      // frames that are already cached - only fetch what's new, and only
      // jump to the latest frame if they were already following it live.
      previousImageIdsRef.current = currentIds;

      if (currentIds.length === previousIds.length) {
        return;
      }

      const wasAtLatest = currentIndex === previousIds.length - 1;
      if (wasAtLatest) {
        setCurrentIndex(currentIds.length - 1);
      }

      let loadedCount = loadedImages.size;
      for (let index = previousIds.length; index < images.length; index++) {
        loadImage(images[index], index, () => {
          loadedCount++;
          setPreloadProgress((loadedCount / currentIds.length) * 100);
        });
      }
      return;
    }

    // Initial load, or the device/time range changed - reset everything
    // and jump to the latest image.
    previousImageIdsRef.current = currentIds;
    setCurrentIndex(images.length - 1);
    setLoadedImages(new Set());
    setPreloadProgress(0);
    imageRefs.current.clear();

    let loadedCount = 0;
    const latestIndex = images.length - 1;

    const onProgress = () => {
      loadedCount++;
      setPreloadProgress((loadedCount / images.length) * 100);
    };

    // Load latest image first, then load the rest
    loadImage(images[latestIndex], latestIndex, onProgress);
    images.forEach((image, index) => {
      if (index !== latestIndex) {
        loadImage(image, index, onProgress);
      }
    });
  }, [images]);

  // Auto-play functionality - only advance when next image is loaded
  useEffect(() => {
    if (isPlaying && images && images.length > 0) {
      playIntervalRef.current = window.setInterval(() => {
        setCurrentIndex((prev) => {
          const nextIndex = prev + 1;
          
          // Stop if we're at the end
          if (nextIndex >= images.length) {
            setIsPlaying(false);
            return prev;
          }
          
          // Only advance if the next image is loaded
          if (loadedImages.has(nextIndex)) {
            return nextIndex;
          }
          
          // If next image isn't loaded yet, wait
          return prev;
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
  }, [isPlaying, images, loadedImages]);

  const handlePlayPause = () => {
    if (!isPlaying) {
      // When starting playback, go to the beginning
      setCurrentIndex(0);
      setIsPlaying(true);
    } else {
      // When pausing, just stop
      setIsPlaying(false);
    }
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
        <p className="text-muted-foreground">Envirocam Image Viewer</p>
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
                {loadedImages.has(currentIndex) ? (
                  <img
                    src={currentImage ? getImageUrl(currentImage.id) : ''}
                    alt={`Image ${currentIndex + 1}`}
                    className="w-full h-full object-contain"
                  />
                ) : (
                  <div className="w-full h-full flex items-center justify-center">
                    <Loader2 className="h-8 w-8 animate-spin text-white" />
                  </div>
                )}
                <div className="absolute bottom-4 left-4 bg-black/70 text-white px-3 py-1 rounded text-sm">
                  {currentImage && format(new Date(currentImage.timestamp), 'PPpp')}
                </div>
                {!loadedImages.has(currentIndex) && (
                  <div className="absolute top-4 left-1/2 -translate-x-1/2 bg-black/70 text-white px-3 py-1 rounded text-sm">
                    Loading image...
                  </div>
                )}
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

                {/* Preload Progress */}
                {preloadProgress < 100 && (
                  <div className="space-y-2">
                    <div className="flex items-center justify-between text-sm text-muted-foreground">
                      <span>Loading images...</span>
                      <span>{Math.round(preloadProgress)}%</span>
                    </div>
                    <div className="w-full bg-secondary rounded-full h-2">
                      <div
                        className="bg-primary rounded-full h-2 transition-all duration-300"
                        style={{ width: `${preloadProgress}%` }}
                      />
                    </div>
                  </div>
                )}

                {/* Playback Controls */}
                <div className="flex flex-col items-center gap-2">
                  <div className="flex items-center gap-2">
                    <Button
                      variant="outline"
                      size="icon"
                      onClick={handlePrevious}
                      disabled={currentIndex === 0}
                    >
                      <ChevronLeft className="h-4 w-4" />
                    </Button>
                    <Button 
                      onClick={handlePlayPause} 
                      size="lg"
                      disabled={preloadProgress < 100}
                    >
                      {isPlaying ? (
                        <>
                          <Pause className="h-5 w-5 mr-2" />
                          Pause
                        </>
                      ) : (
                        <>
                          <Play className="h-5 w-5 mr-2" />
                          {preloadProgress < 100 ? 'Loading...' : 'Play'}
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
                  {preloadProgress === 100 && (
                    <p className="text-xs text-muted-foreground">
                      {loadedImages.size} images ready for playback
                    </p>
                  )}
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
