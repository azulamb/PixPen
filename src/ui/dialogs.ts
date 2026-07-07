import type { AppSettings } from "../settings/app-settings.ts";
import type { GridSettings, SingleGridSettings } from "../types.ts";
import { el } from "./dom.ts";

function openModal(
  title: string,
  body: HTMLElement[],
  buttons: { label: string; value: string; primary?: boolean }[],
): Promise<string | null> {
  return new Promise((resolve) => {
    const dialog = el("dialog", { className: "app-dialog" });
    let resolved = false;
    const finish = (value: string | null) => {
      if (resolved) return;
      resolved = true;
      resolve(value);
      dialog.close();
      dialog.remove();
    };
    const buttonRow = el(
      "div",
      { className: "dialog-buttons" },
      buttons.map((b) =>
        el("button", {
          className: b.primary ? "primary" : "",
          text: b.label,
          onclick: () => finish(b.value),
        })
      ),
    );
    dialog.append(el("h3", { text: title }), ...body, buttonRow);
    dialog.addEventListener("cancel", () => finish(null));
    document.body.append(dialog);
    dialog.showModal();
  });
}

export function showConfirm(message: string): Promise<boolean> {
  return openModal("Confirm", [el("p", { text: message })], [
    { label: "Cancel", value: "cancel" },
    { label: "OK", value: "ok", primary: true },
  ]).then((v) => v === "ok");
}

export interface DocumentSettingsResult {
  width: number;
  height: number;
  dpi: number;
  title: string;
}

export function showDocumentSettingsDialog(
  current: DocumentSettingsResult,
): Promise<DocumentSettingsResult | null> {
  const titleInput = el("input", {
    type: "text",
    value: current.title,
  }) as HTMLInputElement;
  const widthInput = el("input", {
    type: "number",
    min: "1",
    max: "32767",
    value: String(current.width),
  }) as HTMLInputElement;
  const heightInput = el("input", {
    type: "number",
    min: "1",
    max: "32767",
    value: String(current.height),
  }) as HTMLInputElement;
  const dpiInput = el("input", {
    type: "number",
    min: "1",
    value: String(current.dpi),
  }) as HTMLInputElement;

  const body = [
    el("div", { className: "field-row" }, [
      el("label", { text: "Title" }),
      titleInput,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "Width" }),
      widthInput,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "Height" }),
      heightInput,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "DPI" }),
      dpiInput,
    ]),
  ];

  return openModal("Document Settings", body, [
    { label: "Cancel", value: "cancel" },
    { label: "OK", value: "ok", primary: true },
  ]).then((v) => {
    if (v !== "ok") return null;
    return {
      title: titleInput.value || "untitled",
      width: Math.max(
        1,
        Math.min(32767, Number(widthInput.value) || current.width),
      ),
      height: Math.max(
        1,
        Math.min(32767, Number(heightInput.value) || current.height),
      ),
      dpi: Math.max(1, Number(dpiInput.value) || current.dpi),
    };
  });
}

function singleGridFields(label: string, g: SingleGridSettings) {
  const visible = el("input", {
    type: "checkbox",
    checked: g.visible,
  }) as HTMLInputElement;
  const offsetX = el("input", {
    type: "number",
    value: String(g.offsetX),
  }) as HTMLInputElement;
  const offsetY = el("input", {
    type: "number",
    value: String(g.offsetY),
  }) as HTMLInputElement;
  const spacingX = el("input", {
    type: "number",
    min: "1",
    value: String(g.spacingX),
  }) as HTMLInputElement;
  const spacingY = el("input", {
    type: "number",
    min: "1",
    value: String(g.spacingY),
  }) as HTMLInputElement;
  const color = el("input", { type: "color" }) as HTMLInputElement;
  color.value = `#${g.color.slice(3)}`;
  const lineType = el("select", {}, [
    el("option", { value: "Solid", text: "Solid" }),
    el("option", { value: "Dashed", text: "Dashed" }),
  ]) as HTMLSelectElement;
  lineType.value = g.lineType;

  const fieldset = el("fieldset", {}, [
    el("legend", { text: label }),
    el("div", { className: "field-row" }, [
      el("label", { text: "Visible" }),
      visible,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "Offset X/Y" }),
      offsetX,
      offsetY,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "Spacing X/Y" }),
      spacingX,
      spacingY,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "Color" }),
      color,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "Line" }),
      lineType,
    ]),
  ]);

  return {
    fieldset,
    read(): SingleGridSettings {
      return {
        visible: visible.checked,
        offsetX: Number(offsetX.value) || 0,
        offsetY: Number(offsetY.value) || 0,
        spacingX: Math.max(1, Number(spacingX.value) || 1),
        spacingY: Math.max(1, Number(spacingY.value) || 1),
        color: `#FF${color.value.slice(1).toUpperCase()}`,
        lineType: lineType.value === "Dashed" ? "Dashed" : "Solid",
      };
    },
  };
}

export function showGridSettingsDialog(
  current: GridSettings,
): Promise<GridSettings | null> {
  const small = singleGridFields("Small", current.small);
  const medium = singleGridFields("Medium", current.medium);
  const large = singleGridFields("Large", current.large);

  return openModal("Grid Settings", [
    small.fieldset,
    medium.fieldset,
    large.fieldset,
  ], [
    { label: "Cancel", value: "cancel" },
    { label: "OK", value: "ok", primary: true },
  ]).then((
    v,
  ) => (v === "ok"
    ? { small: small.read(), medium: medium.read(), large: large.read() }
    : null)
  );
}

export function showAppSettingsDialog(
  current: AppSettings,
): Promise<AppSettings | null> {
  const undoLimit = el("input", {
    type: "number",
    min: "16",
    value: String(current.undoMemoryLimitMb),
  }) as HTMLInputElement;
  const maxLayers = el("input", {
    type: "number",
    min: "1",
    max: "100",
    value: String(current.maxLayers),
  }) as HTMLInputElement;
  const defW = el("input", {
    type: "number",
    min: "1",
    value: String(current.defaultWidth),
  }) as HTMLInputElement;
  const defH = el("input", {
    type: "number",
    min: "1",
    value: String(current.defaultHeight),
  }) as HTMLInputElement;
  const defDpi = el("input", {
    type: "number",
    min: "1",
    value: String(current.defaultDpi),
  }) as HTMLInputElement;
  const theme = el("select", {}, [
    el("option", { value: "System", text: "System" }),
    el("option", { value: "Light", text: "Light" }),
    el("option", { value: "Dark", text: "Dark" }),
  ]) as HTMLSelectElement;
  theme.value = current.theme;

  const body = [
    el("div", { className: "field-row" }, [
      el("label", { text: "Undo memory limit (MB)" }),
      undoLimit,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "Max layers" }),
      maxLayers,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "Default width" }),
      defW,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "Default height" }),
      defH,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "Default DPI" }),
      defDpi,
    ]),
    el("div", { className: "field-row" }, [
      el("label", { text: "Theme" }),
      theme,
    ]),
  ];

  return openModal("App Settings", body, [
    { label: "Cancel", value: "cancel" },
    { label: "OK", value: "ok", primary: true },
  ]).then((v) => {
    if (v !== "ok") return null;
    return {
      ...current,
      undoMemoryLimitMb: Math.max(
        16,
        Number(undoLimit.value) || current.undoMemoryLimitMb,
      ),
      maxLayers: Math.max(
        1,
        Math.min(100, Number(maxLayers.value) || current.maxLayers),
      ),
      defaultWidth: Math.max(1, Number(defW.value) || current.defaultWidth),
      defaultHeight: Math.max(1, Number(defH.value) || current.defaultHeight),
      defaultDpi: Math.max(1, Number(defDpi.value) || current.defaultDpi),
      theme: (theme.value as AppSettings["theme"]) ?? current.theme,
    };
  });
}

export function showExportDialog(): Promise<"merged" | "layers" | null> {
  return openModal("Export", [el("p", { text: "Choose export format:" })], [
    { label: "Cancel", value: "cancel" },
    { label: "Layers (ZIP)", value: "layers" },
    { label: "Merged PNG", value: "merged", primary: true },
  ]).then((v) => (v === "merged" || v === "layers" ? v : null));
}
