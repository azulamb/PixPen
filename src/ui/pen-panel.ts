import type { PenDefinition } from "../types.ts";
import { el } from "./dom.ts";

export interface PenPanelHandlers {
  onSelect(index: number): void;
  onAdd(): void;
  onDelete(index: number): void;
  onChange(index: number, pen: PenDefinition): void;
}

export interface PenPanelHandle {
  element: HTMLElement;
  update(pens: PenDefinition[], activeIndex: number): void;
}

export function createPenPanel(handlers: PenPanelHandlers): PenPanelHandle {
  const list = el("div", { className: "pen-list" });
  const editor = el("div", { className: "pen-editor" });
  const addBtn = el("button", {
    className: "icon-btn",
    text: "+",
    title: "Add pen",
    onclick: handlers.onAdd,
  });

  const element = el("div", { className: "panel pen-panel" }, [
    el("div", { className: "panel-header" }, ["Pens", addBtn]),
    list,
    editor,
  ]);

  function renderEditor(pen: PenDefinition, index: number) {
    editor.replaceChildren();

    const nameInput = el("input", {
      type: "text",
      value: pen.name,
      onchange: (ev: Event) =>
        handlers.onChange(index, {
          ...pen,
          name: (ev.target as HTMLInputElement).value,
        }),
    });
    const shapeSelect = el("select", {}, [
      el("option", { value: "Round", text: "Round" }),
      el("option", { value: "Square", text: "Square" }),
    ]) as HTMLSelectElement;
    shapeSelect.value = pen.shape;
    shapeSelect.addEventListener(
      "change",
      () =>
        handlers.onChange(index, {
          ...pen,
          shape: shapeSelect.value === "Square" ? "Square" : "Round",
        }),
    );

    const sizeInput = el("input", {
      type: "number",
      min: "1",
      max: "500",
      value: String(pen.size),
      onchange: (ev: Event) =>
        handlers.onChange(index, {
          ...pen,
          size: Number((ev.target as HTMLInputElement).value),
        }),
    });
    const opacityInput = el("input", {
      type: "range",
      min: "0",
      max: "100",
      value: String(Math.round(pen.opacity * 100)),
      oninput: (ev: Event) =>
        handlers.onChange(index, {
          ...pen,
          opacity: Number((ev.target as HTMLInputElement).value) / 100,
        }),
    });
    const pressureSizeInput = el("input", {
      type: "checkbox",
      checked: pen.pressureAffectsSize,
      onchange: (ev: Event) =>
        handlers.onChange(index, {
          ...pen,
          pressureAffectsSize: (ev.target as HTMLInputElement).checked,
        }),
    });
    const pressureOpacityInput = el("input", {
      type: "checkbox",
      checked: pen.pressureAffectsOpacity,
      onchange: (ev: Event) =>
        handlers.onChange(index, {
          ...pen,
          pressureAffectsOpacity: (ev.target as HTMLInputElement).checked,
        }),
    });
    const minSizeInput = el("input", {
      type: "range",
      min: "0",
      max: "100",
      value: String(Math.round(pen.minSizeFactor * 100)),
      oninput: (ev: Event) =>
        handlers.onChange(index, {
          ...pen,
          minSizeFactor: Number((ev.target as HTMLInputElement).value) / 100,
        }),
    });

    editor.append(
      el("div", { className: "field-row" }, [
        el("label", { text: "Name" }),
        nameInput,
      ]),
      el("div", { className: "field-row" }, [
        el("label", { text: "Shape" }),
        shapeSelect,
      ]),
      el("div", { className: "field-row" }, [
        el("label", { text: "Size" }),
        sizeInput,
      ]),
      el("div", { className: "field-row" }, [
        el("label", { text: "Opacity" }),
        opacityInput,
      ]),
      el("div", { className: "field-row" }, [
        el("label", { text: "Pressure→Size" }),
        pressureSizeInput,
      ]),
      el("div", { className: "field-row" }, [
        el("label", { text: "Pressure→Opacity" }),
        pressureOpacityInput,
      ]),
      el("div", { className: "field-row" }, [
        el("label", { text: "Min size %" }),
        minSizeInput,
      ]),
    );
  }

  return {
    element,
    update(pens, activeIndex) {
      list.replaceChildren();
      pens.forEach((pen, i) => {
        const row = el(
          "div",
          {
            className: `pen-row${i === activeIndex ? " active" : ""}`,
            onclick: () => handlers.onSelect(i),
          },
          [
            el("span", { text: pen.name }),
            el("button", {
              className: "layer-icon-btn danger",
              text: "×",
              title: "Delete",
              onclick: (ev: Event) => {
                ev.stopPropagation();
                handlers.onDelete(i);
              },
            }),
          ],
        );
        list.append(row);
      });
      if (pens[activeIndex]) renderEditor(pens[activeIndex], activeIndex);
    },
  };
}
