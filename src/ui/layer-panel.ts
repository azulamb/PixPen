import type { Layer, PixPenDocument } from "../types.ts";
import { el } from "./dom.ts";

export interface LayerPanelHandlers {
  onSelect(layerId: string): void;
  onToggleVisible(layerId: string): void;
  onToggleLock(layerId: string): void;
  onOpacityChange(layerId: string, opacity: number): void;
  onRename(layerId: string, name: string): void;
  onAdd(): void;
  onDelete(layerId: string): void;
  onDuplicate(layerId: string): void;
  onMoveUp(layerId: string): void;
  onMoveDown(layerId: string): void;
}

export interface LayerPanelHandle {
  element: HTMLElement;
  update(doc: PixPenDocument, activeLayerId: string): void;
}

export function createLayerPanel(
  handlers: LayerPanelHandlers,
): LayerPanelHandle {
  const list = el("div", { className: "layer-list" });
  const addBtn = el("button", {
    className: "icon-btn",
    title: "Add layer",
    text: "+",
    onclick: handlers.onAdd,
  });
  const element = el("div", { className: "panel layer-panel" }, [
    el("div", { className: "panel-header" }, ["Layers", addBtn]),
    list,
  ]);

  function renderRow(layer: Layer, isActive: boolean): HTMLElement {
    const visBtn = el("button", {
      className: "layer-icon-btn",
      title: "Toggle visibility",
      text: layer.isVisible ? "\u{1f441}" : "\u{2013}",
      onclick: (ev: Event) => {
        ev.stopPropagation();
        handlers.onToggleVisible(layer.id);
      },
    });
    const lockBtn = el("button", {
      className: "layer-icon-btn",
      title: "Toggle lock",
      text: layer.isLocked ? "\u{1f512}" : "\u{1f513}",
      onclick: (ev: Event) => {
        ev.stopPropagation();
        handlers.onToggleLock(layer.id);
      },
    });
    const nameInput = el("input", {
      className: "layer-name",
      type: "text",
      value: layer.name,
      onchange: (ev: Event) =>
        handlers.onRename(layer.id, (ev.target as HTMLInputElement).value),
    });
    const opacityInput = el("input", {
      className: "layer-opacity",
      type: "number",
      min: "0",
      max: "100",
      value: String(Math.round(layer.opacity * 100)),
      title: "Opacity %",
      onchange: (ev: Event) => {
        const v = Number((ev.target as HTMLInputElement).value) / 100;
        handlers.onOpacityChange(layer.id, Math.max(0, Math.min(1, v)));
      },
    });
    const refBadge = layer.isReference
      ? el("span", {
        className: "ref-badge",
        text: "REF",
        title: "Reference layer",
      })
      : "";

    const row = el(
      "div",
      {
        className: `layer-row${isActive ? " active" : ""}`,
        onclick: () => handlers.onSelect(layer.id),
      },
      [
        visBtn,
        lockBtn,
        nameInput,
        refBadge,
        opacityInput,
        el("button", {
          className: "layer-icon-btn",
          title: "Up",
          text: "↑",
          onclick: (ev: Event) => {
            ev.stopPropagation();
            handlers.onMoveUp(layer.id);
          },
        }),
        el("button", {
          className: "layer-icon-btn",
          title: "Down",
          text: "↓",
          onclick: (ev: Event) => {
            ev.stopPropagation();
            handlers.onMoveDown(layer.id);
          },
        }),
        el("button", {
          className: "layer-icon-btn",
          title: "Duplicate",
          text: "⧉",
          onclick: (ev: Event) => {
            ev.stopPropagation();
            handlers.onDuplicate(layer.id);
          },
        }),
        el("button", {
          className: "layer-icon-btn danger",
          title: "Delete",
          text: "×",
          onclick: (ev: Event) => {
            ev.stopPropagation();
            handlers.onDelete(layer.id);
          },
        }),
      ],
    );
    return row;
  }

  return {
    element,
    update(doc, activeLayerId) {
      list.replaceChildren();
      // Top layer (index 0) shown first, matching common paint-app conventions and the WPF app's panel.
      const ordered = [...doc.layers].sort((a, b) => a.index - b.index);
      for (const layer of ordered) {
        list.append(renderRow(layer, layer.id === activeLayerId));
      }
    },
  };
}
