import axios from 'axios';
import type { Device, Telemetry, Image } from '@/types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

export const api = {
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

  // Images
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
