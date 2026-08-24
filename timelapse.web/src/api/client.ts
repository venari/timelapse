import axios from 'axios';
import type {
  Device,
  Telemetry,
  Image,
  DeviceUpdateRequest,
  EventSummary,
  EventDetail,
  EventType,
  CreateEventRequest,
  UpdateEventRequest,
} from '@/types';

// In production the app is served from the same ASP.NET host as the API (see
// vite.config.ts's build.outDir + Program.cs's fallback routes), so the correct
// default there is a same-origin relative URL - never localhost. VITE_API_BASE_URL
// still overrides this for a split-origin deployment if one is ever needed.
// The localhost fallback only applies to `npm run dev`, where the API runs on a
// separate port (see .env.development / .env.example).
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || (import.meta.env.PROD ? '' : 'http://localhost:5000');

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Every data endpoint requires an ASP.NET Identity session cookie now. Bounce
// unauthenticated requests to the React login screen rather than failing silently -
// except calls to Auth itself, where a 401 is an expected "not logged in" result
// (e.g. the Me check on load), not something that should force a navigation.
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const isAuthCall = error.config?.url?.startsWith('/api/Auth/');
    if (error.response?.status === 401 && !isAuthCall) {
      const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
      window.location.href = `/login?returnUrl=${returnUrl}`;
    }
    return Promise.reject(error);
  }
);

export const api = {
  // Auth
  async login(email: string, password: string, rememberMe: boolean): Promise<{ email: string }> {
    const response = await apiClient.post<{ email: string }>('/api/Auth/Login', {
      email,
      password,
      rememberMe,
    });
    return response.data;
  },

  async logout(): Promise<void> {
    await apiClient.post('/api/Auth/Logout');
  },

  async getCurrentUser(): Promise<{ email: string } | null> {
    try {
      const response = await apiClient.get<{ email: string }>('/api/Auth/Me');
      return response.data;
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 401) {
        return null;
      }
      throw error;
    }
  },

  // Devices
  async getDevices(): Promise<Device[]> {
    // Note: This endpoint might not exist in the current backend
    // You may need to add it or fetch from a different endpoint
    const response = await apiClient.get<Device[]>('/api/Devices');
    return response.data;
  },

  async getDevice(deviceId: number): Promise<Device> {
    const response = await apiClient.get<Device>(`/api/Devices/${deviceId}`);
    return response.data;
  },

  async updateDevice(deviceId: number, payload: DeviceUpdateRequest): Promise<Device> {
    const response = await apiClient.put<Device>(`/api/Devices/${deviceId}`, payload);
    return response.data;
  },

  async getBasemapConfig(): Promise<{ url: string }> {
    const response = await apiClient.get<{ url: string }>('/api/Config/Basemap');
    return response.data;
  },

  // Events
  async getEvents(days: number): Promise<EventSummary[]> {
    const response = await apiClient.get<EventSummary[]>(`/api/Event?days=${days}`);
    return response.data;
  },

  async getEventTypes(): Promise<EventType[]> {
    const response = await apiClient.get<EventType[]>('/api/Event/Types');
    return response.data;
  },

  async getEvent(eventId: number): Promise<EventDetail> {
    const response = await apiClient.get<EventDetail>(`/api/Event/${eventId}`);
    return response.data;
  },

  async createEvent(payload: CreateEventRequest): Promise<EventDetail> {
    const response = await apiClient.post<EventDetail>('/api/Event', payload);
    return response.data;
  },

  async updateEvent(eventId: number, payload: UpdateEventRequest): Promise<EventDetail> {
    const response = await apiClient.put<EventDetail>(`/api/Event/${eventId}`, payload);
    return response.data;
  },

  async deleteEvent(eventId: number): Promise<void> {
    await apiClient.delete(`/api/Event?eventId=${eventId}`);
  },

  // Images
  async getImage(imageId: number): Promise<Image> {
    const response = await apiClient.get<Image>(`/api/Image/${imageId}`);
    return response.data;
  },

  async getLatestImage(deviceId: number): Promise<string> {
    const response = await apiClient.get<string>(`/api/Image/Latest?deviceId=${deviceId}`);
    return response.data;
  },

  async getImageAtOrAround(
    deviceId: number,
    timestamp: string,
    forwards: boolean = true
  ): Promise<Image> {
    const response = await apiClient.get<Image>(
      `/api/Image/GetImageAtOrAround?deviceId=${deviceId}&timestamp=${timestamp}&forwards=${forwards}`
    );
    return response.data;
  },

  async getImagesBetweenDates(
    deviceId: number,
    startDate: string,
    endDate: string
  ): Promise<Image[]> {
    // Note: This endpoint might not exist in the current backend
    // You may need to add it or use a different approach
    const response = await apiClient.get<Image[]>(
      `/api/Image/GetImagesBetweenDates?deviceId=${deviceId}&startDate=${startDate}&endDate=${endDate}`
    );
    return response.data;
  },

  // Telemetry
  async getLatest24HoursTelemetry(deviceId: number): Promise<Telemetry[]> {
    const response = await apiClient.get<Telemetry[]>(
      `/api/Telemetry/GetLatest24HoursTelemetry?deviceId=${deviceId}`
    );
    return response.data;
  },

  async getTelemetryBetweenDates(
    deviceId: number,
    startDate: string,
    endDate: string
  ): Promise<Telemetry[]> {
    const response = await apiClient.get<Telemetry[]>(
      `/api/Telemetry/GetTelemetryBetweenDates?deviceId=${deviceId}&startDate=${startDate}&endDate=${endDate}`
    );
    return response.data;
  },
};

export default apiClient;
