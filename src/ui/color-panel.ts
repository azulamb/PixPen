import type { ColorPalette } from "../types.ts";
import {
  argbToCss,
  formatArgb,
  hsvToRgb,
  parseArgb,
  parseFlexibleHex,
  rgbToHsv,
} from "../color.ts";
import { el } from "./dom.ts";

export interface ColorPanelHandlers {
  onForegroundChange(argb: string): void;
  onBackgroundChange(argb: string): void;
  onSwap(): void;
  onAddSwatch(color: string): void;
  onRemoveSwatch(index: number): void;
  onPresetSelect(name: string): void;
  onPresetSave(name: string): void;
  onPresetDelete(name: string): void;
  onPresetSetDefault(name: string): void;
  getPresetNames(): string[];
}

export interface ColorPanelHandle {
  element: HTMLElement;
  update(palette: ColorPalette): void;
}

export function createColorPanel(
  handlers: ColorPanelHandlers,
): ColorPanelHandle {
  let activeSlot: "fg" | "bg" = "fg";
  let palette: ColorPalette | null = null;

  const fgSwatch = el("button", {
    className: "fgbg-swatch fg",
    title: "Foreground",
  });
  const bgSwatch = el("button", {
    className: "fgbg-swatch bg",
    title: "Background",
  });
  const swapBtn = el("button", {
    className: "icon-btn",
    text: "⇄",
    title: "Swap",
    onclick: () => handlers.onSwap(),
  });

  const hexInput = el("input", { className: "hex-input", type: "text" });

  function makeSlider(
    label: string,
    min: number,
    max: number,
  ): { row: HTMLElement; input: HTMLInputElement; value: HTMLElement } {
    const input = el("input", {
      type: "range",
      min: String(min),
      max: String(max),
    }) as HTMLInputElement;
    const value = el("span", { className: "slider-value" });
    const row = el("div", { className: "slider-row" }, [
      el("label", { text: label }),
      input,
      value,
    ]);
    return { row, input, value };
  }

  const rSlider = makeSlider("R", 0, 255);
  const gSlider = makeSlider("G", 0, 255);
  const bSlider = makeSlider("B", 0, 255);
  const aSlider = makeSlider("A", 0, 255);
  const hSlider = makeSlider("H", 0, 360);
  const sSlider = makeSlider("S", 0, 100);
  const vSlider = makeSlider("V", 0, 100);

  const swatchGrid = el("div", { className: "swatch-grid" });
  const addSwatchBtn = el("button", {
    className: "icon-btn",
    text: "+",
    title: "Add current color",
    onclick: () => {
      if (palette) handlers.onAddSwatch(activeColor());
    },
  });

  const presetSelect = el("select", {
    className: "preset-select",
  }) as HTMLSelectElement;
  presetSelect.addEventListener(
    "change",
    () => handlers.onPresetSelect(presetSelect.value),
  );
  const presetNameInput = el("input", {
    className: "preset-name-input",
    type: "text",
    placeholder: "preset name",
  }) as HTMLInputElement;
  const presetSaveBtn = el("button", {
    className: "icon-btn",
    text: "Save",
    onclick: () => {
      if (presetNameInput.value.trim()) {
        handlers.onPresetSave(presetNameInput.value.trim());
      }
    },
  });
  const presetDeleteBtn = el("button", {
    className: "icon-btn",
    text: "Delete",
    onclick: () => handlers.onPresetDelete(presetSelect.value),
  });
  const presetDefaultBtn = el("button", {
    className: "icon-btn",
    text: "Set default",
    onclick: () => handlers.onPresetSetDefault(presetSelect.value),
  });

  function activeColor(): string {
    return activeSlot === "fg"
      ? palette!.foregroundColor
      : palette!.backgroundColor;
  }

  function commitColor(argb: string) {
    if (!palette) return;
    if (activeSlot === "fg") {
      palette.foregroundColor = argb;
      handlers.onForegroundChange(argb);
    } else {
      palette.backgroundColor = argb;
      handlers.onBackgroundChange(argb);
    }
    refreshFromColor(argb);
  }

  function refreshFromColor(argb: string) {
    const c = parseArgb(argb);
    rSlider.input.value = String(c.r);
    gSlider.input.value = String(c.g);
    bSlider.input.value = String(c.b);
    aSlider.input.value = String(c.a);
    rSlider.value.textContent = String(c.r);
    gSlider.value.textContent = String(c.g);
    bSlider.value.textContent = String(c.b);
    aSlider.value.textContent = String(c.a);
    const hsv = rgbToHsv(c.r, c.g, c.b);
    hSlider.input.value = String(Math.round(hsv.h));
    sSlider.input.value = String(Math.round(hsv.s * 100));
    vSlider.input.value = String(Math.round(hsv.v * 100));
    hSlider.value.textContent = String(Math.round(hsv.h));
    sSlider.value.textContent = String(Math.round(hsv.s * 100));
    vSlider.value.textContent = String(Math.round(hsv.v * 100));
    hexInput.value = argb.replace("#", "");
    fgSwatch.style.background = palette
      ? argbToCss(palette.foregroundColor)
      : "";
    bgSwatch.style.background = palette
      ? argbToCss(palette.backgroundColor)
      : "";
    fgSwatch.classList.toggle("selected", activeSlot === "fg");
    bgSwatch.classList.toggle("selected", activeSlot === "bg");
  }

  function onRgbInput() {
    commitColor(
      formatArgb({
        r: Number(rSlider.input.value),
        g: Number(gSlider.input.value),
        b: Number(bSlider.input.value),
        a: Number(aSlider.input.value),
      }),
    );
  }
  rSlider.input.addEventListener("input", onRgbInput);
  gSlider.input.addEventListener("input", onRgbInput);
  bSlider.input.addEventListener("input", onRgbInput);
  aSlider.input.addEventListener("input", onRgbInput);

  function onHsvInput() {
    const rgb = hsvToRgb({
      h: Number(hSlider.input.value),
      s: Number(sSlider.input.value) / 100,
      v: Number(vSlider.input.value) / 100,
    });
    commitColor(
      formatArgb({
        r: rgb.r,
        g: rgb.g,
        b: rgb.b,
        a: Number(aSlider.input.value),
      }),
    );
  }
  hSlider.input.addEventListener("input", onHsvInput);
  sSlider.input.addEventListener("input", onHsvInput);
  vSlider.input.addEventListener("input", onHsvInput);

  hexInput.addEventListener("change", () => {
    const parsed = parseFlexibleHex(hexInput.value);
    if (parsed) commitColor(formatArgb(parsed));
  });

  fgSwatch.addEventListener("click", () => {
    activeSlot = "fg";
    if (palette) refreshFromColor(palette.foregroundColor);
  });
  bgSwatch.addEventListener("click", () => {
    activeSlot = "bg";
    if (palette) refreshFromColor(palette.backgroundColor);
  });

  const element = el("div", { className: "panel color-panel" }, [
    el("div", { className: "panel-header" }, ["Color"]),
    el("div", { className: "fgbg-row" }, [fgSwatch, bgSwatch, swapBtn]),
    hexInput,
    el("div", { className: "sliders" }, [
      rSlider.row,
      gSlider.row,
      bSlider.row,
      aSlider.row,
      hSlider.row,
      sSlider.row,
      vSlider.row,
    ]),
    el("div", { className: "swatch-header" }, ["Swatches", addSwatchBtn]),
    swatchGrid,
    el("div", { className: "preset-row" }, [
      presetSelect,
      presetDefaultBtn,
      presetDeleteBtn,
    ]),
    el("div", { className: "preset-row" }, [presetNameInput, presetSaveBtn]),
  ]);

  return {
    element,
    update(p: ColorPalette) {
      palette = p;
      refreshFromColor(activeColor());

      swatchGrid.replaceChildren();
      p.colors.forEach((color, i) => {
        const sw = el("button", {
          className: "swatch",
          title: color,
          onclick: () => commitColor(color),
        });
        sw.style.background = argbToCss(color);
        const removeBtn = el("span", {
          className: "swatch-remove",
          text: "×",
          onclick: (ev: Event) => {
            ev.stopPropagation();
            handlers.onRemoveSwatch(i);
          },
        });
        sw.append(removeBtn);
        swatchGrid.append(sw);
      });

      const names = handlers.getPresetNames();
      const currentSelection = presetSelect.value;
      presetSelect.replaceChildren(
        ...names.map((n) => el("option", { value: n, text: n })),
      );
      if (names.includes(currentSelection)) {
        presetSelect.value = currentSelection;
      }
    },
  };
}
