import path from "path"
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ command }) => ({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  // Only applied for `vite build` (production), not `vite`/`vite preview` - those
  // serve from their own standalone dev server and redirect "/" to `base` itself,
  // which would break the normal `npm run dev` workflow. In production the built
  // app is embedded into the API's wwwroot so the same ASP.NET host can serve both
  // the old Razor Pages UI and this app (see Program.cs's MapFallbackToFile routes).
  // `base` is a fixed, absolute asset prefix distinct from the human-facing routes
  // (/dashboard, /device, /image-view, /telemetry) so built JS/CSS resolve correctly
  // no matter which route served index.html, and can't collide with the existing
  // wwwroot/assets/fontawesome content.
  base: command === "build" ? "/dist/" : "/",
  build: {
    outDir: "../timelapse.api/wwwroot/dist",
    emptyOutDir: true,
  },
}))
