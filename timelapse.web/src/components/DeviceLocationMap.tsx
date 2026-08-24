import { useEffect, useRef } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { LocateControl } from 'leaflet.locatecontrol';
import 'leaflet.locatecontrol/dist/L.Control.Locate.min.css';

import icon from 'leaflet/dist/images/marker-icon.png';
import icon2x from 'leaflet/dist/images/marker-icon-2x.png';
import shadow from 'leaflet/dist/images/marker-shadow.png';

// Leaflet's default marker icon paths don't survive bundling - point them at the
// bundled asset URLs instead.
L.Icon.Default.mergeOptions({
  iconRetinaUrl: icon2x,
  iconUrl: icon,
  shadowUrl: shadow,
});

const NZ_CENTER: [number, number] = [-41.288889, 174.777222];

interface DeviceLocationMapProps {
  latitude: number | null;
  longitude: number | null;
  heading: number | null;
  wideAngle: boolean;
  basemapUrl: string | null;
  onLocationPick: (lat: number, lon: number) => void;
}

// Ported 1:1 from the FOV-cone math in the old DeviceEdit.cshtml's embedded script
// (radius/fov/vertex formula kept exactly as-is, including the arbitrary small radius).
function buildFovCone(lat: number, lon: number, heading: number, wideAngle: boolean): L.LatLngExpression[] {
  const fov = wideAngle ? 120 : 75;
  const radius = 0.00001;
  const angles = [-(fov / 2), fov / 2];

  const vertices = angles.map((angle) => {
    const rad = ((angle + heading) * Math.PI) / 180;
    const dLat = radius * Math.cos(rad);
    const dLon = (radius * Math.sin(rad)) / Math.cos((lat * Math.PI) / 180);
    return [lat + (dLat * 180) / Math.PI, lon + (dLon * 180) / Math.PI] as L.LatLngExpression;
  });

  vertices.push([lat, lon]);
  return vertices;
}

export function DeviceLocationMap({
  latitude,
  longitude,
  heading,
  wideAngle,
  basemapUrl,
  onLocationPick,
}: DeviceLocationMapProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<L.Map | null>(null);
  const aerialLayerRef = useRef<L.TileLayer | null>(null);
  const markerRef = useRef<L.Marker | null>(null);
  const polygonRef = useRef<L.Polygon | null>(null);
  const onLocationPickRef = useRef(onLocationPick);
  onLocationPickRef.current = onLocationPick;

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;

    const map = L.map(containerRef.current);
    mapRef.current = map;

    const osm = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 21,
      attribution: '© OpenStreetMap',
    }).addTo(map);

    const aerial = L.tileLayer('', { maxZoom: 21 });
    aerialLayerRef.current = aerial;

    L.control.layers({ OpenStreetMap: osm, Aerial: aerial }).addTo(map);

    new LocateControl().addTo(map);

    const credits = L.control.attribution().addTo(map);
    credits.addAttribution(
      '© <a href="//www.linz.govt.nz/linz-copyright">LINZ CC BY 4.0</a> © <a href="//www.linz.govt.nz/data/linz-data/linz-basemaps/data-attribution">Imagery Basemap contributors</a>'
    );

    if (latitude != null && longitude != null) {
      map.setView([latitude, longitude], 17);
    } else {
      map.setView(NZ_CENTER, 5);
    }

    map.on('click', (e: L.LeafletMouseEvent) => {
      onLocationPickRef.current(Number(e.latlng.lat.toFixed(6)), Number(e.latlng.lng.toFixed(6)));
    });

    return () => {
      map.remove();
      mapRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Basemap URL arrives async (fetched from the API) - wire it in once available.
  useEffect(() => {
    if (basemapUrl && aerialLayerRef.current) {
      aerialLayerRef.current.setUrl(basemapUrl);
    }
  }, [basemapUrl]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    markerRef.current?.remove();
    markerRef.current = null;
    polygonRef.current?.remove();
    polygonRef.current = null;

    if (latitude == null || longitude == null) return;

    markerRef.current = L.marker([latitude, longitude]).addTo(map);

    if (heading != null) {
      const vertices = buildFovCone(latitude, longitude, heading, wideAngle);
      polygonRef.current = L.polygon(vertices, {
        color: 'red',
        fillColor: 'red',
        fillOpacity: 0.5,
      }).addTo(map);
    }

    if (map.getZoom() < 15) {
      map.setZoom(15);
    }
    map.panTo([latitude, longitude]);
  }, [latitude, longitude, heading, wideAngle]);

  return <div ref={containerRef} className="h-[400px] w-full rounded-md" />;
}
