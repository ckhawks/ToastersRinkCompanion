# Meme Board — Animated GIF Support

**Status:** Planned / not started
**Scope:** Client-side (mod). Server/DB changes to *allow* GIF uploads are out of scope for this doc.

## Goal

Let the daily meme board (`src/handlers/Visuals/MemeDisplay.cs`) display **animated** GIFs, not just static PNG/JPG.

## Why it's non-trivial

Unity has **no native animated-GIF support**. The current loader uses
`UnityWebRequestTexture.GetTexture(url)` → `DownloadHandlerTexture` → a single
`Texture2D`, which only decodes PNG/JPG. Handed a GIF it yields, at best, one
frame — no animation.

The standard (non-hacky) solution — used by every Unity GIF library (UniGif,
ProGif, etc.) — is:

1. Download the **raw bytes**.
2. Decode the GIF into an array of `Texture2D` frames + per-frame delays (one-time cost).
3. Play back by swapping the material's `mainTexture` on a timer.

This fits the existing render path exactly: the board already renders through
`mat.mainTexture` on a world-space quad, so playback is just reassigning that one
field each frame. Nothing about the engine is being fought or bypassed.

**Avoid** `System.Drawing.Image` for decoding — it may not exist in Puck's Mono
runtime and drags in platform baggage. Use a self-contained pure-C# decoder.

## Current architecture (as of this doc)

Everything lives in `src/handlers/Visuals/MemeDisplay.cs` (static class):

- `LoadMemeTexture(MemeDisplayPayload)` coroutine (~line 142) — downloads via
  `UnityWebRequestTexture`, stores result in static `Texture2D _currentTexture`,
  destroys the old one, then calls `ShowIfWarmup()`.
- `CreateBoard()` (~line 186) — builds the world-space board: a `Cube` backing +
  a `Quad` named `MemeImage` with `Unlit/Texture` material whose `mainTexture` is
  `_currentTexture`. Aspect ratio computed from `_currentTexture.width/height`.
- Board only renders during `GamePhase.Warmup` (`ShowIfWarmup`, and the
  `MemeDisplayGamePhaseChangedPatch` Harmony patch).
- The image URL is **pushed by the server** in the `meme_display` message payload
  (`MemeDisplayPayload.url`). There is no local config/URL list.
- Only runs when `MessagingHandler.connectedToToastersRink` is true.
- Cleanup destroys `_currentTexture` in `LoadMemeTexture`, `DestroyBoard`, and `Cleanup`.

## Implementation plan

### 1. Vendor a pure-C# GIF decoder
- Add a self-contained decoder under `src/handlers/Visuals/` (e.g. `GifDecoder.cs`).
- Permissively licensed (MIT) vendored, or ~200–300 lines hand-written
  (LZW decompression + frame disposal/compositing).
- Output: `Texture2D[] frames` + `float[] delays` (seconds per frame).
- **No** `System.Drawing` dependency.

### 2. Split the load path in `LoadMemeTexture`
- Download raw bytes with a plain `UnityWebRequest` (read `downloadHandler.data`)
  instead of `UnityWebRequestTexture`.
- Sniff the magic bytes: `GIF8` (`GIF87a` / `GIF89a`) → GIF path; otherwise decode
  as static via the existing `DownloadHandlerTexture` behaviour (keep back-compat).
- GIF path: decode to `Texture2D[]` + delays, store in new static fields
  (e.g. `_gifFrames`, `_gifDelays`), set the board's initial frame.

### 3. Playback
- A small ticking component that advances the frame index by elapsed time and
  reassigns `mat.mainTexture` on the `MemeImage` quad.
- Drive it off an existing per-frame hub rather than a new MonoBehaviour if clean:
  candidates are the `UIManager.Update` postfixes (`UpdatableChat.cs`,
  `JuggleRallyTimer.cs`) or the `PlayerInput.Update` postfix (`SpawnPuckKeybind.cs`).
  Simplest self-contained option: attach a tiny `MonoBehaviour` to the board object.
- Single static frame (non-GIF) path: no ticking needed.

### 4. Cleanup + caps
- Destroy the **whole frame array** everywhere `_currentTexture` is destroyed today
  (`LoadMemeTexture` on reload, `DestroyBoard`, `Cleanup`). A leaked frame array is
  the main memory risk.
- Guardrails: cap max frame count and max frame dimensions (a long/large GIF
  decodes to N uncompressed textures = tens of MB VRAM). Log + truncate if exceeded
  (don't silently drop).

### 5. Test hook (no DB dependency)
- Add a local chat command in `src/ClientChat.cs` (alongside `/logcamera`,
  `/watchpucksof`): `/testmeme <url>`.
- It builds a `MemeDisplayPayload` in-memory and calls the same
  `LoadMemeTexture` → `CreateBoard` path — no server message, no DB row, no
  `connectedToToastersRink` handshake.
- `UnityWebRequest` accepts `file://` URLs, so test fully offline:
  `/testmeme file:///C:/Users/stlrc/test.gif`. Also accepts real `https://…/x.gif`.
- Must be in `GamePhase.Warmup` to see the board (default on a local practice server).

**Test loop:** launch local practice server → `/testmeme file:///.../whatever.gif`
→ watch it animate.

## Suggested build order

Decoder + `/testmeme` hook first (validate loading), then playback, then
cleanup/caps. Static-image behaviour must remain unchanged for non-GIF URLs.

## Open questions / notes

- Server + DB must also allow GIF uploads for this to matter in production; that's a
  separate change from this client work.
- Decide frame/size caps (start conservative, e.g. cap N frames and max dimension).
- Consider whether very large GIFs should be rejected outright vs. truncated.
