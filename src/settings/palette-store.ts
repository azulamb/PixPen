import { createDefaultPalette } from "../types.ts";

/** localStorage-backed replacement for Services/PaletteService.cs (`%AppData%\PixPen\palettes\*.json`). */

export interface PalettePreset {
  name: string;
  colors: string[];
  foregroundColor: string;
  backgroundColor: string;
}

const STORAGE_KEY = "pixpen.palettes";
export const BUILTIN_PALETTE_NAME = "基本パレット";

function readAll(): Record<string, PalettePreset> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

function writeAll(map: Record<string, PalettePreset>) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(map));
}

/** Built-in default is always available and never persisted, matching the original's "基本パレット". */
export function listPaletteNames(): string[] {
  return [BUILTIN_PALETTE_NAME, ...Object.keys(readAll())];
}

export function loadPalettePreset(name: string): PalettePreset {
  if (!name || name === BUILTIN_PALETTE_NAME) {
    const p = createDefaultPalette();
    return {
      name: BUILTIN_PALETTE_NAME,
      colors: p.colors,
      foregroundColor: p.foregroundColor,
      backgroundColor: p.backgroundColor,
    };
  }
  return readAll()[name] ?? loadPalettePreset(BUILTIN_PALETTE_NAME);
}

export function savePalettePreset(preset: PalettePreset) {
  if (preset.name === BUILTIN_PALETTE_NAME) return;
  const all = readAll();
  all[preset.name] = preset;
  writeAll(all);
}

export function deletePalettePreset(name: string) {
  if (name === BUILTIN_PALETTE_NAME) return;
  const all = readAll();
  delete all[name];
  writeAll(all);
}
