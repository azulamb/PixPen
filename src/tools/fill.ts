import type { ColorPalette, Layer } from "../types.ts";
import { parseArgb } from "../color.ts";
import type { Rect } from "../canvas/rect.ts";
import type { StrokePoint, Tool } from "./types.ts";
import { EMPTY_RECT } from "./types.ts";

function samePixel(
  data: Uint8ClampedArray,
  o: number,
  r: number,
  g: number,
  b: number,
  a: number,
): boolean {
  return data[o] === r && data[o + 1] === g && data[o + 2] === b &&
    data[o + 3] === a;
}

/** Stack-based scanline flood fill, exact-color match (no anti-alias/tolerance), matching Tools/FillTool.cs. */
function floodFill(
  image: ImageData,
  startX: number,
  startY: number,
  r: number,
  g: number,
  b: number,
  a: number,
): Rect {
  const { width, height, data } = image;
  const startO = (startY * width + startX) * 4;
  const targetR = data[startO],
    targetG = data[startO + 1],
    targetB = data[startO + 2],
    targetA = data[startO + 3];
  if (targetR === r && targetG === g && targetB === b && targetA === a) {
    return { x: startX, y: startY, width: 1, height: 1 };
  }

  let minX = startX, minY = startY, maxX = startX, maxY = startY;
  const stack: number[] = [startX, startY];

  while (stack.length > 0) {
    const y = stack.pop()!;
    const x = stack.pop()!;
    if (x < 0 || y < 0 || x >= width || y >= height) continue;
    const o = (y * width + x) * 4;
    if (!samePixel(data, o, targetR, targetG, targetB, targetA)) continue;

    // Find span extents on this row.
    let left = x;
    while (
      left > 0 &&
      samePixel(
        data,
        (y * width + (left - 1)) * 4,
        targetR,
        targetG,
        targetB,
        targetA,
      )
    ) left--;
    let right = x;
    while (
      right < width - 1 &&
      samePixel(
        data,
        (y * width + (right + 1)) * 4,
        targetR,
        targetG,
        targetB,
        targetA,
      )
    ) right++;

    for (let sx = left; sx <= right; sx++) {
      const so = (y * width + sx) * 4;
      data[so] = r;
      data[so + 1] = g;
      data[so + 2] = b;
      data[so + 3] = a;
    }
    minX = Math.min(minX, left);
    maxX = Math.max(maxX, right);
    minY = Math.min(minY, y);
    maxY = Math.max(maxY, y);

    if (y - 1 >= 0) {
      for (let sx = left; sx <= right; sx++) {
        if (
          samePixel(
            data,
            ((y - 1) * width + sx) * 4,
            targetR,
            targetG,
            targetB,
            targetA,
          )
        ) stack.push(sx, y - 1);
      }
    }
    if (y + 1 < height) {
      for (let sx = left; sx <= right; sx++) {
        if (
          samePixel(
            data,
            ((y + 1) * width + sx) * 4,
            targetR,
            targetG,
            targetB,
            targetA,
          )
        ) stack.push(sx, y + 1);
      }
    }
  }

  return { x: minX, y: minY, width: maxX - minX + 1, height: maxY - minY + 1 };
}

export class FillTool implements Tool {
  onDown(point: StrokePoint, layer: Layer, palette: ColorPalette): Rect {
    const x = Math.floor(point.x);
    const y = Math.floor(point.y);
    if (x < 0 || y < 0 || x >= layer.image.width || y >= layer.image.height) {
      return EMPTY_RECT;
    }
    const c = parseArgb(palette.foregroundColor);
    return floodFill(layer.image, x, y, c.r, c.g, c.b, c.a);
  }

  onMove(_point: StrokePoint, _layer: Layer, _palette: ColorPalette): Rect {
    return EMPTY_RECT;
  }

  onUp(_point: StrokePoint, _layer: Layer, _palette: ColorPalette): Rect {
    return EMPTY_RECT;
  }
}
