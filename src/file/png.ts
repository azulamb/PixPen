/** ImageData <-> PNG bytes, via an offscreen &lt;canvas&gt;. PNG on disk is always standard RGBA (the WPF app's
 * "BGRA32" in docs/file-format.md describes WriteableBitmap's in-memory layout, not the encoded file bytes),
 * so no channel swap is needed here. */

export async function imageDataToPngBytes(
  image: ImageData,
): Promise<Uint8Array<ArrayBuffer>> {
  const canvas = document.createElement("canvas");
  canvas.width = image.width;
  canvas.height = image.height;
  const ctx = canvas.getContext("2d")!;
  ctx.putImageData(image, 0, 0);
  const blob = await new Promise<Blob>((resolve, reject) => {
    canvas.toBlob(
      (b) => (b ? resolve(b) : reject(new Error("canvas.toBlob failed"))),
      "image/png",
    );
  });
  return new Uint8Array(await blob.arrayBuffer());
}

export async function pngBytesToImageData(
  bytes: Uint8Array,
): Promise<ImageData> {
  const blob = new Blob([new Uint8Array(bytes)], { type: "image/png" });
  const bitmap = await createImageBitmap(blob);
  const canvas = document.createElement("canvas");
  canvas.width = bitmap.width;
  canvas.height = bitmap.height;
  const ctx = canvas.getContext("2d")!;
  ctx.drawImage(bitmap, 0, 0);
  bitmap.close();
  return ctx.getImageData(0, 0, canvas.width, canvas.height);
}
