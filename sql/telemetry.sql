SELECT d.name, t.timestamp  at time zone 'Pacific/Auckland', t.battery_percent, t.temperature_c
FROM public.telemetry t
INNER JOIN public.devices d ON t.device_id = d.id 
ORDER BY t.timestamp DESC