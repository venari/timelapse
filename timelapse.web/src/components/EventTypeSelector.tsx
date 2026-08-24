import { Button } from '@/components/ui/button';
import type { EventType } from '@/types';

interface EventTypeSelectorProps {
  eventTypes: EventType[];
  selectedIds: number[];
  onChange: (ids: number[]) => void;
}

export function EventTypeSelector({ eventTypes, selectedIds, onChange }: EventTypeSelectorProps) {
  const toggle = (id: number) => {
    onChange(selectedIds.includes(id) ? selectedIds.filter((i) => i !== id) : [...selectedIds, id]);
  };

  return (
    <div className="flex flex-wrap gap-2">
      {eventTypes.map((eventType) => (
        <Button
          key={eventType.id}
          type="button"
          size="sm"
          variant={selectedIds.includes(eventType.id) ? 'default' : 'outline'}
          onClick={() => toggle(eventType.id)}
        >
          {eventType.name}
        </Button>
      ))}
    </div>
  );
}
