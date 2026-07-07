/** Minimal ZIP writer, "stored" (uncompressed) method only — matches the WPF app's use of ZIP purely as a
 * container (CompressionLevel.NoCompression), so no DEFLATE encoder is needed. Unzip is handled by
 * jsr:@azulamb/zipper, which supports both stored and deflate; we only ever need to produce stored archives. */

export interface ZipInputEntry {
  name: string;
  data: Uint8Array<ArrayBuffer>;
  lastModified?: Date;
}

const CRC_TABLE = (() => {
  const table = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) {
      c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    }
    table[n] = c >>> 0;
  }
  return table;
})();

function crc32(data: Uint8Array): number {
  let crc = 0xffffffff;
  for (let i = 0; i < data.length; i++) {
    crc = CRC_TABLE[(crc ^ data[i]) & 0xff] ^ (crc >>> 8);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function dosDateTime(date: Date): { time: number; date: number } {
  const time = ((date.getHours() & 0x1f) << 11) |
    ((date.getMinutes() & 0x3f) << 5) |
    (Math.floor(date.getSeconds() / 2) & 0x1f);
  const dosDate = (((date.getFullYear() - 1980) & 0x7f) << 9) |
    (((date.getMonth() + 1) & 0xf) << 5) | (date.getDate() & 0x1f);
  return { time, date: dosDate };
}

const LOCAL_HEADER_SIZE = 30;
const CENTRAL_HEADER_SIZE = 46;
const EOCD_SIZE = 22;

/** Builds an uncompressed (stored) ZIP archive from `entries`. */
export function buildStoredZip(
  entries: ZipInputEntry[],
): Uint8Array<ArrayBuffer> {
  const encoder = new TextEncoder();
  const now = new Date();
  const prepared = entries.map((e) => ({
    nameBytes: encoder.encode(e.name),
    data: e.data,
    crc: crc32(e.data),
    dosTime: dosDateTime(e.lastModified ?? now),
  }));

  let localSize = 0;
  let centralSize = 0;
  for (const p of prepared) {
    localSize += LOCAL_HEADER_SIZE + p.nameBytes.length + p.data.length;
    centralSize += CENTRAL_HEADER_SIZE + p.nameBytes.length;
  }
  const buffer = new Uint8Array(localSize + centralSize + EOCD_SIZE);
  const view = new DataView(buffer.buffer);
  const localOffsets: number[] = [];
  let offset = 0;

  for (const p of prepared) {
    localOffsets.push(offset);
    view.setUint32(offset, 0x04034b50, true);
    offset += 4;
    view.setUint16(offset, 20, true);
    offset += 2; // version needed to extract
    view.setUint16(offset, 0, true);
    offset += 2; // general purpose flag
    view.setUint16(offset, 0, true);
    offset += 2; // compression method: stored
    view.setUint16(offset, p.dosTime.time, true);
    offset += 2;
    view.setUint16(offset, p.dosTime.date, true);
    offset += 2;
    view.setUint32(offset, p.crc, true);
    offset += 4;
    view.setUint32(offset, p.data.length, true);
    offset += 4; // compressed size
    view.setUint32(offset, p.data.length, true);
    offset += 4; // uncompressed size
    view.setUint16(offset, p.nameBytes.length, true);
    offset += 2;
    view.setUint16(offset, 0, true);
    offset += 2; // extra field length
    buffer.set(p.nameBytes, offset);
    offset += p.nameBytes.length;
    buffer.set(p.data, offset);
    offset += p.data.length;
  }

  const centralStart = offset;
  prepared.forEach((p, i) => {
    view.setUint32(offset, 0x02014b50, true);
    offset += 4;
    view.setUint16(offset, 20, true);
    offset += 2; // version made by
    view.setUint16(offset, 20, true);
    offset += 2; // version needed to extract
    view.setUint16(offset, 0, true);
    offset += 2; // general purpose flag
    view.setUint16(offset, 0, true);
    offset += 2; // compression method
    view.setUint16(offset, p.dosTime.time, true);
    offset += 2;
    view.setUint16(offset, p.dosTime.date, true);
    offset += 2;
    view.setUint32(offset, p.crc, true);
    offset += 4;
    view.setUint32(offset, p.data.length, true);
    offset += 4;
    view.setUint32(offset, p.data.length, true);
    offset += 4;
    view.setUint16(offset, p.nameBytes.length, true);
    offset += 2;
    view.setUint16(offset, 0, true);
    offset += 2; // extra field length
    view.setUint16(offset, 0, true);
    offset += 2; // file comment length
    view.setUint16(offset, 0, true);
    offset += 2; // disk number start
    view.setUint16(offset, 0, true);
    offset += 2; // internal file attributes
    view.setUint32(offset, 0, true);
    offset += 4; // external file attributes
    view.setUint32(offset, localOffsets[i], true);
    offset += 4;
    buffer.set(p.nameBytes, offset);
    offset += p.nameBytes.length;
  });
  const centralSizeActual = offset - centralStart;

  view.setUint32(offset, 0x06054b50, true);
  offset += 4;
  view.setUint16(offset, 0, true);
  offset += 2; // disk number
  view.setUint16(offset, 0, true);
  offset += 2; // disk where central directory starts
  view.setUint16(offset, prepared.length, true);
  offset += 2; // records on this disk
  view.setUint16(offset, prepared.length, true);
  offset += 2; // total records
  view.setUint32(offset, centralSizeActual, true);
  offset += 4;
  view.setUint32(offset, centralStart, true);
  offset += 4;
  view.setUint16(offset, 0, true);
  offset += 2; // comment length

  return buffer;
}
