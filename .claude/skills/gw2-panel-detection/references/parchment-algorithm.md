# ParchmentDetector — exact algorithm (verified against v0.2.2 source)

File: `ParchmentDetector.cs`. Ported 1:1 from the validated Python prototype.
Signature: `Rectangle? Find(Bitmap bmp, out double solidity)`.

## Constants

| Constant | Value | Why this value |
|---|---|---|
| `LumThresh` | 185 | Parchment paper is bright; 185 rejects moonlit grass (~155) that broke the prototype's original 150 threshold in a night-scene debug session. Luminance = (299R + 587G + 114B)/1000. |
| `ChromaThresh` | 60 | Parchment is nearly colorless (R≈G≈B). Chroma = max(R,G,B) − min(R,G,B). Snow passes this too — shape filters below are what reject snow. |
| `Cell` | 8 | 8×8 px downsample. Detection runs on the cell grid, not raw pixels. |
| cell pass ratio | ≥ 0.45 | A cell is "parchment" if ≥45% of its 64 pixels pass lum+chroma. |

## Steps

1. LockBits the whole frame (Format24bppRgb — note byte order is **B,G,R**), count
   qualifying pixels per cell.
2. Mark cells passing the 45% ratio; flood-fill (4-neighbour, explicit stack — no
   recursion) into blobs.
3. For each blob compute: bounding box, `solidity = area / (bh*bw)`,
   `ratio = bh/bw` (height over width), `wFrac = bw/cw`, `hFrac = bh/ch`.
4. Accept if **all** hold: `solidity > 0.6`, `0.9 < ratio < 3.0`,
   `0.05 < wFrac < 0.85`, `0.12 < hFrac < 0.98`. These shape gates are what reject
   snowfields, bright skies, and UI slivers that pass the color mask.
5. Largest accepted blob (by cell area) wins. Return its bbox scaled back by `Cell`.

## Post-processing

`InnerCrop(box)`: shave the decorative frame before OCR — 3% of width on each side,
4% of height top, 2% bottom.

## Caller behavior worth knowing (LorebookReaderModule.CaptureBookAsync)

- If `Find` returns null, the module falls back to a fixed center window:
  x=34%, y=12%, w=32%, h=80% of the frame, and logs "using center fallback".
  OCR of the fallback often yields <20 chars → the "no readable text found" toast.
- Book title comes from a second OCR pass on a strip **above** the parchment:
  height = 14% of parchment height, offset 6 px, width = parchment + 20 px,
  accepted only if 2–60 chars (`TryReadHeader`).

## Failure gallery (real, from calibration history)

- **Night scene, bright grass** (prototype era): brightness-only detection at
  threshold 150 swallowed the scene below the book → answer was raising to 185 *and*
  adding chroma + blob-shape gates, not just cranking brightness.
- **Panel not centered**: early prototype assumed a centered book; the flood-fill
  approach removed that assumption. Never reintroduce "center band only" logic for
  the primary detection (the center band survives only as the last-ditch fallback).
- **Textures resembling parchment** (README Known Issues): light stone/canvas surfaces
  can pass all gates for a frame → buttons flicker on. Mitigation ideas live in the
  SKILL.md improvements list (temporal smoothing).
