import type { GridSettings, SingleGridSettings } from "../types.ts";
import { argbToCss } from "../color.ts";

export interface Point {
  x: number;
  y: number;
}

/** 2D affine view transform: scale (zoom) + translate (pan). No rotation, matching the WPF app's DrawingCanvas. */
export class Viewport {
  zoom = 1;
  panX = 0;
  panY = 0;

  canvasToScreen(p: Point): Point {
    return { x: p.x * this.zoom + this.panX, y: p.y * this.zoom + this.panY };
  }

  screenToCanvas(p: Point): Point {
    return {
      x: (p.x - this.panX) / this.zoom,
      y: (p.y - this.panY) / this.zoom,
    };
  }

  zoomAt(
    screenX: number,
    screenY: number,
    factor: number,
    minZoom = 0.05,
    maxZoom = 64,
  ) {
    const before = this.screenToCanvas({ x: screenX, y: screenY });
    this.zoom = Math.max(minZoom, Math.min(maxZoom, this.zoom * factor));
    const after = this.canvasToScreen(before);
    this.panX += screenX - after.x;
    this.panY += screenY - after.y;
  }

  fitToWindow(
    viewWidth: number,
    viewHeight: number,
    docWidth: number,
    docHeight: number,
    margin = 0.9,
  ) {
    const scale = Math.min(
      (viewWidth * margin) / docWidth,
      (viewHeight * margin) / docHeight,
    );
    this.zoom = scale > 0 && Number.isFinite(scale) ? scale : 1;
    this.panX = (viewWidth - docWidth * this.zoom) / 2;
    this.panY = (viewHeight - docHeight * this.zoom) / 2;
  }
}

const CHECKER_TILE_CSS_PX = 16;
const CHECKER_LIGHT = "#e8e8e8";
const CHECKER_DARK = "#c8c8c8";

let checkerPatternCanvas: HTMLCanvasElement | null = null;

function getCheckerTile(): HTMLCanvasElement {
  if (checkerPatternCanvas) return checkerPatternCanvas;
  const c = document.createElement("canvas");
  c.width = CHECKER_TILE_CSS_PX * 2;
  c.height = CHECKER_TILE_CSS_PX * 2;
  const ctx = c.getContext("2d")!;
  ctx.fillStyle = CHECKER_LIGHT;
  ctx.fillRect(0, 0, c.width, c.height);
  ctx.fillStyle = CHECKER_DARK;
  ctx.fillRect(0, 0, CHECKER_TILE_CSS_PX, CHECKER_TILE_CSS_PX);
  ctx.fillRect(
    CHECKER_TILE_CSS_PX,
    CHECKER_TILE_CSS_PX,
    CHECKER_TILE_CSS_PX,
    CHECKER_TILE_CSS_PX,
  );
  checkerPatternCanvas = c;
  return c;
}

/** Draws a checkerboard sized so tiles stay a constant ~16 CSS px regardless of zoom (transparency indicator). */
export function drawCheckerboard(
  ctx: CanvasRenderingContext2D,
  view: Viewport,
  docWidth: number,
  docHeight: number,
) {
  const topLeft = view.canvasToScreen({ x: 0, y: 0 });
  const bottomRight = view.canvasToScreen({ x: docWidth, y: docHeight });
  ctx.save();
  ctx.beginPath();
  ctx.rect(
    topLeft.x,
    topLeft.y,
    bottomRight.x - topLeft.x,
    bottomRight.y - topLeft.y,
  );
  ctx.clip();
  const pattern = ctx.createPattern(getCheckerTile(), "repeat")!;
  ctx.fillStyle = pattern;
  ctx.translate(topLeft.x, topLeft.y);
  ctx.fillRect(0, 0, bottomRight.x - topLeft.x, bottomRight.y - topLeft.y);
  ctx.restore();
}

const MAX_GRID_LINES = 2000;

function drawSingleGrid(
  ctx: CanvasRenderingContext2D,
  view: Viewport,
  docWidth: number,
  docHeight: number,
  grid: SingleGridSettings,
) {
  if (!grid.visible || grid.spacingX <= 0 || grid.spacingY <= 0) return;
  const color = argbToCss(grid.color);
  ctx.save();
  ctx.strokeStyle = color;
  ctx.lineWidth = 1;
  if (grid.lineType === "Dashed") ctx.setLineDash([4, 4]);

  const topLeft = view.canvasToScreen({ x: 0, y: 0 });
  const bottomRight = view.canvasToScreen({ x: docWidth, y: docHeight });

  const startX = Math.floor((-grid.offsetX) / grid.spacingX) * grid.spacingX +
    grid.offsetX;
  const vCount = Math.ceil(docWidth / grid.spacingX) + 1;
  const hCount = Math.ceil(docHeight / grid.spacingY) + 1;
  if (vCount + hCount > MAX_GRID_LINES) {
    ctx.restore();
    return;
  }

  ctx.beginPath();
  for (let x = startX; x <= docWidth; x += grid.spacingX) {
    if (x < 0) continue;
    const sx = view.canvasToScreen({ x, y: 0 }).x;
    ctx.moveTo(sx, topLeft.y);
    ctx.lineTo(sx, bottomRight.y);
  }
  const startY = Math.floor((-grid.offsetY) / grid.spacingY) * grid.spacingY +
    grid.offsetY;
  for (let y = startY; y <= docHeight; y += grid.spacingY) {
    if (y < 0) continue;
    const sy = view.canvasToScreen({ x: 0, y }).y;
    ctx.moveTo(topLeft.x, sy);
    ctx.lineTo(bottomRight.x, sy);
  }
  ctx.stroke();
  ctx.restore();
}

export function drawGrid(
  ctx: CanvasRenderingContext2D,
  view: Viewport,
  docWidth: number,
  docHeight: number,
  grid: GridSettings,
) {
  drawSingleGrid(ctx, view, docWidth, docHeight, grid.large);
  drawSingleGrid(ctx, view, docWidth, docHeight, grid.medium);
  drawSingleGrid(ctx, view, docWidth, docHeight, grid.small);
}
