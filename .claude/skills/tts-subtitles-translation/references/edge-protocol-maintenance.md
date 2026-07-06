# Edge TTS protocol — maintenance procedure

When Edge voices break globally (all users, all voices, usually HTTP 403 on the
WebSocket handshake), Microsoft rotated something. Fix procedure:

1. Check the upstream reference implementation: the `edge-tts` Python project
   (github.com/rany2/edge-tts) — it tracks endpoint changes within days. Diff its
   current constants against ours.
2. Constants to compare (all in `EdgeTtsService.cs`, deliberately gathered at the top):
   - `TrustedToken` (historically stable: 6A5AA1D4EAFF4E9FB37E23D68491D6F4)
   - `ChromiumFull` / `ChromiumMajor` (pinned browser version — the usual culprit;
     bump to a current stable Edge version)
   - `Sec-MS-GEC` recipe: SHA-256( (unix_seconds + 11644473600, floored to 300s) ×
     10^7 as string + TrustedToken ), uppercase hex
   - Origin header (read-aloud extension id), User-Agent shape, `muid` cookie
3. Local repro without the game: a tiny net48 console calling
   `EdgeTtsService.SpeakAsync("test", "en-GB-RyanNeural", 1.0)` — if it 403s there,
   it is protocol drift; if it works there but not in-game, look at the module.
4. Distinguish from the user-clock failure: Sec-MS-GEC embeds a 5-minute time window,
   so ONE user failing while others are fine → their Windows clock is off.
5. After fixing: version bump + prerelease (protocol fixes are worth fast-tracking —
   every Edge user is affected).

Design intent to preserve: one WebSocket per chunk (stateless, simple recovery),
15 s per-chunk timeout, throw-on-empty-audio (drives the offline fallback),
WebSocketLite stays minimal (only what this endpoint needs).
