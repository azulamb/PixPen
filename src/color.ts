/** Color conversion helpers. Storage format is always #AARRGGBB (alpha-first), matching Services/ColorHelper.cs. */

export interface Rgba {
  r: number;
  g: number;
  b: number;
  a: number;
}

export interface Hsv {
  h: number; // 0-360
  s: number; // 0-1
  v: number; // 0-1
}

export function parseArgb(argb: string): Rgba {
  const hex = argb.replace("#", "");
  return {
    a: parseInt(hex.substring(0, 2), 16),
    r: parseInt(hex.substring(2, 4), 16),
    g: parseInt(hex.substring(4, 6), 16),
    b: parseInt(hex.substring(6, 8), 16),
  };
}

function toHex2(n: number): string {
  return Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, "0")
    .toUpperCase();
}

export function formatArgb(c: Rgba): string {
  return `#${toHex2(c.a)}${toHex2(c.r)}${toHex2(c.g)}${toHex2(c.b)}`;
}

/** Accepts either 6-digit (RRGGBB, alpha=255) or 8-digit (AARRGGBB) hex, with or without '#'. */
export function parseFlexibleHex(input: string): Rgba | null {
  const hex = input.replace("#", "").trim();
  if (/^[0-9a-fA-F]{6}$/.test(hex)) {
    return {
      a: 255,
      r: parseInt(hex.substring(0, 2), 16),
      g: parseInt(hex.substring(2, 4), 16),
      b: parseInt(hex.substring(4, 6), 16),
    };
  }
  if (/^[0-9a-fA-F]{8}$/.test(hex)) {
    return parseArgb(`#${hex}`);
  }
  return null;
}

export function rgbaToCss(c: Rgba): string {
  return `rgba(${c.r}, ${c.g}, ${c.b}, ${(c.a / 255).toFixed(3)})`;
}

export function argbToCss(argb: string): string {
  return rgbaToCss(parseArgb(argb));
}

export function rgbToHsv(r: number, g: number, b: number): Hsv {
  const rn = r / 255, gn = g / 255, bn = b / 255;
  const max = Math.max(rn, gn, bn);
  const min = Math.min(rn, gn, bn);
  const delta = max - min;
  let h = 0;
  if (delta !== 0) {
    if (max === rn) h = 60 * (((gn - bn) / delta) % 6);
    else if (max === gn) h = 60 * ((bn - rn) / delta + 2);
    else h = 60 * ((rn - gn) / delta + 4);
  }
  if (h < 0) h += 360;
  const s = max === 0 ? 0 : delta / max;
  const v = max;
  return { h, s, v };
}

export function hsvToRgb(hsv: Hsv): { r: number; g: number; b: number } {
  const { h, s, v } = hsv;
  const c = v * s;
  const hh = h / 60;
  const x = c * (1 - Math.abs((hh % 2) - 1));
  let r1 = 0, g1 = 0, b1 = 0;
  if (hh >= 0 && hh < 1) [r1, g1, b1] = [c, x, 0];
  else if (hh < 2) [r1, g1, b1] = [x, c, 0];
  else if (hh < 3) [r1, g1, b1] = [0, c, x];
  else if (hh < 4) [r1, g1, b1] = [0, x, c];
  else if (hh < 5) [r1, g1, b1] = [x, 0, c];
  else [r1, g1, b1] = [c, 0, x];
  const m = v - c;
  return {
    r: (r1 + m) * 255,
    g: (g1 + m) * 255,
    b: (b1 + m) * 255,
  };
}
