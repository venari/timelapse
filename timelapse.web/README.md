# Timelapse Camera Dashboard - React Front-End

A modern React front-end for the Timelapse Camera monitoring system, built with TypeScript, Vite, and Shadcn/UI.

## Features

- **Dashboard**: View all your timelapse cameras with their latest images and telemetry
- **Image Viewer**: Browse timelapse images with play/pause controls and time range selection
- **Telemetry Graphs**: Visualize battery level, temperature, disk space, and other metrics over time
- **Real-time Updates**: Automatic polling to display new images and telemetry without refresh

## Tech Stack

- **React 18** - UI library
- **TypeScript** - Type safety
- **Vite** - Fast build tool
- **Shadcn/UI** - Beautiful UI components
- **Tailwind CSS** - Utility-first CSS
- **TanStack Query** - Data fetching and caching
- **React Router** - Client-side routing
- **Recharts** - Data visualization
- **Axios** - HTTP client

## Getting Started

### Prerequisites

- Node.js 18+ and npm
- The ASP.NET Core backend running (timelapse.api)

### Installation

1. Install dependencies:
```bash
npm install
```

2. Configure the API URL:

Edit `.env` file and set the backend URL:
```env
VITE_API_BASE_URL=http://localhost:5000
```

(The default port for the ASP.NET Core backend is 5000)

3. Start the development server:
```bash
npm run dev
```

The app will be available at `http://localhost:5173`

### Building for Production

```bash
npm run build
```

The built files will be in the `dist` directory.

### Preview Production Build

```bash
npm run preview
```

## Project Structure

```
timelapse.web/
├── src/
│   ├── api/          # API client and endpoints
│   ├── components/   # Reusable React components
│   │   └── ui/       # Shadcn/UI components
│   ├── lib/          # Utility functions
│   ├── pages/        # Page components
│   │   ├── Dashboard.tsx
│   │   ├── ImageView.tsx
│   │   └── TelemetryGraph.tsx
│   ├── types/        # TypeScript type definitions
│   ├── App.tsx       # Main app component with routing
│   ├── main.tsx      # Entry point
│   └── index.css     # Global styles
├── components.json   # Shadcn/UI configuration
├── tailwind.config.js
├── tsconfig.json
└── vite.config.ts
```

## Pages

### Dashboard (`/`)
- Lists all devices with their latest images and telemetry
- Shows device status badges (Support Mode, Monitoring, Service, etc.)
- Displays battery level, temperature, and disk space
- Click on device name to view details
- Click on image to view timelapse
- Click "View detailed charts" to see telemetry graphs

### Image Viewer (`/image-view/:deviceId`)
- Browse timelapse images with a slider
- Play/pause timelapse animation (50ms between frames)
- Select time range: 1 hour, 24 hours, 48 hours, or 7 days
- Navigate with previous/next buttons
- Displays timestamp for each image

### Telemetry Graphs (`/telemetry/:deviceId`)
- Interactive charts for battery, temperature, disk space, voltage, and current
- Select time range: 1 hour, 24 hours, 48 hours, or 7 days
- Hover over charts to see exact values
- Automatic updates every 30 seconds

## Configuration

### Environment Variables

- `VITE_API_BASE_URL` - Backend API base URL (default: `http://localhost:5000`)

### Polling Intervals

Real-time updates are configured in the React Query queries:
- Dashboard devices: 30 seconds
- Image viewer: 30 seconds
- Telemetry graphs: 30 seconds

To change these intervals, edit the `refetchInterval` option in the respective page components.

## Backend Requirements

The front-end expects the following API endpoints:

- `GET /api/Devices` - List all devices
- `GET /api/Devices/{id}` - Get device by ID
- `GET /api/Image/Latest?deviceId={id}` - Get latest image
- `GET /api/Image/GetImagesBetweenDates?deviceId={id}&startDate={date}&endDate={date}` - Get images in range
- `GET /api/Telemetry/GetLatest24HoursTelemetry?deviceId={id}` - Get last 24 hours telemetry
- `GET /api/Telemetry/GetTelemetryBetweenDates?deviceId={id}&startDate={date}&endDate={date}` - Get telemetry in range

CORS must be enabled in the backend for the development server (http://localhost:5173).

## Customization

### Adding New Components

Use the Shadcn/UI CLI to add new components:

```bash
npx shadcn@latest add [component-name]
```

Available components: https://ui.shadcn.com/docs/components

### Styling

The app uses Tailwind CSS. Edit `tailwind.config.js` to customize colors, spacing, etc.

CSS variables for Shadcn/UI are defined in `src/index.css`.

## Development Tips

- Hot Module Replacement (HMR) is enabled - changes will reflect immediately
- TypeScript errors will show in the browser during development
- Use React DevTools browser extension for debugging
- Use TanStack Query DevTools (already included) to inspect query cache

## Troubleshooting

### CORS Errors

Make sure the backend has CORS enabled for `http://localhost:5173`. Check the `Program.cs` file in the backend.

### Images Not Loading

Check that:
1. The backend is running
2. Azure Blob Storage is configured correctly
3. SAS tokens are being generated properly
4. CORS is configured on the Azure Storage account

### Data Not Updating

Check the browser console for API errors. Verify the API endpoints are returning data correctly using the Swagger UI at `http://localhost:5000/swagger`.

## License

[Your License Here]
