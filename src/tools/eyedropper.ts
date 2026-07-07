import type { ColorPalette, Layer } from "../types.ts";
import { formatArgb } from "../color.ts";
import type { Rect } from "../canvas/rect.ts";
import type { StrokePoint, Tool } from "./types.ts";
import { EMPTY_RECT } from "./types.ts";

/** Samples the merged composite (not the active layer) and sets palette.foregroundColor, matching Tools/EyedropperTool.cs. */
export class EyedropperTool implements Tool {
  constructor(private getComposite: () => ImageData) {}

  private pick(point: StrokePoint, palette: ColorPalette): Rect {
    const image = this.getComposite();
    const x = Math.floor(point.x);
    const y = Math.floor(point.y);
    if (x < 0 || y < 0 || x >= image.width || y >= image.height) {
      return EMPTY_RECT;
    }
    const o = (y * image.width + x) * 4;
    palette.foregroundColor = formatArgb({
      r: image.data[o],
      g: image.data[o + 1],
      b: image.data[o + 2],
      a: image.data[o + 3],
    });
    return EMPTY_RECT;
  }

  onDown(point: StrokePoint, _layer: Layer, palette: ColorPalette): Rect {
    return this.pick(point, palette);
  }

  onMove(_point: StrokePoint, _layer: Layer, _palette: ColorPalette): Rect {
    return EMPTY_RECT;
  }

  onUp(_point: StrokePoint, _layer: Layer, _palette: ColorPalette): Rect {
    return EMPTY_RECT;
  }
}
