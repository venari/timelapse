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
  latitude: number;
  longitude: number;
  altitude?: number;
  deviceId: number;
}

export interface Event {
  id: number;
  name: string;
  description?: string;
  startTime: string;
  endTime: string;
  deviceId: number;
}
