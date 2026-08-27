export interface Device {
  id: number;
  serialNumber: string;
  name: string;
  shortDescription?: string;
  description?: string;
  supportMode: boolean;
  monitoringMode: boolean;
  retired: boolean;
  hibernateMode: boolean;
  powerOff: boolean;
  service: boolean;
  wideAngle: boolean;
  // Only populated by GET /api/Devices/{id} (single-device), not the list endpoint.
  sleepDuringNight?: boolean;
  daytimeStartsAtH?: number;
  daytimeEndsAtH?: number;
  utcOffsetMinutes?: number;
  cameraIntervalS?: number;
  apiUrl?: string;
  hflip?: boolean;
  vflip?: boolean;
  enableLongExposureAtNight?: boolean;
  longExposureXclkHz?: number;
  geoIntervalS?: number;
  autoSyncPeriodS?: number;
  latestTelemetry?: Telemetry;
  latestImage?: Image;
  deviceLocations?: DeviceLocation[];
}

export interface Telemetry {
  id: number;
  timestamp: string;
  temperatureC: number;
  batteryPercent: number;
  status?: string; // Raw JSON string from backend
  diskSpaceFree?: number | null;
  uptimeSeconds?: number;
  pendingImages?: number;
  uploadedImages?: number;
  pendingTelemetry?: number;
  uploadedTelemetry?: number;
  deviceId: number;
  // Computed properties from backend
  batteryVoltage?: number;
  batteryCurrent?: number;
  ioVoltage?: number;
  ioCurrent?: number;
  powerSwitch?: boolean;
  connectedToWirelessNetwork?: boolean;
  wirelessSSID?: string;
  connectedToInternet?: boolean;
  status_Battery?: string;
  charging?: boolean;
}

export interface Image {
  id: number;
  timestamp: string;
  blobUri: string;
  deviceId: number;
}

export interface DeviceLocation {
  id: number;
  deviceId: number;
  latitude: number;
  longitude: number;
  timestamp: string;
  heading?: number | null;
  pitch?: number | null;
  heightMM?: number | null;
  description?: string | null;
}

export interface DeviceLocationUpdateRequest {
  locationMoved: boolean;
  description?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  heading?: number | null;
  pitch?: number | null;
  heightMM?: number | null;
}

export interface DeviceUpdateRequest {
  name: string;
  description?: string | null;
  shortDescription?: string | null;
  supportMode: boolean;
  monitoringMode: boolean;
  hibernateMode: boolean;
  powerOff: boolean;
  service: boolean;
  wideAngle: boolean;
  retired: boolean;
  sleepDuringNight: boolean;
  daytimeStartsAtH: number;
  daytimeEndsAtH: number;
  utcOffsetMinutes: number;
  cameraIntervalS: number;
  apiUrl: string;
  hflip: boolean;
  vflip: boolean;
  enableLongExposureAtNight: boolean;
  longExposureXclkHz: number;
  geoIntervalS: number;
  autoSyncPeriodS: number;
  location?: DeviceLocationUpdateRequest | null;
}

export interface EventType {
  id: number;
  name: string;
  description?: string | null;
}

export interface EventSummary {
  id: number;
  startTime: string;
  endTime: string;
  description?: string | null;
  createdDate: string;
  device: { id: number; name: string; description?: string | null };
  eventTypes: EventType[];
  startImage: Image | null;
  endImage: Image | null;
  createdBy: string;
}

export interface EventDetail extends EventSummary {
  lastEditedDate: string;
  lastEditedBy: string;
  eventImages: { id: number; timestamp: string; blobUri: string }[];
}

export interface CreateEventRequest {
  imageId: number;
  startTime: string;
  endTime: string;
  description: string;
  eventTypeIds: number[];
}

export interface UpdateEventRequest {
  startTime: string;
  endTime: string;
  description: string;
  eventTypeIds: number[];
}
