import { unzip } from "jsr:@azulamb/zipper@^0.1.0";
import type {
  ColorPalette,
  GridSettings,
  Layer,
  PenDefinition,
  PixPenDocument,
  SingleGridSettings,
} from "../types.ts";
import { buildStoredZip } from "./zip-writer.ts";
import { imageDataToPngBytes, pngBytesToImageData } from "./png.ts";

/** .ppx v0.2.0 (docs/file-format.md): an uncompressed ZIP of document.json + layer_XXXX.png/ref_XXXX.png. */

interface SingleGridJson {
  Visible: boolean;
  OffsetX: number;
  OffsetY: number;
  SpacingX: number;
  SpacingY: number;
  Color: string;
  LineType: string;
}

interface DocumentJson {
  Id: string;
  Width: number;
  Height: number;
  Dpi: number;
  Title: string;
  Palette: {
    Colors: string[];
    ForegroundColor: string;
    BackgroundColor: string;
  };
  Pens: {
    Name: string;
    Shape: string;
    Size: number;
    Opacity: number;
    PressureAffectsSize: boolean;
    PressureAffectsOpacity: boolean;
    MinSizeFactor: number;
  }[];
  Grid: {
    Small: SingleGridJson;
    Medium: SingleGridJson;
    Large: SingleGridJson;
  };
  Layers: {
    Index: number;
    Name: string;
    IsVisible: boolean;
    IsLocked: boolean;
    Opacity: number;
    IsReference?: boolean;
    RefX?: number;
    RefY?: number;
    RefWidth?: number;
    RefHeight?: number;
  }[];
}

function gridToJson(g: SingleGridSettings): SingleGridJson {
  return {
    Visible: g.visible,
    OffsetX: g.offsetX,
    OffsetY: g.offsetY,
    SpacingX: g.spacingX,
    SpacingY: g.spacingY,
    Color: g.color,
    LineType: g.lineType,
  };
}

function gridFromJson(g: SingleGridJson): SingleGridSettings {
  return {
    visible: g.Visible,
    offsetX: g.OffsetX,
    offsetY: g.OffsetY,
    spacingX: g.SpacingX,
    spacingY: g.SpacingY,
    color: g.Color,
    lineType: g.LineType === "Dashed" ? "Dashed" : "Solid",
  };
}

function buildDocumentJson(doc: PixPenDocument): DocumentJson {
  return {
    Id: doc.id,
    Width: doc.width,
    Height: doc.height,
    Dpi: doc.dpi,
    Title: doc.title,
    Palette: {
      Colors: doc.palette.colors,
      ForegroundColor: doc.palette.foregroundColor,
      BackgroundColor: doc.palette.backgroundColor,
    },
    Pens: doc.pens.map((p) => ({
      Name: p.name,
      Shape: p.shape,
      Size: p.size,
      Opacity: p.opacity,
      PressureAffectsSize: p.pressureAffectsSize,
      PressureAffectsOpacity: p.pressureAffectsOpacity,
      MinSizeFactor: p.minSizeFactor,
    })),
    Grid: {
      Small: gridToJson(doc.grid.small),
      Medium: gridToJson(doc.grid.medium),
      Large: gridToJson(doc.grid.large),
    },
    Layers: doc.layers.map((l) => ({
      Index: l.index,
      Name: l.name,
      IsVisible: l.isVisible,
      IsLocked: l.isLocked,
      Opacity: l.opacity,
      IsReference: l.isReference,
      RefX: l.refX,
      RefY: l.refY,
      RefWidth: l.refWidth,
      RefHeight: l.refHeight,
    })),
  };
}

function layerFileName(index: number, isReference: boolean): string {
  const padded = String(index).padStart(4, "0");
  return isReference ? `ref_${padded}.png` : `layer_${padded}.png`;
}

export async function saveDocumentToPpx(
  doc: PixPenDocument,
): Promise<Uint8Array<ArrayBuffer>> {
  const encoder = new TextEncoder();
  const entries = [{
    name: "document.json",
    data: encoder.encode(JSON.stringify(buildDocumentJson(doc))),
  }];
  for (const layer of doc.layers) {
    const data = await imageDataToPngBytes(layer.image);
    entries.push({ name: layerFileName(layer.index, layer.isReference), data });
  }
  return buildStoredZip(entries);
}

export async function loadDocumentFromPpx(
  file: File | Blob,
): Promise<PixPenDocument> {
  const zipEntries = await unzip(file);
  const docEntry = zipEntries.find((e) => e.path === "document.json");
  if (!docEntry) throw new Error("document.json not found in archive");
  const json = JSON.parse(await docEntry.file.text()) as DocumentJson;

  const layers: Layer[] = [];
  for (const meta of json.Layers) {
    const isReference = !!meta.IsReference;
    const fileName = layerFileName(meta.Index, isReference);
    const pngEntry = zipEntries.find((e) => e.path === fileName);
    const image = pngEntry
      ? await pngBytesToImageData(
        new Uint8Array(await pngEntry.file.arrayBuffer()),
      )
      : new ImageData(
        isReference ? 1 : json.Width,
        isReference ? 1 : json.Height,
      );
    layers.push({
      id: crypto.randomUUID(),
      index: meta.Index,
      name: meta.Name,
      isVisible: meta.IsVisible,
      isLocked: meta.IsLocked,
      opacity: meta.Opacity,
      isReference,
      image,
      refX: meta.RefX ?? 0,
      refY: meta.RefY ?? 0,
      refWidth: meta.RefWidth ?? 0,
      refHeight: meta.RefHeight ?? 0,
    });
  }
  layers.sort((a, b) => a.index - b.index);

  const palette: ColorPalette = {
    colors: json.Palette.Colors,
    foregroundColor: json.Palette.ForegroundColor,
    backgroundColor: json.Palette.BackgroundColor,
  };
  const pens: PenDefinition[] = json.Pens.map((p) => ({
    name: p.Name,
    shape: p.Shape === "Square" ? "Square" : "Round",
    size: p.Size,
    opacity: p.Opacity,
    pressureAffectsSize: p.PressureAffectsSize,
    pressureAffectsOpacity: p.PressureAffectsOpacity,
    minSizeFactor: p.MinSizeFactor,
  }));
  const grid: GridSettings = {
    small: gridFromJson(json.Grid.Small),
    medium: gridFromJson(json.Grid.Medium),
    large: gridFromJson(json.Grid.Large),
  };

  return {
    id: json.Id,
    width: json.Width,
    height: json.Height,
    dpi: json.Dpi,
    title: json.Title,
    palette,
    pens,
    grid,
    layers,
    isModified: false,
    fileHandle: null,
  };
}
