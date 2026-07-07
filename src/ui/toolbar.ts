import { el } from "./dom.ts";

export type ToolKind = "Pen" | "Eraser" | "Fill" | "Eyedropper" | "Selection";

const TOOLS: { kind: ToolKind; label: string; icon: string; key: string }[] = [
  { kind: "Pen", label: "Pen", icon: "✎", key: "P" },
  { kind: "Eraser", label: "Eraser", icon: "⬚", key: "E" },
  { kind: "Fill", label: "Fill", icon: "█", key: "F" },
  { kind: "Eyedropper", label: "Eyedropper", icon: "⬡", key: "I" },
  { kind: "Selection", label: "Selection", icon: "⬚︎", key: "S" },
];

export interface ToolbarHandlers {
  onToolChange(tool: ToolKind): void;
  onUndo(): void;
  onRedo(): void;
  onSave(): void;
  onNew(): void;
  onOpen(): void;
  onToggleGrid(): void;
  onDocumentSettings(): void;
  onGridSettings(): void;
  onAppSettings(): void;
  onExport(): void;
}

export interface ToolbarHandle {
  element: HTMLElement;
  update(
    state: {
      activeTool: ToolKind;
      canUndo: boolean;
      canRedo: boolean;
      gridVisible: boolean;
      zoomPercent: number;
    },
  ): void;
}

export function createToolbar(handlers: ToolbarHandlers): ToolbarHandle {
  const toolButtons = new Map<ToolKind, HTMLButtonElement>();

  const toolGroup = el("div", { className: "tool-group" });
  for (const t of TOOLS) {
    const btn = el("button", {
      className: "tool-btn",
      title: `${t.label} (${t.key})`,
      text: t.icon,
      onclick: () => handlers.onToolChange(t.kind),
    });
    toolButtons.set(t.kind, btn);
    toolGroup.append(btn);
  }

  const undoBtn = el("button", {
    className: "icon-btn",
    title: "Undo (Ctrl+Z)",
    text: "↶",
    onclick: handlers.onUndo,
  });
  const redoBtn = el("button", {
    className: "icon-btn",
    title: "Redo (Ctrl+Y)",
    text: "↷",
    onclick: handlers.onRedo,
  });
  const newBtn = el("button", {
    className: "icon-btn",
    title: "New (Ctrl+N)",
    text: "➕",
    onclick: handlers.onNew,
  });
  const openBtn = el("button", {
    className: "icon-btn",
    title: "Open (Ctrl+O)",
    text: "\u{1f4c2}",
    onclick: handlers.onOpen,
  });
  const saveBtn = el("button", {
    className: "icon-btn",
    title: "Save (Ctrl+S)",
    text: "\u{1f4be}",
    onclick: handlers.onSave,
  });
  const gridBtn = el("button", {
    className: "icon-btn",
    title: "Toggle grid",
    text: "#",
    onclick: handlers.onToggleGrid,
  });
  const docSettingsBtn = el("button", {
    className: "icon-btn",
    title: "Document settings",
    text: "\u{1f4c4}",
    onclick: handlers.onDocumentSettings,
  });
  const gridSettingsBtn = el("button", {
    className: "icon-btn",
    title: "Grid settings",
    text: "\u{25a6}",
    onclick: handlers.onGridSettings,
  });
  const exportBtn = el("button", {
    className: "icon-btn",
    title: "Export",
    text: "\u{2b07}",
    onclick: handlers.onExport,
  });
  const appSettingsBtn = el("button", {
    className: "icon-btn",
    title: "App settings",
    text: "\u{2699}",
    onclick: handlers.onAppSettings,
  });
  const zoomLabel = el("span", { className: "zoom-label" });

  const element = el("div", { className: "toolbar" }, [
    newBtn,
    openBtn,
    saveBtn,
    exportBtn,
    el("span", { className: "sep" }),
    undoBtn,
    redoBtn,
    el("span", { className: "sep" }),
    toolGroup,
    el("span", { className: "sep" }),
    gridBtn,
    docSettingsBtn,
    gridSettingsBtn,
    appSettingsBtn,
    zoomLabel,
  ]);

  return {
    element,
    update(state) {
      for (const [kind, btn] of toolButtons) {
        btn.classList.toggle("active", kind === state.activeTool);
      }
      undoBtn.toggleAttribute("disabled", !state.canUndo);
      redoBtn.toggleAttribute("disabled", !state.canRedo);
      gridBtn.classList.toggle("active", state.gridVisible);
      zoomLabel.textContent = `${Math.round(state.zoomPercent)}%`;
    },
  };
}
