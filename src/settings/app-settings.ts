import type { ThemeMode } from "../types.ts";

/** localStorage-backed replacement for Services/AppSettingsService.cs (`%AppData%\PixPen\settings.json`). */

export interface PressureCurve {
  min: number;
  max: number;
  gamma: number;
}

export interface AppSettings {
  undoMemoryLimitMb: number;
  maxLayers: number;
  defaultDpi: number;
  defaultWidth: number;
  defaultHeight: number;
  pressureCurve: PressureCurve;
  theme: ThemeMode;
  defaultPaletteName: string | null;
}

const STORAGE_KEY = "pixpen.settings";

export function defaultAppSettings(): AppSettings {
  return {
    undoMemoryLimitMb: 512,
    maxLayers: 100,
    defaultDpi: 96,
    defaultWidth: 1000,
    defaultHeight: 1000,
    pressureCurve: { min: 0, max: 1, gamma: 1 },
    theme: "System",
    defaultPaletteName: null,
  };
}

export function loadAppSettings(): AppSettings {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return defaultAppSettings();
    return { ...defaultAppSettings(), ...JSON.parse(raw) };
  } catch {
    return defaultAppSettings();
  }
}

export function saveAppSettings(settings: AppSettings) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
}
