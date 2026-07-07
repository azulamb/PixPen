/** Integer pixel rectangle, used for dirty-region tracking (mirrors Int32Rect usage in the WPF app). */

export interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export const EMPTY_RECT: Rect = { x: 0, y: 0, width: 0, height: 0 };

export function isEmptyRect(r: Rect): boolean {
  return r.width <= 0 || r.height <= 0;
}

export function unionRect(a: Rect, b: Rect): Rect {
  if (isEmptyRect(a)) return b;
  if (isEmptyRect(b)) return a;
  const x = Math.min(a.x, b.x);
  const y = Math.min(a.y, b.y);
  const right = Math.max(a.x + a.width, b.x + b.width);
  const bottom = Math.max(a.y + a.height, b.y + b.height);
  return { x, y, width: right - x, height: bottom - y };
}

export function clampRect(r: Rect, width: number, height: number): Rect {
  const x = Math.max(0, Math.min(r.x, width));
  const y = Math.max(0, Math.min(r.y, height));
  const right = Math.max(x, Math.min(r.x + r.width, width));
  const bottom = Math.max(y, Math.min(r.y + r.height, height));
  return { x, y, width: right - x, height: bottom - y };
}

export function rectFromPoints(
  x0: number,
  y0: number,
  x1: number,
  y1: number,
  margin = 0,
): Rect {
  const minX = Math.min(x0, x1) - margin;
  const minY = Math.min(y0, y1) - margin;
  const maxX = Math.max(x0, x1) + margin;
  const maxY = Math.max(y0, y1) + margin;
  return {
    x: Math.floor(minX),
    y: Math.floor(minY),
    width: Math.ceil(maxX - minX),
    height: Math.ceil(maxY - minY),
  };
}
