import type { Layer, PixPenDocument } from "../types.ts";
import type { Rect } from "./rect.ts";
import { clampRect } from "./rect.ts";

/** Porter-Duff "over", operating on a single pixel. srcA already includes layer opacity. */
function blendPixel(
  dst: Uint8ClampedArray,
  dstOffset: number,
  sr: number,
  sg: number,
  sb: number,
  sa: number, // 0-1
) {
  if (sa <= 0) return;
  const dr = dst[dstOffset],
    dg = dst[dstOffset + 1],
    db = dst[dstOffset + 2],
    da = dst[dstOffset + 3] / 255;
  const outA = sa + da * (1 - sa);
  if (outA <= 0) {
    dst[dstOffset + 3] = 0;
    return;
  }
  dst[dstOffset] = (sr * sa + dr * da * (1 - sa)) / outA;
  dst[dstOffset + 1] = (sg * sa + dg * da * (1 - sa)) / outA;
  dst[dstOffset + 2] = (sb * sa + db * da * (1 - sa)) / outA;
  dst[dstOffset + 3] = outA * 255;
}

/** Composites a normal (canvas-sized) layer onto target within `region`. */
function compositeNormalLayer(target: ImageData, layer: Layer, region: Rect) {
  const src = layer.image;
  const w = target.width;
  const opacity = layer.opacity;
  const r = clampRect(region, target.width, target.height);
  for (let y = r.y; y < r.y + r.height; y++) {
    for (let x = r.x; x < r.x + r.width; x++) {
      const o = (y * w + x) * 4;
      const sa = (src.data[o + 3] / 255) * opacity;
      if (sa <= 0) continue;
      blendPixel(
        target.data,
        o,
        src.data[o],
        src.data[o + 1],
        src.data[o + 2],
        sa,
      );
    }
  }
}

/** Composites a reference layer (original-size image, placed via refX/refY/refWidth/refHeight) using nearest-neighbor sampling. */
function compositeReferenceLayer(
  target: ImageData,
  layer: Layer,
  region: Rect,
) {
  const src = layer.image;
  const dstW = layer.refWidth || src.width;
  const dstH = layer.refHeight || src.height;
  if (dstW <= 0 || dstH <= 0) return;

  const left = Math.max(Math.floor(layer.refX), region.x, 0);
  const top = Math.max(Math.floor(layer.refY), region.y, 0);
  const right = Math.min(
    Math.ceil(layer.refX + dstW),
    region.x + region.width,
    target.width,
  );
  const bottom = Math.min(
    Math.ceil(layer.refY + dstH),
    region.y + region.height,
    target.height,
  );
  if (right <= left || bottom <= top) return;

  const scaleX = src.width / dstW;
  const scaleY = src.height / dstH;
  const opacity = layer.opacity;
  const w = target.width;

  for (let y = top; y < bottom; y++) {
    const sy = Math.floor((y - layer.refY) * scaleY);
    if (sy < 0 || sy >= src.height) continue;
    for (let x = left; x < right; x++) {
      const sx = Math.floor((x - layer.refX) * scaleX);
      if (sx < 0 || sx >= src.width) continue;
      const so = (sy * src.width + sx) * 4;
      const sa = (src.data[so + 3] / 255) * opacity;
      if (sa <= 0) continue;
      const o = (y * w + x) * 4;
      blendPixel(
        target.data,
        o,
        src.data[so],
        src.data[so + 1],
        src.data[so + 2],
        sa,
      );
    }
  }
}

/** Composites all visible layers (index 0 = top, highest index = bottom) into a fresh transparent ImageData. */
export function compositeAll(doc: PixPenDocument, region?: Rect): ImageData {
  const target = new ImageData(doc.width, doc.height);
  const r = region ?? { x: 0, y: 0, width: doc.width, height: doc.height };
  // Verified against the WPF app's actual rendering (docs/ss.png): index 0 ends up on top, so paint
  // descending (highest index = bottom, drawn first; index 0 = top, drawn last).
  const ordered = [...doc.layers].sort((a, b) => b.index - a.index);
  for (const layer of ordered) {
    if (!layer.isVisible) continue;
    if (layer.isReference) {
      compositeReferenceLayer(target, layer, r);
    } else {
      compositeNormalLayer(target, layer, r);
    }
  }
  return target;
}

/** Composites into an existing target ImageData (in place), only touching `region`. Used for partial recomposite. */
export function compositeAllInto(
  target: ImageData,
  doc: PixPenDocument,
  region: Rect,
) {
  const r = clampRect(region, target.width, target.height);
  for (let y = r.y; y < r.y + r.height; y++) {
    for (let x = r.x; x < r.x + r.width; x++) {
      const o = (y * target.width + x) * 4;
      target.data[o] = 0;
      target.data[o + 1] = 0;
      target.data[o + 2] = 0;
      target.data[o + 3] = 0;
    }
  }
  // Verified against the WPF app's actual rendering (docs/ss.png): index 0 ends up on top, so paint
  // descending (highest index = bottom, drawn first; index 0 = top, drawn last).
  const ordered = [...doc.layers].sort((a, b) => b.index - a.index);
  for (const layer of ordered) {
    if (!layer.isVisible) continue;
    if (layer.isReference) {
      compositeReferenceLayer(target, layer, r);
    } else {
      compositeNormalLayer(target, layer, r);
    }
  }
}
