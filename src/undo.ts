import type { Layer, PixPenDocument } from "./types.ts";
import type { Rect } from "./canvas/rect.ts";

/** Dirty-rect-based undo/redo. Unlike the original WPF app (whole-layer snapshots per stroke), only the
 * touched rectangle's before/after bytes are kept, which is far more memory-efficient for large canvases. */

interface StrokeAction {
  kind: "stroke";
  layerId: string;
  rect: Rect;
  before: Uint8ClampedArray<ArrayBuffer>;
  after: Uint8ClampedArray<ArrayBuffer>;
}

interface LayerAddAction {
  kind: "layerAdd";
  layer: Layer;
  atIndex: number;
}

interface LayerRemoveAction {
  kind: "layerRemove";
  layer: Layer;
  atIndex: number;
}

interface LayerReorderAction {
  kind: "layerReorder";
  fromIndex: number;
  toIndex: number;
}

interface RefTransformAction {
  kind: "refTransform";
  layerId: string;
  before: { refX: number; refY: number; refWidth: number; refHeight: number };
  after: { refX: number; refY: number; refWidth: number; refHeight: number };
}

export type UndoAction =
  | StrokeAction
  | LayerAddAction
  | LayerRemoveAction
  | LayerReorderAction
  | RefTransformAction;

function actionByteSize(action: UndoAction): number {
  if (action.kind === "stroke") {
    return action.before.length + action.after.length;
  }
  if (action.kind === "layerAdd" || action.kind === "layerRemove") {
    return action.layer.image.data.length;
  }
  return 64;
}

function readRect(
  image: ImageData,
  rect: Rect,
): Uint8ClampedArray<ArrayBuffer> {
  const out = new Uint8ClampedArray(rect.width * rect.height * 4);
  for (let y = 0; y < rect.height; y++) {
    const srcY = rect.y + y;
    if (srcY < 0 || srcY >= image.height) continue;
    const srcOffset = (srcY * image.width + rect.x) * 4;
    const dstOffset = y * rect.width * 4;
    const rowBytes = rect.width * 4;
    out.set(image.data.subarray(srcOffset, srcOffset + rowBytes), dstOffset);
  }
  return out;
}

function writeRect(image: ImageData, rect: Rect, bytes: Uint8ClampedArray) {
  for (let y = 0; y < rect.height; y++) {
    const dstY = rect.y + y;
    if (dstY < 0 || dstY >= image.height) continue;
    const dstOffset = (dstY * image.width + rect.x) * 4;
    const srcOffset = y * rect.width * 4;
    const rowBytes = rect.width * 4;
    image.data.set(bytes.subarray(srcOffset, srcOffset + rowBytes), dstOffset);
  }
}

/** Call before mutating a layer's pixels; pass the returned snapshot + the tool's dirty rect to `undoStack.pushStroke`. */
export function snapshotRect(
  layer: Layer,
  rect: Rect,
): Uint8ClampedArray<ArrayBuffer> {
  return readRect(layer.image, rect);
}

export function reindexLayers(layers: Layer[]) {
  layers.forEach((l, i) => (l.index = i));
}

export class UndoStack {
  private undoList: UndoAction[] = [];
  private redoList: UndoAction[] = [];
  private memoryBytes = 0;

  constructor(private getMemoryLimitBytes: () => number) {}

  get canUndo(): boolean {
    return this.undoList.length > 0;
  }

  get canRedo(): boolean {
    return this.redoList.length > 0;
  }

  push(action: UndoAction) {
    this.undoList.push(action);
    this.redoList = [];
    this.memoryBytes += actionByteSize(action);
    this.trim();
  }

  pushStroke(layer: Layer, rect: Rect, before: Uint8ClampedArray<ArrayBuffer>) {
    if (rect.width <= 0 || rect.height <= 0) return;
    const after = readRect(layer.image, rect);
    this.push({ kind: "stroke", layerId: layer.id, rect, before, after });
  }

  clear() {
    this.undoList = [];
    this.redoList = [];
    this.memoryBytes = 0;
  }

  private trim() {
    const limit = this.getMemoryLimitBytes();
    while (this.memoryBytes > limit && this.undoList.length > 1) {
      const evicted = this.undoList.shift()!;
      this.memoryBytes -= actionByteSize(evicted);
    }
  }

  /** Applies the inverse of `action` (undo=true) or re-applies it (undo=false) to `doc`. Returns the dirty rect, if any. */
  private apply(
    doc: PixPenDocument,
    action: UndoAction,
    isUndo: boolean,
  ): Rect | null {
    switch (action.kind) {
      case "stroke": {
        const layer = doc.layers.find((l) => l.id === action.layerId);
        if (!layer) return null;
        writeRect(
          layer.image,
          action.rect,
          isUndo ? action.before : action.after,
        );
        return action.rect;
      }
      case "layerAdd": {
        if (isUndo) {
          doc.layers.splice(action.atIndex, 1);
        } else {
          doc.layers.splice(action.atIndex, 0, action.layer);
        }
        reindexLayers(doc.layers);
        return null;
      }
      case "layerRemove": {
        if (isUndo) {
          doc.layers.splice(action.atIndex, 0, action.layer);
        } else {
          doc.layers.splice(action.atIndex, 1);
        }
        reindexLayers(doc.layers);
        return null;
      }
      case "layerReorder": {
        const from = isUndo ? action.toIndex : action.fromIndex;
        const to = isUndo ? action.fromIndex : action.toIndex;
        const [moved] = doc.layers.splice(from, 1);
        doc.layers.splice(to, 0, moved);
        reindexLayers(doc.layers);
        return null;
      }
      case "refTransform": {
        const layer = doc.layers.find((l) => l.id === action.layerId);
        if (!layer) return null;
        const v = isUndo ? action.before : action.after;
        layer.refX = v.refX;
        layer.refY = v.refY;
        layer.refWidth = v.refWidth;
        layer.refHeight = v.refHeight;
        return null;
      }
    }
  }

  undo(doc: PixPenDocument): Rect | null {
    const action = this.undoList.pop();
    if (!action) return null;
    const rect = this.apply(doc, action, true);
    this.redoList.push(action);
    return rect;
  }

  redo(doc: PixPenDocument): Rect | null {
    const action = this.redoList.pop();
    if (!action) return null;
    const rect = this.apply(doc, action, false);
    this.undoList.push(action);
    return rect;
  }
}
