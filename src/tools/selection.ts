import type { ColorPalette, Layer, SelectionMask } from "../types.ts";
import type { Rect } from "../canvas/rect.ts";
import { clampRect, unionRect } from "../canvas/rect.ts";
import type { StrokePoint, Tool } from "./types.ts";
import { blendPixelOver } from "./stamp.ts";

export function emptySelection(): SelectionMask {
  return { hasSelection: false, x: 0, y: 0, width: 0, height: 0 };
}

export function isPointInSelection(
  sel: SelectionMask,
  x: number,
  y: number,
): boolean {
  return sel.hasSelection && x >= sel.x && y >= sel.y &&
    x < sel.x + sel.width && y < sel.y + sel.height;
}

/** Rectangular marquee only, matching Tools/SelectionTool.cs. Reports the union of old+new rect as "dirty" so the caller can redraw the marching-ants overlay. */
export class SelectionTool implements Tool {
  private startX = 0;
  private startY = 0;

  constructor(
    private getSelection: () => SelectionMask,
    private setSelection: (s: SelectionMask) => void,
  ) {}

  onDown(point: StrokePoint, layer: Layer, _palette: ColorPalette): Rect {
    const prev = this.getSelection();
    this.startX = Math.round(point.x);
    this.startY = Math.round(point.y);
    const next = clampRect(
      { x: this.startX, y: this.startY, width: 0, height: 0 },
      layer.image.width,
      layer.image.height,
    );
    this.setSelection({ hasSelection: false, ...next });
    return unionRect(prevRect(prev), next);
  }

  onMove(point: StrokePoint, layer: Layer, _palette: ColorPalette): Rect {
    const prev = this.getSelection();
    const x = Math.round(point.x);
    const y = Math.round(point.y);
    const next = clampRect(
      {
        x: Math.min(this.startX, x),
        y: Math.min(this.startY, y),
        width: Math.abs(x - this.startX),
        height: Math.abs(y - this.startY),
      },
      layer.image.width,
      layer.image.height,
    );
    this.setSelection({
      hasSelection: next.width > 0 && next.height > 0,
      ...next,
    });
    return unionRect(prevRect(prev), next);
  }

  onUp(_point: StrokePoint, _layer: Layer, _palette: ColorPalette): Rect {
    const sel = this.getSelection();
    return prevRect(sel);
  }
}

function prevRect(sel: SelectionMask): Rect {
  return { x: sel.x, y: sel.y, width: sel.width, height: sel.height };
}

/** Extracts a copy of the selection's pixels (used for Copy/Cut/move-start). */
export function extractSelection(layer: Layer, sel: SelectionMask): ImageData {
  const out = new ImageData(sel.width, sel.height);
  for (let y = 0; y < sel.height; y++) {
    const srcY = sel.y + y;
    if (srcY < 0 || srcY >= layer.image.height) continue;
    for (let x = 0; x < sel.width; x++) {
      const srcX = sel.x + x;
      if (srcX < 0 || srcX >= layer.image.width) continue;
      const so = (srcY * layer.image.width + srcX) * 4;
      const doff = (y * sel.width + x) * 4;
      out.data[doff] = layer.image.data[so];
      out.data[doff + 1] = layer.image.data[so + 1];
      out.data[doff + 2] = layer.image.data[so + 2];
      out.data[doff + 3] = layer.image.data[so + 3];
    }
  }
  return out;
}

/** Punches a fully-transparent hole where the selection was (used for Cut/move). */
export function clearSelectionArea(layer: Layer, sel: SelectionMask) {
  const r = clampRect(sel, layer.image.width, layer.image.height);
  for (let y = r.y; y < r.y + r.height; y++) {
    for (let x = r.x; x < r.x + r.width; x++) {
      const o = (y * layer.image.width + x) * 4;
      layer.image.data[o] = 0;
      layer.image.data[o + 1] = 0;
      layer.image.data[o + 2] = 0;
      layer.image.data[o + 3] = 0;
    }
  }
}

/** Alpha-composites `pixels` onto the layer at (x,y), preserving any existing content underneath (over, not overwrite). */
export function pasteImageDataAt(
  layer: Layer,
  pixels: ImageData,
  x: number,
  y: number,
): Rect {
  for (let py = 0; py < pixels.height; py++) {
    const dy = y + py;
    if (dy < 0 || dy >= layer.image.height) continue;
    for (let px = 0; px < pixels.width; px++) {
      const dx = x + px;
      if (dx < 0 || dx >= layer.image.width) continue;
      const so = (py * pixels.width + px) * 4;
      const a = pixels.data[so + 3];
      if (a === 0) continue;
      blendPixelOver(layer.image, dx, dy, {
        r: pixels.data[so],
        g: pixels.data[so + 1],
        b: pixels.data[so + 2],
        a,
      }, 1);
    }
  }
  return clampRect(
    { x, y, width: pixels.width, height: pixels.height },
    layer.image.width,
    layer.image.height,
  );
}
