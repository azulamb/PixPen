/** Core data model, mirrors the WPF app's Models/*.cs and docs/file-format.md (.ppx v0.2.0). */

export type PenShape = "Round" | "Square";
export type GridLineType = "Solid" | "Dashed";
export type ThemeMode = "System" | "Light" | "Dark";
export type PanelSide = "Left" | "Right";

export interface PenDefinition {
  name: string;
  shape: PenShape;
  size: number;
  opacity: number;
  pressureAffectsSize: boolean;
  pressureAffectsOpacity: boolean;
  minSizeFactor: number;
}

export interface ColorPalette {
  colors: string[];
  foregroundColor: string;
  backgroundColor: string;
}

export interface SingleGridSettings {
  visible: boolean;
  offsetX: number;
  offsetY: number;
  spacingX: number;
  spacingY: number;
  color: string;
  lineType: GridLineType;
}

export interface GridSettings {
  small: SingleGridSettings;
  medium: SingleGridSettings;
  large: SingleGridSettings;
}

export interface Layer {
  /** Stable identity used by the undo stack; unrelated to `index` (which is the persisted stacking order). */
  id: string;
  index: number;
  name: string;
  isVisible: boolean;
  isLocked: boolean;
  opacity: number;
  isReference: boolean;
  /** Canvas-size ImageData for normal layers; original-size ImageData for reference layers. */
  image: ImageData;
  refX: number;
  refY: number;
  refWidth: number;
  refHeight: number;
}

export interface SelectionMask {
  hasSelection: boolean;
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface PixPenDocument {
  id: string;
  width: number;
  height: number;
  dpi: number;
  title: string;
  palette: ColorPalette;
  pens: PenDefinition[];
  grid: GridSettings;
  layers: Layer[];
  isModified: boolean;
  fileHandle: FileSystemFileHandle | null;
}

export const DEFAULT_PALETTE_COLORS: string[] = [
  "#FF141820", // soft black
  "#FFF5F8FF", // soft white
  "#FF7F8C99", // slate gray
  "#FFE0546B", // soft red
  "#FFE87A9E", // rose
  "#FFE0813A", // burnt orange
  "#FFF2C14E", // amber
  "#FFE8E06B", // pale yellow
  "#FF8FCB6E", // leaf green
  "#FF4FA37B", // pine green
  "#FF57C2C2", // teal
  "#FF5CA7D9", // sky blue
  "#FF3E6FB8", // deep blue
  "#FF6C63C4", // indigo
  "#FF9B6BC4", // violet
  "#FFC46BAF", // orchid
  "#FF7A5A4A", // umber
  "#FFA6836A", // tan
  "#FFC9A98B", // sand
  "#FF3A3F44", // charcoal
];

export function createDefaultPalette(): ColorPalette {
  return {
    colors: [...DEFAULT_PALETTE_COLORS],
    foregroundColor: DEFAULT_PALETTE_COLORS[0],
    backgroundColor: DEFAULT_PALETTE_COLORS[1],
  };
}

export function createDefaultPens(): PenDefinition[] {
  return [
    {
      name: "Pen",
      shape: "Round",
      size: 10,
      opacity: 1,
      pressureAffectsSize: true,
      pressureAffectsOpacity: false,
      minSizeFactor: 0,
    },
    {
      name: "Pen (Large)",
      shape: "Round",
      size: 30,
      opacity: 1,
      pressureAffectsSize: true,
      pressureAffectsOpacity: false,
      minSizeFactor: 0,
    },
  ];
}

function defaultSingleGrid(spacing: number, color: string): SingleGridSettings {
  return {
    visible: spacing === 8,
    offsetX: 0,
    offsetY: 0,
    spacingX: spacing,
    spacingY: spacing,
    color,
    lineType: "Solid",
  };
}

export function createDefaultGrid(): GridSettings {
  return {
    small: defaultSingleGrid(8, "#FF808080"),
    medium: defaultSingleGrid(32, "#FF404040"),
    large: defaultSingleGrid(128, "#FF202020"),
  };
}

export function createTransparentImageData(
  width: number,
  height: number,
): ImageData {
  return new ImageData(width, height);
}

export function createFilledImageData(
  width: number,
  height: number,
  argb: string,
): ImageData {
  const image = new ImageData(width, height);
  const { r, g, b, a } = hexArgbToRgba(argb);
  const data = image.data;
  for (let i = 0; i < data.length; i += 4) {
    data[i] = r;
    data[i + 1] = g;
    data[i + 2] = b;
    data[i + 3] = a;
  }
  return image;
}

export function createDefaultDocument(
  width = 1000,
  height = 1000,
  dpi = 96,
): PixPenDocument {
  const palette = createDefaultPalette();
  return {
    id: crypto.randomUUID(),
    width,
    height,
    dpi,
    title: "untitled",
    palette,
    pens: createDefaultPens(),
    grid: createDefaultGrid(),
    layers: [
      {
        id: crypto.randomUUID(),
        index: 0,
        name: "Layer 1",
        isVisible: true,
        isLocked: false,
        opacity: 1,
        isReference: false,
        image: createTransparentImageData(width, height),
        refX: 0,
        refY: 0,
        refWidth: 0,
        refHeight: 0,
      },
    ],
    isModified: false,
    fileHandle: null,
  };
}

/** #AARRGGBB -> {r,g,b,a(0-255)}. Import from color.ts would be circular for this internal use, so re-implemented minimally here. */
function hexArgbToRgba(
  argb: string,
): { r: number; g: number; b: number; a: number } {
  const hex = argb.replace("#", "");
  const a = parseInt(hex.substring(0, 2), 16);
  const r = parseInt(hex.substring(2, 4), 16);
  const g = parseInt(hex.substring(4, 6), 16);
  const b = parseInt(hex.substring(6, 8), 16);
  return { r, g, b, a };
}
