import type { ColorPalette, Layer } from "../types.ts";
import type { Rect } from "../canvas/rect.ts";

export interface StrokePoint {
  x: number;
  y: number;
  pressure: number; // 0-1
  isEraser: boolean;
}

export interface Tool {
  onDown(point: StrokePoint, layer: Layer, palette: ColorPalette): Rect;
  onMove(point: StrokePoint, layer: Layer, palette: ColorPalette): Rect;
  onUp(point: StrokePoint, layer: Layer, palette: ColorPalette): Rect;
}

export const EMPTY_RECT: Rect = { x: 0, y: 0, width: 0, height: 0 };
