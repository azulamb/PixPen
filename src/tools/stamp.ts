import type { Rgba } from "../color.ts";
import { unionRect } from "../canvas/rect.ts";
import type { Rect } from "../canvas/rect.ts";

/** Alpha-blends a single RGBA color (src, over) onto one pixel of `image`, Porter-Duff "over". */
function blendPixelOver(
  image: ImageData,
  x: number,
  y: number,
  color: Rgba,
  opacity: number,
) {
  if (x < 0 || y < 0 || x >= image.width || y >= image.height) return;
  const sa = (color.a / 255) * opacity;
  if (sa <= 0) return;
  const o = (y * image.width + x) * 4;
  const data = image.data;
  const da = data[o + 3] / 255;
  const outA = sa + da * (1 - sa);
  if (outA <= 0) {
    data[o + 3] = 0;
    return;
  }
  data[o] = (color.r * sa + data[o] * da * (1 - sa)) / outA;
  data[o + 1] = (color.g * sa + data[o + 1] * da * (1 - sa)) / outA;
  data[o + 2] = (color.b * sa + data[o + 2] * da * (1 - sa)) / outA;
  data[o + 3] = outA * 255;
}

/** Reduces alpha proportionally without touching color, i.e. "unpremultiplied" erase. */
function erasePixel(image: ImageData, x: number, y: number, strength: number) {
  if (x < 0 || y < 0 || x >= image.width || y >= image.height) return;
  if (strength <= 0) return;
  const o = (y * image.width + x) * 4 + 3;
  image.data[o] = image.data[o] * (1 - strength);
}

export interface StampOptions {
  shape: "Round" | "Square";
  radius: number;
  color: Rgba;
  opacity: number;
  erase: boolean;
}

function stampAt(
  image: ImageData,
  cx: number,
  cy: number,
  opts: StampOptions,
): Rect {
  const r = Math.max(1, Math.ceil(opts.radius));
  const minX = Math.floor(cx - r);
  const minY = Math.floor(cy - r);
  const maxX = Math.ceil(cx + r);
  const maxY = Math.ceil(cy + r);
  const rSq = opts.radius * opts.radius;

  for (let y = minY; y <= maxY; y++) {
    for (let x = minX; x <= maxX; x++) {
      if (opts.shape === "Round") {
        const dx = x + 0.5 - cx;
        const dy = y + 0.5 - cy;
        if (dx * dx + dy * dy > rSq) continue;
      }
      if (opts.erase) {
        erasePixel(image, x, y, opts.opacity);
      } else {
        blendPixelOver(image, x, y, opts.color, opts.opacity);
      }
    }
  }
  return { x: minX, y: minY, width: maxX - minX + 1, height: maxY - minY + 1 };
}

/** Stamps repeatedly along the segment (x0,y0)-(x1,y1) so fast strokes don't leave gaps. Mirrors BitmapHelper.InterpolateStamp. */
export function interpolateStamp(
  image: ImageData,
  x0: number,
  y0: number,
  x1: number,
  y1: number,
  opts: StampOptions,
): Rect {
  const dist = Math.hypot(x1 - x0, y1 - y0);
  const step = Math.max(1, opts.radius / 2);
  const steps = Math.max(1, Math.ceil(dist / step));
  let dirty: Rect = { x: 0, y: 0, width: 0, height: 0 };
  for (let i = 1; i <= steps; i++) {
    const t = i / steps;
    const x = x0 + (x1 - x0) * t;
    const y = y0 + (y1 - y0) * t;
    dirty = unionRect(dirty, stampAt(image, x, y, opts));
  }
  return dirty;
}

export { blendPixelOver, erasePixel, stampAt };
