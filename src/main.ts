import {
  createDefaultDocument,
  createDefaultPens,
  createTransparentImageData,
  type Layer,
  type PixPenDocument,
  type SelectionMask,
} from "./types.ts";
import {
  drawCheckerboard,
  drawGrid,
  type Point,
  Viewport,
} from "./canvas/view.ts";
import { compositeAll, compositeAllInto } from "./canvas/compositor.ts";
import { type Rect, unionRect } from "./canvas/rect.ts";
import { reindexLayers, snapshotRect, UndoStack } from "./undo.ts";
import { PenTool } from "./tools/pen.ts";
import { EraserTool } from "./tools/eraser.ts";
import { FillTool } from "./tools/fill.ts";
import { EyedropperTool } from "./tools/eyedropper.ts";
import {
  clearSelectionArea,
  emptySelection,
  extractSelection,
  isPointInSelection,
  pasteImageDataAt,
  SelectionTool,
} from "./tools/selection.ts";
import type { Tool } from "./tools/types.ts";
import { pointerEventToStrokePoint } from "./input/pointer.ts";
import {
  type AppSettings,
  loadAppSettings,
  saveAppSettings,
} from "./settings/app-settings.ts";
import {
  BUILTIN_PALETTE_NAME,
  deletePalettePreset,
  listPaletteNames,
  loadPalettePreset,
  savePalettePreset,
} from "./settings/palette-store.ts";
import { openPpxFromFile, pickAndOpenPpx, saveDocument } from "./file/io.ts";
import { exportLayersAsZip, exportMergedPng } from "./file/export.ts";
import { imageDataToPngBytes, pngBytesToImageData } from "./file/png.ts";
import { createToolbar, type ToolKind } from "./ui/toolbar.ts";
import { createLayerPanel } from "./ui/layer-panel.ts";
import { createColorPanel } from "./ui/color-panel.ts";
import { createPenPanel } from "./ui/pen-panel.ts";
import {
  showAppSettingsDialog,
  showConfirm,
  showDocumentSettingsDialog,
  showExportDialog,
  showGridSettingsDialog,
} from "./ui/dialogs.ts";

// ---------------------------------------------------------------------------
// DOM references
// ---------------------------------------------------------------------------

const toolbarSlot = document.getElementById("toolbar-slot")!;
const leftPanel = document.getElementById("left-panel")!;
const rightPanel = document.getElementById("right-panel")!;
const canvasArea = document.getElementById("canvas-area")!;
const viewCanvas = document.getElementById("view-canvas") as HTMLCanvasElement;
const viewCtx = viewCanvas.getContext("2d")!;
const statusBar = document.getElementById("status-bar")!;

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------

let appSettings: AppSettings = loadAppSettings();
applyTheme(appSettings.theme);

let doc: PixPenDocument = createDefaultDocument(
  appSettings.defaultWidth,
  appSettings.defaultHeight,
  appSettings.defaultDpi,
);
applyDefaultPaletteToDoc(doc);

let activeLayerId = doc.layers[0].id;
let activeToolKind: ToolKind = "Pen";
let activePenIndex = 0;
let selection: SelectionMask = emptySelection();
let undoStack = new UndoStack(() =>
  appSettings.undoMemoryLimitMb * 1024 * 1024
);
const view = new Viewport();
let dpr = globalThis.devicePixelRatio || 1;

let internalClipboard: ImageData | null = null;
let floatingOverlay: { x: number; y: number; image: ImageData } | null = null;
let overlayCanvas: HTMLCanvasElement | null = null;

let strokeBeforeFull: Uint8ClampedArray<ArrayBuffer> | null = null;
let strokeDirty: Rect = { x: 0, y: 0, width: 0, height: 0 };
let activePointerId: number | null = null;
let selectionDragMode: "none" | "marquee" | "move" = "none";
let moveDragStart: Point = { x: 0, y: 0 };
let moveOverlayStart: Point = { x: 0, y: 0 };
let isPanning = false;
let panStart = { x: 0, y: 0, panX: 0, panY: 0 };

interface RefDragState {
  handle: "move" | "nw" | "ne" | "sw" | "se";
  startX: number;
  startY: number;
  orig: { refX: number; refY: number; refWidth: number; refHeight: number };
}
let refDrag: RefDragState | null = null;

let compositeImage: ImageData = compositeAll(doc);
const offscreen = document.createElement("canvas");
offscreen.width = doc.width;
offscreen.height = doc.height;
const offscreenCtx = offscreen.getContext("2d", { willReadFrequently: true })!;
offscreenCtx.putImageData(compositeImage, 0, 0);

// ---------------------------------------------------------------------------
// Tools
// ---------------------------------------------------------------------------

const penTool = new PenTool(() => doc.pens[activePenIndex] ?? doc.pens[0]);
const eraserTool = new EraserTool(() =>
  doc.pens[activePenIndex] ?? doc.pens[0]
);
const fillTool = new FillTool();
const eyedropperTool = new EyedropperTool(() => compositeImage);
const selectionTool = new SelectionTool(
  () => selection,
  (s) => (selection = s),
);

function getTool(kind: ToolKind): Tool {
  switch (kind) {
    case "Pen":
      return penTool;
    case "Eraser":
      return eraserTool;
    case "Fill":
      return fillTool;
    case "Eyedropper":
      return eyedropperTool;
    case "Selection":
      return selectionTool;
  }
}

function effectiveToolKind(point: { isEraser: boolean }): ToolKind {
  if (activeToolKind === "Pen" && point.isEraser) return "Eraser";
  return activeToolKind;
}

function currentActiveLayer(): Layer {
  return doc.layers.find((l) => l.id === activeLayerId) ?? doc.layers[0];
}

function ensureActiveLayerValid() {
  if (!doc.layers.some((l) => l.id === activeLayerId)) {
    activeLayerId = doc.layers[0]?.id ?? "";
  }
  if (activePenIndex >= doc.pens.length) {
    activePenIndex = Math.max(0, doc.pens.length - 1);
  }
}

// ---------------------------------------------------------------------------
// Rendering
// ---------------------------------------------------------------------------

function resizeViewCanvas() {
  const r = canvasArea.getBoundingClientRect();
  dpr = globalThis.devicePixelRatio || 1;
  viewCanvas.width = Math.max(1, Math.floor(r.width * dpr));
  viewCanvas.height = Math.max(1, Math.floor(r.height * dpr));
  render();
}

function render() {
  viewCtx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const cssW = viewCanvas.width / dpr;
  const cssH = viewCanvas.height / dpr;
  viewCtx.clearRect(0, 0, cssW, cssH);

  drawCheckerboard(viewCtx, view, doc.width, doc.height);

  viewCtx.save();
  viewCtx.imageSmoothingEnabled = false;
  viewCtx.translate(view.panX, view.panY);
  viewCtx.scale(view.zoom, view.zoom);
  viewCtx.drawImage(offscreen, 0, 0);
  if (floatingOverlay && overlayCanvas) {
    viewCtx.drawImage(overlayCanvas, floatingOverlay.x, floatingOverlay.y);
  }
  viewCtx.restore();

  drawGrid(viewCtx, view, doc.width, doc.height, doc.grid);
  drawSelectionOverlay();
  drawRefHandles();
}

function drawSelectionOverlay() {
  if (!selection.hasSelection) return;
  const topLeft = view.canvasToScreen({ x: selection.x, y: selection.y });
  const bottomRight = view.canvasToScreen({
    x: selection.x + selection.width,
    y: selection.y + selection.height,
  });
  viewCtx.save();
  viewCtx.strokeStyle = "#000";
  viewCtx.setLineDash([4, 4]);
  viewCtx.lineWidth = 1;
  viewCtx.strokeRect(
    topLeft.x,
    topLeft.y,
    bottomRight.x - topLeft.x,
    bottomRight.y - topLeft.y,
  );
  viewCtx.strokeStyle = "#fff";
  viewCtx.lineDashOffset = 4;
  viewCtx.strokeRect(
    topLeft.x,
    topLeft.y,
    bottomRight.x - topLeft.x,
    bottomRight.y - topLeft.y,
  );
  viewCtx.restore();
}

function drawRefHandles() {
  const layer = currentActiveLayer();
  if (!layer || !layer.isReference) return;
  const w = layer.refWidth || layer.image.width;
  const h = layer.refHeight || layer.image.height;
  const topLeft = view.canvasToScreen({ x: layer.refX, y: layer.refY });
  const bottomRight = view.canvasToScreen({
    x: layer.refX + w,
    y: layer.refY + h,
  });
  viewCtx.save();
  viewCtx.strokeStyle = "#3670db";
  viewCtx.setLineDash([6, 4]);
  viewCtx.strokeRect(
    topLeft.x,
    topLeft.y,
    bottomRight.x - topLeft.x,
    bottomRight.y - topLeft.y,
  );
  viewCtx.setLineDash([]);
  viewCtx.fillStyle = "#3670db";
  for (
    const p of [topLeft, bottomRight, { x: topLeft.x, y: bottomRight.y }, {
      x: bottomRight.x,
      y: topLeft.y,
    }]
  ) {
    viewCtx.fillRect(p.x - 4, p.y - 4, 8, 8);
  }
  viewCtx.restore();
}

function recompositeRegion(rect: Rect) {
  if (rect.width <= 0 || rect.height <= 0) return;
  compositeAllInto(compositeImage, doc, rect);
  offscreenCtx.putImageData(
    compositeImage,
    0,
    0,
    rect.x,
    rect.y,
    rect.width,
    rect.height,
  );
}

function recomputeFullComposite() {
  compositeImage = compositeAll(doc);
  offscreenCtx.putImageData(compositeImage, 0, 0);
}

function setFloatingOverlay(x: number, y: number, image: ImageData) {
  overlayCanvas = document.createElement("canvas");
  overlayCanvas.width = image.width;
  overlayCanvas.height = image.height;
  overlayCanvas.getContext("2d")!.putImageData(image, 0, 0);
  floatingOverlay = { x, y, image };
}

// ---------------------------------------------------------------------------
// Stroke / undo helpers
// ---------------------------------------------------------------------------

function beginStroke(layer: Layer) {
  strokeBeforeFull = new Uint8ClampedArray(layer.image.data);
  strokeDirty = { x: 0, y: 0, width: 0, height: 0 };
}

function accumulateDirty(rect: Rect) {
  strokeDirty = unionRect(strokeDirty, rect);
}

function commitStroke(layer: Layer) {
  if (strokeBeforeFull && strokeDirty.width > 0 && strokeDirty.height > 0) {
    const wrapped = new ImageData(
      strokeBeforeFull,
      layer.image.width,
      layer.image.height,
    );
    const before = snapshotRect({ image: wrapped } as Layer, strokeDirty);
    undoStack.pushStroke(layer, strokeDirty, before);
    doc.isModified = true;
  }
  strokeBeforeFull = null;
  strokeDirty = { x: 0, y: 0, width: 0, height: 0 };
}

function pushPixelUndo(
  layer: Layer,
  before: Uint8ClampedArray<ArrayBuffer>,
  rect: Rect,
) {
  const wrapped = new ImageData(before, layer.image.width, layer.image.height);
  const beforeRect = snapshotRect({ image: wrapped } as Layer, rect);
  undoStack.pushStroke(layer, rect, beforeRect);
  doc.isModified = true;
}

// ---------------------------------------------------------------------------
// Reference layer transform handles
// ---------------------------------------------------------------------------

const HANDLE_SCREEN_RADIUS = 8;

function hitTestRef(
  layer: Layer,
  p: Point,
): "move" | "nw" | "ne" | "sw" | "se" | null {
  const w = layer.refWidth || layer.image.width;
  const h = layer.refHeight || layer.image.height;
  const corners: Record<string, Point> = {
    nw: { x: layer.refX, y: layer.refY },
    ne: { x: layer.refX + w, y: layer.refY },
    sw: { x: layer.refX, y: layer.refY + h },
    se: { x: layer.refX + w, y: layer.refY + h },
  };
  const screen = view.canvasToScreen(p);
  for (const key of ["nw", "ne", "sw", "se"] as const) {
    const hs = view.canvasToScreen(corners[key]);
    if (Math.hypot(screen.x - hs.x, screen.y - hs.y) <= HANDLE_SCREEN_RADIUS) {
      return key;
    }
  }
  if (
    p.x >= layer.refX && p.x <= layer.refX + w && p.y >= layer.refY &&
    p.y <= layer.refY + h
  ) return "move";
  return null;
}

function updateRefDrag(layer: Layer, point: Point, lockAspect: boolean) {
  if (!refDrag) return;
  const dx = point.x - refDrag.startX;
  const dy = point.y - refDrag.startY;
  const { refX, refY, refWidth, refHeight } = refDrag.orig;

  if (refDrag.handle === "move") {
    layer.refX = refX + dx;
    layer.refY = refY + dy;
    return;
  }

  const aspect = refWidth / refHeight;
  let newRefX = refX, newRefY = refY, newW = refWidth, newH = refHeight;
  if (refDrag.handle === "se") {
    newW = Math.max(1, refWidth + dx);
    newH = lockAspect ? newW / aspect : Math.max(1, refHeight + dy);
  } else if (refDrag.handle === "nw") {
    newW = Math.max(1, refWidth - dx);
    newH = lockAspect ? newW / aspect : Math.max(1, refHeight - dy);
    newRefX = refX + refWidth - newW;
    newRefY = refY + refHeight - newH;
  } else if (refDrag.handle === "ne") {
    newW = Math.max(1, refWidth + dx);
    newH = lockAspect ? newW / aspect : Math.max(1, refHeight - dy);
    newRefY = refY + refHeight - newH;
  } else if (refDrag.handle === "sw") {
    newW = Math.max(1, refWidth - dx);
    newH = lockAspect ? newW / aspect : Math.max(1, refHeight + dy);
    newRefX = refX + refWidth - newW;
  }
  layer.refX = newRefX;
  layer.refY = newRefY;
  layer.refWidth = newW;
  layer.refHeight = newH;
}

// ---------------------------------------------------------------------------
// Pointer interaction
// ---------------------------------------------------------------------------

viewCanvas.addEventListener("contextmenu", (e) => e.preventDefault());

viewCanvas.addEventListener("pointerdown", (e) => {
  e.preventDefault();
  viewCanvas.setPointerCapture(e.pointerId);
  activePointerId = e.pointerId;

  if (e.button === 1) {
    isPanning = true;
    panStart = { x: e.clientX, y: e.clientY, panX: view.panX, panY: view.panY };
    return;
  }

  const point = pointerEventToStrokePoint(e, viewCanvas, view);
  const layer = currentActiveLayer();

  if (e.button === 2) {
    eyedropperTool.onDown(point, layer, doc.palette);
    refreshColorPanel();
    return;
  }

  if (layer.isReference) {
    const hit = hitTestRef(layer, point);
    if (hit) {
      refDrag = {
        handle: hit,
        startX: point.x,
        startY: point.y,
        orig: {
          refX: layer.refX,
          refY: layer.refY,
          refWidth: layer.refWidth || layer.image.width,
          refHeight: layer.refHeight || layer.image.height,
        },
      };
    }
    return;
  }

  const kind = effectiveToolKind(point);

  if (kind === "Selection") {
    if (isPointInSelection(selection, point.x, point.y) && !layer.isLocked) {
      selectionDragMode = "move";
      strokeBeforeFull = new Uint8ClampedArray(layer.image.data);
      const extracted = extractSelection(layer, selection);
      clearSelectionArea(layer, selection);
      setFloatingOverlay(selection.x, selection.y, extracted);
      moveOverlayStart = { x: selection.x, y: selection.y };
      moveDragStart = { x: point.x, y: point.y };
      strokeDirty = {
        x: selection.x,
        y: selection.y,
        width: selection.width,
        height: selection.height,
      };
      recompositeRegion(strokeDirty);
      render();
      return;
    }
    selectionDragMode = "marquee";
    selectionTool.onDown(point, layer, doc.palette);
    render();
    return;
  }

  if (kind === "Eyedropper") {
    eyedropperTool.onDown(point, layer, doc.palette);
    refreshColorPanel();
    return;
  }

  if (layer.isLocked) return;

  beginStroke(layer);
  const rect = getTool(kind).onDown(point, layer, doc.palette);
  accumulateDirty(rect);
  recompositeRegion(rect);
  render();
});

viewCanvas.addEventListener("pointermove", (e) => {
  if (e.pointerId !== activePointerId) return;

  if (isPanning) {
    view.panX = panStart.panX + (e.clientX - panStart.x);
    view.panY = panStart.panY + (e.clientY - panStart.y);
    render();
    return;
  }

  const point = pointerEventToStrokePoint(e, viewCanvas, view);
  const layer = currentActiveLayer();

  if (refDrag) {
    updateRefDrag(layer, point, e.ctrlKey);
    render();
    return;
  }

  if (selectionDragMode === "move" && floatingOverlay) {
    const dx = Math.round(point.x - moveDragStart.x);
    const dy = Math.round(point.y - moveDragStart.y);
    floatingOverlay.x = moveOverlayStart.x + dx;
    floatingOverlay.y = moveOverlayStart.y + dy;
    render();
    return;
  }

  if (selectionDragMode === "marquee") {
    selectionTool.onMove(point, layer, doc.palette);
    render();
    return;
  }

  if (layer.isReference || layer.isLocked) return;

  const kind = effectiveToolKind(point);
  if (kind === "Eyedropper" || kind === "Fill" || kind === "Selection") return;

  if (strokeBeforeFull) {
    const rect = getTool(kind).onMove(point, layer, doc.palette);
    accumulateDirty(rect);
    recompositeRegion(rect);
    render();
  }
});

function endPointer(e: PointerEvent) {
  if (e.pointerId !== activePointerId) return;
  activePointerId = null;
  if (viewCanvas.hasPointerCapture(e.pointerId)) {
    viewCanvas.releasePointerCapture(e.pointerId);
  }

  if (isPanning) {
    isPanning = false;
    return;
  }

  const point = pointerEventToStrokePoint(e, viewCanvas, view);
  const layer = currentActiveLayer();

  if (refDrag) {
    const after = {
      refX: layer.refX,
      refY: layer.refY,
      refWidth: layer.refWidth,
      refHeight: layer.refHeight,
    };
    const changed = Object.entries(after).some(([k, v]) =>
      refDrag!.orig[k as keyof typeof after] !== v
    );
    if (changed) {
      undoStack.push({
        kind: "refTransform",
        layerId: layer.id,
        before: refDrag.orig,
        after,
      });
      doc.isModified = true;
    }
    refDrag = null;
    recomputeFullComposite();
    refreshAll();
    render();
    return;
  }

  if (selectionDragMode === "move" && floatingOverlay) {
    const pasteRect = pasteImageDataAt(
      layer,
      floatingOverlay.image,
      floatingOverlay.x,
      floatingOverlay.y,
    );
    selection = {
      hasSelection: true,
      x: floatingOverlay.x,
      y: floatingOverlay.y,
      width: floatingOverlay.image.width,
      height: floatingOverlay.image.height,
    };
    accumulateDirty(pasteRect);
    if (strokeBeforeFull) pushPixelUndo(layer, strokeBeforeFull, strokeDirty);
    strokeBeforeFull = null;
    floatingOverlay = null;
    overlayCanvas = null;
    recompositeRegion(strokeDirty);
    selectionDragMode = "none";
    refreshAll();
    render();
    return;
  }

  if (selectionDragMode === "marquee") {
    selectionTool.onUp(point, layer, doc.palette);
    selectionDragMode = "none";
    render();
    return;
  }

  const kind = effectiveToolKind(point);
  if (kind === "Eyedropper" || layer.isReference || layer.isLocked) return;

  if (strokeBeforeFull) {
    getTool(kind).onUp(point, layer, doc.palette);
    commitStroke(layer);
    refreshAll();
    render();
  }
}
viewCanvas.addEventListener("pointerup", endPointer);
viewCanvas.addEventListener("pointercancel", endPointer);

viewCanvas.addEventListener(
  "wheel",
  (e) => {
    e.preventDefault();
    const r = viewCanvas.getBoundingClientRect();
    const sx = e.clientX - r.left;
    const sy = e.clientY - r.top;
    if (e.ctrlKey) {
      view.zoomAt(sx, sy, e.deltaY < 0 ? 1.1 : 1 / 1.1);
    } else if (e.shiftKey) {
      view.panX -= e.deltaY;
    } else {
      view.panX -= e.deltaX;
      view.panY -= e.deltaY;
    }
    render();
    refreshUndoButtons();
  },
  { passive: false },
);

globalThis.addEventListener("resize", resizeViewCanvas);

// ---------------------------------------------------------------------------
// Keyboard shortcuts
// ---------------------------------------------------------------------------

const TOOL_KEYS: Record<string, ToolKind> = {
  p: "Pen",
  e: "Eraser",
  i: "Eyedropper",
  s: "Selection",
  f: "Fill",
};

globalThis.addEventListener("keydown", (e) => {
  const target = e.target as HTMLElement;
  if (target && ["INPUT", "SELECT", "TEXTAREA"].includes(target.tagName)) {
    return;
  }

  const ctrl = e.ctrlKey || e.metaKey;
  const key = e.key.toLowerCase();

  if (ctrl && key === "z" && !e.shiftKey) {
    e.preventDefault();
    doUndo();
  } else if (ctrl && (key === "y" || (key === "z" && e.shiftKey))) {
    e.preventDefault();
    doRedo();
  } else if (ctrl && e.shiftKey && key === "s") {
    e.preventDefault();
    doSave(true);
  } else if (ctrl && key === "s") {
    e.preventDefault();
    doSave(false);
  } else if (ctrl && key === "n") {
    e.preventDefault();
    doNew();
  } else if (ctrl && key === "o") {
    e.preventDefault();
    doOpen();
  } else if (ctrl && key === "x") {
    e.preventDefault();
    doCut();
  } else if (ctrl && key === "c") {
    e.preventDefault();
    doCopy();
  } else if (ctrl && key === "v") {
    e.preventDefault();
    doPaste();
  } else if (e.key === "Delete" || e.key === "Backspace") {
    doDeleteSelection();
  } else if (!ctrl && !e.altKey && TOOL_KEYS[key]) {
    setActiveTool(TOOL_KEYS[key]);
  }
});

globalThis.addEventListener("beforeunload", (e) => {
  if (doc.isModified) {
    e.preventDefault();
    e.returnValue = "";
  }
});

// ---------------------------------------------------------------------------
// Clipboard commands
// ---------------------------------------------------------------------------

async function doCopy() {
  if (!selection.hasSelection) return;
  const layer = currentActiveLayer();
  const pixels = extractSelection(layer, selection);
  internalClipboard = pixels;
  try {
    const bytes = await imageDataToPngBytes(pixels);
    await navigator.clipboard.write([
      new ClipboardItem({
        "image/png": new Blob([bytes], { type: "image/png" }),
      }),
    ]);
  } catch {
    // Clipboard API unavailable or permission denied: the internal clipboard still covers in-app paste.
  }
}

async function doCut() {
  if (!selection.hasSelection) return;
  const layer = currentActiveLayer();
  if (layer.isLocked || layer.isReference) return;
  await doCopy();
  const before = new Uint8ClampedArray(layer.image.data);
  const rect = {
    x: selection.x,
    y: selection.y,
    width: selection.width,
    height: selection.height,
  };
  clearSelectionArea(layer, selection);
  pushPixelUndo(layer, before, rect);
  recompositeRegion(rect);
  refreshAll();
  render();
}

async function doPaste() {
  const layer = currentActiveLayer();
  if (layer.isLocked || layer.isReference) return;
  let pixels: ImageData | null = internalClipboard;
  try {
    const items = await navigator.clipboard.read();
    for (const item of items) {
      if (item.types.includes("image/png")) {
        const blob = await item.getType("image/png");
        pixels = await pngBytesToImageData(
          new Uint8Array(await blob.arrayBuffer()),
        );
        break;
      }
    }
  } catch {
    // fall back to the internal clipboard captured above
  }
  if (!pixels) return;

  const centerScreen = {
    x: viewCanvas.width / (2 * dpr),
    y: viewCanvas.height / (2 * dpr),
  };
  const centerCanvas = view.screenToCanvas(centerScreen);
  const x = Math.round(centerCanvas.x - pixels.width / 2);
  const y = Math.round(centerCanvas.y - pixels.height / 2);

  const before = new Uint8ClampedArray(layer.image.data);
  const rect = pasteImageDataAt(layer, pixels, x, y);
  pushPixelUndo(layer, before, rect);
  selection = {
    hasSelection: true,
    x,
    y,
    width: pixels.width,
    height: pixels.height,
  };
  recompositeRegion(rect);
  refreshAll();
  render();
}

function doDeleteSelection() {
  if (!selection.hasSelection) return;
  const layer = currentActiveLayer();
  if (layer.isLocked || layer.isReference) return;
  const before = new Uint8ClampedArray(layer.image.data);
  const rect = {
    x: selection.x,
    y: selection.y,
    width: selection.width,
    height: selection.height,
  };
  clearSelectionArea(layer, selection);
  pushPixelUndo(layer, before, rect);
  recompositeRegion(rect);
  refreshAll();
  render();
}

// ---------------------------------------------------------------------------
// Undo / redo
// ---------------------------------------------------------------------------

function doUndo() {
  const rect = undoStack.undo(doc);
  if (rect) recompositeRegion(rect);
  else recomputeFullComposite();
  ensureActiveLayerValid();
  refreshAll();
  render();
}

function doRedo() {
  const rect = undoStack.redo(doc);
  if (rect) recompositeRegion(rect);
  else recomputeFullComposite();
  ensureActiveLayerValid();
  refreshAll();
  render();
}

// ---------------------------------------------------------------------------
// Layer operations
// ---------------------------------------------------------------------------

function addLayer() {
  if (doc.layers.length >= appSettings.maxLayers) return;
  const layer: Layer = {
    id: crypto.randomUUID(),
    index: doc.layers.length,
    name: `Layer ${doc.layers.length + 1}`,
    isVisible: true,
    isLocked: false,
    opacity: 1,
    isReference: false,
    image: createTransparentImageData(doc.width, doc.height),
    refX: 0,
    refY: 0,
    refWidth: 0,
    refHeight: 0,
  };
  // New layers land on top (index 0), matching the WPF app and common paint-tool convention.
  const atIndex = 0;
  doc.layers.unshift(layer);
  reindexLayers(doc.layers);
  undoStack.push({ kind: "layerAdd", layer, atIndex });
  activeLayerId = layer.id;
  doc.isModified = true;
  recomputeFullComposite();
  refreshAll();
  render();
}

function deleteLayer(id: string) {
  if (doc.layers.length <= 1) return;
  const idx = doc.layers.findIndex((l) => l.id === id);
  if (idx < 0) return;
  const [layer] = doc.layers.splice(idx, 1);
  reindexLayers(doc.layers);
  undoStack.push({ kind: "layerRemove", layer, atIndex: idx });
  ensureActiveLayerValid();
  doc.isModified = true;
  recomputeFullComposite();
  refreshAll();
  render();
}

function duplicateLayer(id: string) {
  if (doc.layers.length >= appSettings.maxLayers) return;
  const idx = doc.layers.findIndex((l) => l.id === id);
  if (idx < 0) return;
  const src = doc.layers[idx];
  const copy: Layer = {
    ...src,
    id: crypto.randomUUID(),
    name: `${src.name} copy`,
    image: new ImageData(
      new Uint8ClampedArray(src.image.data),
      src.image.width,
      src.image.height,
    ),
  };
  // Duplicate lands above the source (index 0 = top), matching common paint-tool convention.
  doc.layers.splice(idx, 0, copy);
  reindexLayers(doc.layers);
  undoStack.push({ kind: "layerAdd", layer: copy, atIndex: idx });
  activeLayerId = copy.id;
  doc.isModified = true;
  recomputeFullComposite();
  refreshAll();
  render();
}

function moveLayer(id: string, dir: "up" | "down") {
  const idx = doc.layers.findIndex((l) => l.id === id);
  if (idx < 0) return;
  // index 0 = top, so "up" (more on top) moves toward the front of the array.
  const to = dir === "up" ? idx - 1 : idx + 1;
  if (to < 0 || to >= doc.layers.length) return;
  const [moved] = doc.layers.splice(idx, 1);
  doc.layers.splice(to, 0, moved);
  reindexLayers(doc.layers);
  undoStack.push({ kind: "layerReorder", fromIndex: idx, toIndex: to });
  doc.isModified = true;
  recomputeFullComposite();
  refreshAll();
  render();
}

function setLayerVisible(id: string, visible: boolean) {
  const l = doc.layers.find((x) => x.id === id);
  if (!l) return;
  l.isVisible = visible;
  doc.isModified = true;
  recomputeFullComposite();
  refreshAll();
  render();
}

function setLayerLocked(id: string, locked: boolean) {
  const l = doc.layers.find((x) => x.id === id);
  if (!l) return;
  l.isLocked = locked;
  refreshLayerPanel();
}

function setLayerOpacity(id: string, opacity: number) {
  const l = doc.layers.find((x) => x.id === id);
  if (!l) return;
  l.opacity = opacity;
  doc.isModified = true;
  recomputeFullComposite();
  refreshAll();
  render();
}

function renameLayer(id: string, name: string) {
  const l = doc.layers.find((x) => x.id === id);
  if (!l) return;
  l.name = name;
  doc.isModified = true;
}

function addReferenceLayer(
  image: ImageData,
  name: string,
  centerX: number,
  centerY: number,
) {
  if (doc.layers.length >= appSettings.maxLayers) return;
  const layer: Layer = {
    id: crypto.randomUUID(),
    index: doc.layers.length,
    name,
    isVisible: true,
    isLocked: false,
    opacity: 1,
    isReference: true,
    image,
    refX: centerX - image.width / 2,
    refY: centerY - image.height / 2,
    refWidth: image.width,
    refHeight: image.height,
  };
  // New layers land on top (index 0), so a dropped reference image is immediately visible.
  const atIndex = 0;
  doc.layers.unshift(layer);
  reindexLayers(doc.layers);
  undoStack.push({ kind: "layerAdd", layer, atIndex });
  activeLayerId = layer.id;
  doc.isModified = true;
  recomputeFullComposite();
  refreshAll();
  render();
}

// ---------------------------------------------------------------------------
// Document lifecycle
// ---------------------------------------------------------------------------

function applyDefaultPaletteToDoc(target: PixPenDocument) {
  const preset = loadPalettePreset(
    appSettings.defaultPaletteName ?? BUILTIN_PALETTE_NAME,
  );
  target.palette = {
    colors: [...preset.colors],
    foregroundColor: preset.foregroundColor,
    backgroundColor: preset.backgroundColor,
  };
}

function loadNewDocument(newDoc: PixPenDocument) {
  doc = newDoc;
  activeLayerId = doc.layers[0]?.id ?? "";
  activePenIndex = 0;
  selection = emptySelection();
  floatingOverlay = null;
  overlayCanvas = null;
  undoStack = new UndoStack(() => appSettings.undoMemoryLimitMb * 1024 * 1024);
  offscreen.width = doc.width;
  offscreen.height = doc.height;
  recomputeFullComposite();
  view.fitToWindow(
    viewCanvas.width / dpr,
    viewCanvas.height / dpr,
    doc.width,
    doc.height,
  );
  refreshAll();
  render();
}

async function confirmDiscardIfModified(): Promise<boolean> {
  if (!doc.isModified) return true;
  return await showConfirm("Unsaved changes will be lost. Continue?");
}

async function doNew() {
  if (!(await confirmDiscardIfModified())) return;
  const newDoc = createDefaultDocument(
    appSettings.defaultWidth,
    appSettings.defaultHeight,
    appSettings.defaultDpi,
  );
  applyDefaultPaletteToDoc(newDoc);
  loadNewDocument(newDoc);
}

async function doOpen() {
  if (!(await confirmDiscardIfModified())) return;
  try {
    const opened = await pickAndOpenPpx();
    if (opened) loadNewDocument(opened);
  } catch (err) {
    alert(`Failed to open file: ${(err as Error).message}`);
  }
}

async function doSave(forceDialog: boolean) {
  try {
    await saveDocument(doc, forceDialog);
    refreshAll();
  } catch (err) {
    alert(`Failed to save file: ${(err as Error).message}`);
  }
}

async function doExport() {
  const choice = await showExportDialog();
  if (choice === "merged") await exportMergedPng(doc);
  else if (choice === "layers") await exportLayersAsZip(doc);
}

function resizeCanvas(newWidth: number, newHeight: number) {
  for (const layer of doc.layers) {
    if (layer.isReference) continue;
    const resized = new ImageData(newWidth, newHeight);
    const copyW = Math.min(layer.image.width, newWidth);
    const copyH = Math.min(layer.image.height, newHeight);
    for (let y = 0; y < copyH; y++) {
      const srcOff = y * layer.image.width * 4;
      const dstOff = y * newWidth * 4;
      resized.data.set(
        layer.image.data.subarray(srcOff, srcOff + copyW * 4),
        dstOff,
      );
    }
    layer.image = resized;
  }
  doc.width = newWidth;
  doc.height = newHeight;
  undoStack.clear(); // old snapshots no longer match layer dimensions, matching the original app's behavior
  offscreen.width = newWidth;
  offscreen.height = newHeight;
  recomputeFullComposite();
}

async function doDocumentSettings() {
  const result = await showDocumentSettingsDialog({
    width: doc.width,
    height: doc.height,
    dpi: doc.dpi,
    title: doc.title,
  });
  if (!result) return;
  doc.title = result.title;
  doc.dpi = result.dpi;
  if (result.width !== doc.width || result.height !== doc.height) {
    resizeCanvas(result.width, result.height);
  }
  doc.isModified = true;
  refreshAll();
  render();
}

async function doGridSettings() {
  const result = await showGridSettingsDialog(doc.grid);
  if (!result) return;
  doc.grid = result;
  doc.isModified = true;
  refreshUndoButtons();
  render();
}

async function doAppSettings() {
  const result = await showAppSettingsDialog(appSettings);
  if (!result) return;
  appSettings = result;
  saveAppSettings(appSettings);
  applyTheme(appSettings.theme);
}

function applyTheme(theme: AppSettings["theme"]) {
  if (theme === "System") delete document.documentElement.dataset.theme;
  else document.documentElement.dataset.theme = theme.toLowerCase();
}

// ---------------------------------------------------------------------------
// Drag & drop
// ---------------------------------------------------------------------------

canvasArea.addEventListener("dragover", (e) => e.preventDefault());
canvasArea.addEventListener("drop", async (e) => {
  e.preventDefault();
  const file = e.dataTransfer?.files?.[0];
  if (!file) return;

  if (file.name.toLowerCase().endsWith(".ppx")) {
    if (!(await confirmDiscardIfModified())) return;
    try {
      loadNewDocument(await openPpxFromFile(file));
    } catch (err) {
      alert(`Failed to open file: ${(err as Error).message}`);
    }
    return;
  }

  if (file.type.startsWith("image/")) {
    const bytes = new Uint8Array(await file.arrayBuffer());
    const image = await pngBytesToImageData(bytes);
    const r = canvasArea.getBoundingClientRect();
    const dropCanvasPoint = view.screenToCanvas({
      x: e.clientX - r.left,
      y: e.clientY - r.top,
    });
    addReferenceLayer(
      image,
      file.name.replace(/\.[^./]+$/, ""),
      dropCanvasPoint.x,
      dropCanvasPoint.y,
    );
  }
});

// ---------------------------------------------------------------------------
// UI wiring
// ---------------------------------------------------------------------------

function setActiveTool(kind: ToolKind) {
  activeToolKind = kind;
  refreshUndoButtons();
}

const toolbar = createToolbar({
  onToolChange: setActiveTool,
  onUndo: doUndo,
  onRedo: doRedo,
  onSave: () => doSave(false),
  onNew: doNew,
  onOpen: doOpen,
  onToggleGrid: () => {
    doc.grid.small.visible = !doc.grid.small.visible;
    refreshUndoButtons();
    render();
  },
  onDocumentSettings: doDocumentSettings,
  onGridSettings: doGridSettings,
  onAppSettings: doAppSettings,
  onExport: doExport,
});
toolbarSlot.append(toolbar.element);

const layerPanel = createLayerPanel({
  onSelect: (id) => {
    activeLayerId = id;
    refreshAll();
    render();
  },
  onToggleVisible: (id) =>
    setLayerVisible(id, !doc.layers.find((l) => l.id === id)!.isVisible),
  onToggleLock: (id) =>
    setLayerLocked(id, !doc.layers.find((l) => l.id === id)!.isLocked),
  onOpacityChange: setLayerOpacity,
  onRename: renameLayer,
  onAdd: addLayer,
  onDelete: deleteLayer,
  onDuplicate: duplicateLayer,
  onMoveUp: (id) => moveLayer(id, "up"),
  onMoveDown: (id) => moveLayer(id, "down"),
});
leftPanel.append(layerPanel.element);

const colorPanel = createColorPanel({
  onForegroundChange: () => (doc.isModified = true),
  onBackgroundChange: () => (doc.isModified = true),
  onSwap: () => {
    const t = doc.palette.foregroundColor;
    doc.palette.foregroundColor = doc.palette.backgroundColor;
    doc.palette.backgroundColor = t;
    refreshColorPanel();
  },
  onAddSwatch: (color) => {
    doc.palette.colors.push(color);
    refreshColorPanel();
  },
  onRemoveSwatch: (i) => {
    doc.palette.colors.splice(i, 1);
    refreshColorPanel();
  },
  onPresetSelect: (name) => {
    const p = loadPalettePreset(name);
    doc.palette = {
      colors: [...p.colors],
      foregroundColor: p.foregroundColor,
      backgroundColor: p.backgroundColor,
    };
    refreshColorPanel();
  },
  onPresetSave: (name) => {
    savePalettePreset({
      name,
      colors: doc.palette.colors,
      foregroundColor: doc.palette.foregroundColor,
      backgroundColor: doc.palette.backgroundColor,
    });
    refreshColorPanel();
  },
  onPresetDelete: (name) => {
    deletePalettePreset(name);
    refreshColorPanel();
  },
  onPresetSetDefault: (name) => {
    appSettings.defaultPaletteName = name;
    saveAppSettings(appSettings);
  },
  getPresetNames: listPaletteNames,
});
rightPanel.append(colorPanel.element);

const penPanel = createPenPanel({
  onSelect: (i) => {
    activePenIndex = i;
    refreshPenPanel();
  },
  onAdd: () => {
    doc.pens.push({
      ...createDefaultPens()[0],
      name: `Pen ${doc.pens.length + 1}`,
    });
    activePenIndex = doc.pens.length - 1;
    refreshPenPanel();
  },
  onDelete: (i) => {
    if (doc.pens.length <= 1) return;
    doc.pens.splice(i, 1);
    if (activePenIndex >= doc.pens.length) activePenIndex = doc.pens.length - 1;
    refreshPenPanel();
  },
  onChange: (i, pen) => {
    doc.pens[i] = pen;
    refreshPenPanel();
  },
});
rightPanel.append(penPanel.element);

function refreshLayerPanel() {
  layerPanel.update(doc, activeLayerId);
}
function refreshColorPanel() {
  colorPanel.update(doc.palette);
}
function refreshPenPanel() {
  penPanel.update(doc.pens, activePenIndex);
}
function refreshStatusBar() {
  statusBar.textContent = `${doc.width} × ${doc.height}px · ${
    Math.round(view.zoom * 100)
  }% · ${activeToolKind}${doc.isModified ? " · modified" : ""}`;
}
function refreshUndoButtons() {
  toolbar.update({
    activeTool: activeToolKind,
    canUndo: undoStack.canUndo,
    canRedo: undoStack.canRedo,
    gridVisible: doc.grid.small.visible,
    zoomPercent: view.zoom * 100,
  });
  refreshStatusBar();
}
function refreshAll() {
  ensureActiveLayerValid();
  refreshLayerPanel();
  refreshColorPanel();
  refreshPenPanel();
  refreshUndoButtons();
  document.title = `PixPen${doc.isModified ? " *" : ""} - ${doc.title}`;
}

// ---------------------------------------------------------------------------
// Bootstrap
// ---------------------------------------------------------------------------

resizeViewCanvas();
view.fitToWindow(
  viewCanvas.width / dpr,
  viewCanvas.height / dpr,
  doc.width,
  doc.height,
);
refreshAll();
render();
