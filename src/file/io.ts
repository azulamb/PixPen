import type { PixPenDocument } from "../types.ts";
import { loadDocumentFromPpx, saveDocumentToPpx } from "./ppx.ts";
import { downloadBlob, stripExtension } from "./download.ts";

const PPX_PICKER_TYPES = [
  {
    description: "PixPen Document",
    accept: { "application/octet-stream": [".ppx"] },
  },
];

function isAbort(e: unknown): boolean {
  return e instanceof DOMException && e.name === "AbortError";
}

function openPpxViaInput(): Promise<PixPenDocument | null> {
  return new Promise((resolve, reject) => {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = ".ppx";
    input.addEventListener("change", () => {
      const file = input.files?.[0];
      if (!file) {
        resolve(null);
        return;
      }
      loadDocumentFromPpx(file)
        .then((doc) => {
          doc.title = stripExtension(file.name);
          resolve(doc);
        })
        .catch(reject);
    });
    input.click();
  });
}

/** Opens a `.ppx` file via the File System Access API when available (keeps the handle for later overwrite-save),
 * falling back to a plain `<input type=file>` picker otherwise. Returns null if the user cancels. */
export async function pickAndOpenPpx(): Promise<PixPenDocument | null> {
  if (window.showOpenFilePicker) {
    let handle: FileSystemFileHandle;
    try {
      [handle] = await window.showOpenFilePicker({ types: PPX_PICKER_TYPES });
    } catch (e) {
      if (isAbort(e)) return null;
      throw e;
    }
    const file = await handle.getFile();
    const doc = await loadDocumentFromPpx(file);
    doc.fileHandle = handle;
    doc.title = stripExtension(handle.name);
    return doc;
  }
  return openPpxViaInput();
}

/** Loads a `.ppx` from an already-obtained File (drag & drop). */
export async function openPpxFromFile(file: File): Promise<PixPenDocument> {
  const doc = await loadDocumentFromPpx(file);
  doc.title = stripExtension(file.name);
  return doc;
}

/**
 * Saves `doc` to disk. If it already has a file handle and `forceDialog` is false, overwrites in place
 * (Ctrl+S). Otherwise prompts via the save picker (Ctrl+Shift+S / first save), falling back to a plain
 * download when the File System Access API is unavailable. Returns false if the user cancels.
 */
export async function saveDocument(
  doc: PixPenDocument,
  forceDialog = false,
): Promise<boolean> {
  const bytes = await saveDocumentToPpx(doc);

  if (!forceDialog && doc.fileHandle) {
    const writable = await doc.fileHandle.createWritable();
    await writable.write(bytes);
    await writable.close();
    doc.isModified = false;
    return true;
  }

  if (window.showSaveFilePicker) {
    let handle: FileSystemFileHandle;
    try {
      handle = await window.showSaveFilePicker({
        suggestedName: `${doc.title || "untitled"}.ppx`,
        types: PPX_PICKER_TYPES,
      });
    } catch (e) {
      if (isAbort(e)) return false;
      throw e;
    }
    const writable = await handle.createWritable();
    await writable.write(bytes);
    await writable.close();
    doc.fileHandle = handle;
    doc.title = stripExtension(handle.name);
    doc.isModified = false;
    return true;
  }

  downloadBlob(
    new Blob([bytes], { type: "application/octet-stream" }),
    `${doc.title || "untitled"}.ppx`,
  );
  doc.isModified = false;
  return true;
}
