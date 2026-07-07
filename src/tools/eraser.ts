import type { ColorPalette, Layer, PenDefinition } from "../types.ts";
import type { Rect } from "../canvas/rect.ts";
import type { StrokePoint, Tool } from "./types.ts";
import { EMPTY_RECT } from "./types.ts";
import { interpolateStamp, stampAt } from "./stamp.ts";

function resolveSizeAndOpacity(
  pen: PenDefinition,
  pressure: number,
): { radius: number; opacity: number } {
  let sizeFactor = 1;
  if (pen.pressureAffectsSize) {
    sizeFactor = pen.minSizeFactor + (1 - pen.minSizeFactor) * pressure;
  }
  const opacity = pen.pressureAffectsOpacity
    ? pen.opacity * pressure
    : pen.opacity;
  return { radius: (pen.size * sizeFactor) / 2, opacity };
}

/** Same stamp/interpolate mechanics as PenTool, but reduces alpha instead of blending color. */
export class EraserTool implements Tool {
  private lastX = 0;
  private lastY = 0;

  constructor(private getPen: () => PenDefinition) {}

  onDown(point: StrokePoint, layer: Layer, _palette: ColorPalette): Rect {
    this.lastX = point.x;
    this.lastY = point.y;
    const pen = this.getPen();
    const { radius, opacity } = resolveSizeAndOpacity(pen, point.pressure);
    return stampAt(layer.image, point.x, point.y, {
      shape: pen.shape,
      radius,
      opacity,
      color: { r: 0, g: 0, b: 0, a: 0 },
      erase: true,
    });
  }

  onMove(point: StrokePoint, layer: Layer, _palette: ColorPalette): Rect {
    const pen = this.getPen();
    const { radius, opacity } = resolveSizeAndOpacity(pen, point.pressure);
    const dirty = interpolateStamp(
      layer.image,
      this.lastX,
      this.lastY,
      point.x,
      point.y,
      {
        shape: pen.shape,
        radius,
        opacity,
        color: { r: 0, g: 0, b: 0, a: 0 },
        erase: true,
      },
    );
    this.lastX = point.x;
    this.lastY = point.y;
    return dirty;
  }

  onUp(_point: StrokePoint, _layer: Layer, _palette: ColorPalette): Rect {
    return EMPTY_RECT;
  }
}
