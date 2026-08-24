import { useState } from 'react';
import { format } from 'date-fns';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Loader2, Rewind, FastForward, SkipBack, SkipForward } from 'lucide-react';
import { api } from '@/api/client';
import { getImageUrl } from '@/lib/imageUtils';

interface ScrubberImage {
  id: number;
  timestamp: string;
}

interface ImageScrubberProps {
  deviceId: number;
  currentImage: ScrubberImage;
  onNavigate: (image: ScrubberImage) => void;
}

// Ports the "browse the device's timelapse frame by frame" behavior from the old
// Events/Create.cshtml and Events/Edit.cshtml pages, which both use this same
// back/forward-by-minutes pattern against GetImageAtOrAround.
export function ImageScrubber({ deviceId, currentImage, onNavigate }: ImageScrubberProps) {
  const [isLoading, setIsLoading] = useState(false);

  const navigate = async (minutes: number) => {
    setIsLoading(true);
    try {
      const newTimestamp = new Date(new Date(currentImage.timestamp).getTime() + minutes * 60 * 1000);
      const image = await api.getImageAtOrAround(deviceId, newTimestamp.toISOString(), minutes > 0);
      onNavigate(image);
    } catch {
      // No image found in that direction - just stay put.
    } finally {
      setIsLoading(false);
    }
  };

  const jumpToDateTime = async (localDateTimeValue: string) => {
    if (!localDateTimeValue) return;
    setIsLoading(true);
    try {
      const target = new Date(localDateTimeValue);
      const image = await api.getImageAtOrAround(deviceId, target.toISOString(), true);
      onNavigate(image);
    } catch {
      // No image found at or after that time - just stay put.
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="space-y-3">
      <div className="relative aspect-video bg-black rounded-lg overflow-hidden">
        <img
          src={getImageUrl(currentImage.id)}
          alt={`Frame at ${format(new Date(currentImage.timestamp), 'PPpp')}`}
          className="w-full h-full object-contain"
        />
        {isLoading && (
          <div className="absolute inset-0 flex items-center justify-center bg-black/40">
            <Loader2 className="h-6 w-6 animate-spin text-white" />
          </div>
        )}
        <div className="absolute bottom-2 left-2 bg-black/70 text-white px-2 py-1 rounded text-xs">
          {format(new Date(currentImage.timestamp), 'PPpp')}
        </div>
      </div>
      <div className="flex items-center justify-center gap-1">
        <Button type="button" variant="outline" size="icon" disabled={isLoading} onClick={() => navigate(-60)} title="Back 1 hour">
          <Rewind className="h-4 w-4" />
        </Button>
        <Button type="button" variant="outline" size="icon" disabled={isLoading} onClick={() => navigate(-10)} title="Back 10 minutes">
          <SkipBack className="h-4 w-4" />
        </Button>
        <Button type="button" variant="outline" size="icon" disabled={isLoading} onClick={() => navigate(-1)} title="Back 1 minute">
          <SkipBack className="h-3 w-3" />
        </Button>
        <Button type="button" variant="outline" size="icon" disabled={isLoading} onClick={() => navigate(1)} title="Forward 1 minute">
          <SkipForward className="h-3 w-3" />
        </Button>
        <Button type="button" variant="outline" size="icon" disabled={isLoading} onClick={() => navigate(10)} title="Forward 10 minutes">
          <SkipForward className="h-4 w-4" />
        </Button>
        <Button type="button" variant="outline" size="icon" disabled={isLoading} onClick={() => navigate(60)} title="Forward 1 hour">
          <FastForward className="h-4 w-4" />
        </Button>
      </div>
      <div className="flex items-center justify-center gap-2">
        <Label htmlFor="jumpToDateTime" className="text-sm font-normal text-muted-foreground">
          Jump to:
        </Label>
        <Input
          id="jumpToDateTime"
          type="datetime-local"
          step={1}
          className="w-auto"
          disabled={isLoading}
          value={format(new Date(currentImage.timestamp), "yyyy-MM-dd'T'HH:mm:ss")}
          onChange={(e) => jumpToDateTime(e.target.value)}
        />
      </div>
    </div>
  );
}
