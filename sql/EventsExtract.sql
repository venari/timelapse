SELECT events.id, 
start_time, end_time, 
end_time - start_time as duration,

created_date, /*created_by_user_id, */ user_created.user_name as created_by,
/*last_edited_by_user_id,*/ last_edited_date, user_edited.user_name as last_edited_by,

events.description, 
events.device_id, devices.Name as device_name, 

start_image_id, start_image.blob_uri,
end_image_id, end_image.blob_uri,

CONCAT('https://timelapse-dev.azurewebsites.net/Events/Detail/', events.id) AS event_detail_page,

event_types.description as event_type_description

FROM public.events
INNER JOIN public.devices ON events.device_id = devices.id
INNER JOIN public.event_event_type ON events.id = event_event_type.events_id
INNER JOIN public.event_types ON event_event_type.event_types_id = event_types.id
INNER JOIN public."AspNetUsers" user_created ON user_created.id = created_by_user_id
INNER JOIN public."AspNetUsers" user_edited ON user_edited.id = last_edited_by_user_id
INNER JOIN public.images start_image ON events.start_image_id = start_image.id
INNER JOIN public.images end_image ON events.end_image_id = end_image.id
-- ORDER BY events.id
ORDER BY start_time DESC




