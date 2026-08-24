import { useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { api } from '@/api/client';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { DeviceLocationMap } from '@/components/DeviceLocationMap';
import { Loader2 } from 'lucide-react';
import type { DeviceUpdateRequest } from '@/types';

const locationSchema = z
  .object({
    locationMoved: z.boolean(),
    description: z.string().optional().nullable(),
    latitude: z.number().optional().nullable(),
    longitude: z.number().optional().nullable(),
    heading: z.number().optional().nullable(),
    pitch: z.number().optional().nullable(),
    heightMM: z.number().optional().nullable(),
  })
  .refine((loc) => (loc.latitude == null) === (loc.longitude == null), {
    message: 'Latitude and Longitude must both be provided together',
    path: ['latitude'],
  })
  .refine((loc) => loc.latitude == null || !!loc.description, {
    message: 'Location Description is required',
    path: ['description'],
  });

const deviceEditSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  description: z.string().optional().nullable(),
  shortDescription: z.string().optional().nullable(),
  supportMode: z.boolean(),
  monitoringMode: z.boolean(),
  hibernateMode: z.boolean(),
  powerOff: z.boolean(),
  service: z.boolean(),
  wideAngle: z.boolean(),
  retired: z.boolean(),
  sleepDuringNight: z.boolean(),
  // Plain z.number() (not z.coerce.number()) so the schema's input and output types
  // match - the number inputs below already coerce via register(..., { valueAsNumber:
  // true }), and mixing that with z.coerce breaks zodResolver's generic inference.
  daytimeStartsAtH: z.number().int(),
  daytimeEndsAtH: z.number().int(),
  cameraIntervalS: z.number().int(),
  apiUrl: z.string(),
  hflip: z.boolean(),
  vflip: z.boolean(),
  geoIntervalS: z.number().int(),
  autoSyncPeriodS: z.number().int(),
  location: locationSchema,
});

type DeviceEditForm = z.infer<typeof deviceEditSchema>;

const emptyDefaults: DeviceEditForm = {
  name: '',
  description: '',
  shortDescription: '',
  supportMode: false,
  monitoringMode: false,
  hibernateMode: false,
  powerOff: false,
  service: false,
  wideAngle: false,
  retired: false,
  sleepDuringNight: false,
  daytimeStartsAtH: 7,
  daytimeEndsAtH: 17,
  cameraIntervalS: 300,
  apiUrl: '',
  hflip: false,
  vflip: false,
  geoIntervalS: 3600,
  autoSyncPeriodS: 300,
  location: {
    locationMoved: false,
    description: '',
    latitude: null,
    longitude: null,
    heading: null,
    pitch: null,
    heightMM: null,
  },
};

export function DeviceEdit() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const {
    data: device,
    isLoading: deviceLoading,
    error: deviceError,
  } = useQuery({
    queryKey: ['device', deviceId],
    queryFn: () => api.getDevice(Number(deviceId)),
    enabled: !!deviceId,
  });

  const { data: basemap } = useQuery({
    queryKey: ['basemap'],
    queryFn: api.getBasemapConfig,
  });

  const {
    register,
    control,
    handleSubmit,
    watch,
    setValue,
    reset,
    formState: { errors },
  } = useForm<DeviceEditForm>({
    resolver: zodResolver(deviceEditSchema),
    defaultValues: emptyDefaults,
  });

  useEffect(() => {
    if (!device) return;

    const currentLocation = device.deviceLocations && device.deviceLocations.length > 0
      ? [...device.deviceLocations].sort(
          (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
        )[0]
      : null;

    reset({
      name: device.name,
      description: device.description ?? '',
      shortDescription: device.shortDescription ?? '',
      supportMode: device.supportMode,
      monitoringMode: device.monitoringMode,
      hibernateMode: device.hibernateMode,
      powerOff: device.powerOff,
      service: device.service,
      wideAngle: device.wideAngle,
      retired: device.retired,
      sleepDuringNight: device.sleepDuringNight ?? emptyDefaults.sleepDuringNight,
      daytimeStartsAtH: device.daytimeStartsAtH ?? emptyDefaults.daytimeStartsAtH,
      daytimeEndsAtH: device.daytimeEndsAtH ?? emptyDefaults.daytimeEndsAtH,
      cameraIntervalS: device.cameraIntervalS ?? emptyDefaults.cameraIntervalS,
      apiUrl: device.apiUrl ?? emptyDefaults.apiUrl,
      hflip: device.hflip ?? emptyDefaults.hflip,
      vflip: device.vflip ?? emptyDefaults.vflip,
      geoIntervalS: device.geoIntervalS ?? emptyDefaults.geoIntervalS,
      autoSyncPeriodS: device.autoSyncPeriodS ?? emptyDefaults.autoSyncPeriodS,
      location: {
        locationMoved: false,
        description: currentLocation?.description ?? '',
        latitude: currentLocation?.latitude ?? null,
        longitude: currentLocation?.longitude ?? null,
        heading: currentLocation?.heading ?? null,
        pitch: currentLocation?.pitch ?? null,
        heightMM: currentLocation?.heightMM ?? null,
      },
    });
  }, [device, reset]);

  const mutation = useMutation({
    mutationFn: (payload: DeviceUpdateRequest) => api.updateDevice(Number(deviceId), payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['device', deviceId] });
      queryClient.invalidateQueries({ queryKey: ['devices'] });
      navigate('/dashboard');
    },
  });

  const onSubmit = (values: DeviceEditForm) => {
    mutation.mutate(values as DeviceUpdateRequest);
  };

  const wideAngle = watch('wideAngle');
  const latitude = watch('location.latitude') ?? null;
  const longitude = watch('location.longitude') ?? null;
  const heading = watch('location.heading') ?? null;

  if (deviceLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    );
  }

  if (deviceError) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-destructive mb-2">Error Loading Device</h2>
          <p className="text-muted-foreground">
            {deviceError instanceof Error ? deviceError.message : 'An unknown error occurred'}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-8 px-4 max-w-3xl">
      <div className="mb-6">
        <h1 className="text-4xl font-bold mb-2">Edit {device?.name}</h1>
        <p className="text-muted-foreground">Update device settings, camera configuration, and location</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Device</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label>Serial Number</Label>
              <Input value={device?.serialNumber ?? ''} disabled />
            </div>
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" {...register('name')} />
              {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
            </div>
            <div className="space-y-2">
              <Label htmlFor="shortDescription">Short Description</Label>
              <Input id="shortDescription" {...register('shortDescription')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea id="description" rows={3} {...register('description')} />
            </div>

            {(
              [
                ['supportMode', 'Support Mode'],
                ['monitoringMode', 'Monitoring Mode'],
                ['hibernateMode', 'Hibernate Mode'],
                ['powerOff', 'Power Off'],
                ['wideAngle', 'Wide Angle'],
                ['service', 'Service'],
                ['retired', 'Retired'],
              ] as const
            ).map(([field, label]) => (
              <div key={field} className="flex items-center justify-between">
                <Label htmlFor={field}>{label}</Label>
                <Controller
                  name={field}
                  control={control}
                  render={({ field: { value, onChange } }) => (
                    <Switch id={field} checked={value} onCheckedChange={onChange} />
                  )}
                />
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Camera Configuration</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center justify-between">
              <Label htmlFor="sleepDuringNight">Sleep During Night</Label>
              <Controller
                name="sleepDuringNight"
                control={control}
                render={({ field: { value, onChange } }) => (
                  <Switch id="sleepDuringNight" checked={value} onCheckedChange={onChange} />
                )}
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="daytimeStartsAtH">Daytime Starts At (h)</Label>
                <Input id="daytimeStartsAtH" type="number" {...register('daytimeStartsAtH', { valueAsNumber: true })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="daytimeEndsAtH">Daytime Ends At (h)</Label>
                <Input id="daytimeEndsAtH" type="number" {...register('daytimeEndsAtH', { valueAsNumber: true })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="cameraIntervalS">Camera Interval (s)</Label>
                <Input id="cameraIntervalS" type="number" {...register('cameraIntervalS', { valueAsNumber: true })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="geoIntervalS">Geo Interval (s)</Label>
                <Input id="geoIntervalS" type="number" {...register('geoIntervalS', { valueAsNumber: true })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="autoSyncPeriodS">Auto Sync Period (s)</Label>
                <Input id="autoSyncPeriodS" type="number" {...register('autoSyncPeriodS', { valueAsNumber: true })} />
              </div>
            </div>
            <div className="flex items-center justify-between">
              <Label htmlFor="hflip">Horizontal Flip</Label>
              <Controller
                name="hflip"
                control={control}
                render={({ field: { value, onChange } }) => (
                  <Switch id="hflip" checked={value} onCheckedChange={onChange} />
                )}
              />
            </div>
            <div className="flex items-center justify-between">
              <Label htmlFor="vflip">Vertical Flip</Label>
              <Controller
                name="vflip"
                control={control}
                render={({ field: { value, onChange } }) => (
                  <Switch id="vflip" checked={value} onCheckedChange={onChange} />
                )}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="apiUrl">API URL</Label>
              <Input id="apiUrl" {...register('apiUrl')} />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Location</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <DeviceLocationMap
              latitude={latitude}
              longitude={longitude}
              heading={heading}
              wideAngle={wideAngle}
              basemapUrl={basemap?.url ?? null}
              onLocationPick={(lat, lon) => {
                setValue('location.latitude', lat, { shouldValidate: true });
                setValue('location.longitude', lon, { shouldValidate: true });
                setValue('location.locationMoved', true);
              }}
            />

            <div className="flex items-center justify-between">
              <Label htmlFor="locationMoved">New Location</Label>
              <Controller
                name="location.locationMoved"
                control={control}
                render={({ field: { value, onChange } }) => (
                  <Switch id="locationMoved" checked={value} onCheckedChange={onChange} />
                )}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="locationDescription">Location Description</Label>
              <Input id="locationDescription" {...register('location.description')} />
              {errors.location?.description && (
                <p className="text-sm text-destructive">{errors.location.description.message}</p>
              )}
            </div>

            <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
              <div className="space-y-2">
                <Label htmlFor="latitude">Latitude</Label>
                <Input
                  id="latitude"
                  type="number"
                  step="any"
                  {...register('location.latitude', { valueAsNumber: true })}
                />
                {errors.location?.latitude && (
                  <p className="text-sm text-destructive">{errors.location.latitude.message}</p>
                )}
              </div>
              <div className="space-y-2">
                <Label htmlFor="longitude">Longitude</Label>
                <Input
                  id="longitude"
                  type="number"
                  step="any"
                  {...register('location.longitude', { valueAsNumber: true })}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="heading">Heading</Label>
                <Input id="heading" type="number" {...register('location.heading', { valueAsNumber: true })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="pitch">Pitch</Label>
                <Input id="pitch" type="number" {...register('location.pitch', { valueAsNumber: true })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="heightMM">Height (mm)</Label>
                <Input id="heightMM" type="number" {...register('location.heightMM', { valueAsNumber: true })} />
              </div>
            </div>
          </CardContent>
        </Card>

        {mutation.isError && (
          <p className="text-sm text-destructive">
            {mutation.error instanceof Error ? mutation.error.message : 'Failed to save device'}
          </p>
        )}

        <Button type="submit" size="lg" disabled={mutation.isPending}>
          {mutation.isPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
          Save
        </Button>
      </form>
    </div>
  );
}
