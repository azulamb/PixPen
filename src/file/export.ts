import type { PixPenDocument } from "../types.ts";
import { compositeAll } from "../canvas/compositor.ts";
import { imageDataToPngBytes } from "./png.ts";
import { buildStoredZip } from "./zip-writer.ts";
import { downloadBlob, sanitizeFileName } from "./download.ts";

/** Single flattened PNG of all visible layers composited together. */
export async function exportMergedPng(doc: PixPenDocument) {
  const composite = compositeAll(doc);
  const bytes = await imageDataToPngBytes(composite);
  downloadBlob(
    new Blob([bytes], { type: "image/png" }),
    `${doc.title || "untitled"}.png`,
  );
}

/** Each visible layer as its own PNG, bundled into a ZIP (replaces the WPF app's "export to folder", since
 * static web pages have no folder-write access). */
export async function exportLayersAsZip(doc: PixPenDocument) {
  const ordered = [...doc.layers].sort((a, b) => a.index - b.index).filter((
    l,
  ) => l.isVisible);
  const entries = [];
  for (const layer of ordered) {
    const bytes = await imageDataToPngBytes(layer.image);
    const idx = String(layer.index).padStart(4, "0");
    entries.push({
      name: `${idx}_${sanitizeFileName(layer.name)}.png`,
      data: bytes,
    });
  }
  const zipBytes = buildStoredZip(entries);
  downloadBlob(
    new Blob([zipBytes], { type: "application/zip" }),
    `${doc.title || "untitled"}_layers.zip`,
  );
}
