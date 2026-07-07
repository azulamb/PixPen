import type { Viewport } from "../canvas/view.ts";
import type { StrokePoint } from "../tools/types.ts";

/** Normalizes a PointerEvent into canvas-space coordinates + pressure + eraser-tip detection.
 * Replaces the WPF app's 4-source arbitration (WM_POINTER / WinTab / WPF Stylus / mouse) entirely —
 * the Pointer Events API already unifies these across input devices. */
export function pointerEventToStrokePoint(
  e: PointerEvent,
  canvasEl: HTMLElement,
  view: Viewport,
): StrokePoint {
  const rect = canvasEl.getBoundingClientRect();
  const screenX = e.clientX - rect.left;
  const screenY = e.clientY - rect.top;
  const canvasPoint = view.screenToCanvas({ x: screenX, y: screenY });

  // Per the Pointer Events spec, `buttons` bit 0x20 indicates the stylus eraser tip/button.
  const isEraser = e.pointerType === "pen" && (e.buttons & 0x20) !== 0;

  let pressure = e.pressure;
  if (e.pointerType === "mouse") {
    pressure = 1;
  } else if (pressure <= 0) {
    pressure = 0.5; // device without pressure hardware, or a transient 0 reading
  }

  return { x: canvasPoint.x, y: canvasPoint.y, pressure, isEraser };
}
