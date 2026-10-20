Soldier Idle v2

Changes: smaller knee bend and invariant rifle geometry.
The upper body drops at most 12 pixels (v1 dropped approximately 59 pixels).
The same upper-body artwork is translated without scaling or redrawing.
Leg articulation tapers to zero above the boots; both feet stay fixed.

Preview: soldier-idle-preview.gif (transparent, infinite loop).
Lossless animated preview: soldier-idle.webp.
PNG sprite sheet: soldier-idle-spritesheet.png.
Grid: 6 columns x 4 rows. Cell size: 768 x 768 pixels.
Order: left to right, then top to bottom. 24 frames, 60 ms per frame.
Full cycle: 1.44 seconds. Identical frames are coalesced in GIF/WebP with durations preserved.
Individual frames: frames/soldier-idle-00.png through soldier-idle-23.png.
Ground baseline: y=720 from top. Suggested pivot: (0.5, 0.0625).

Art source: ../Idle-v1/frames/soldier-idle-00.png, originally created with built-in imagegen.
Revision method: render a small leg deformation from the existing image; no new AI frame generation.
The reference image and previous animation remain unchanged.
validation.json checks exact rifle-region pixel identity after translation and exact fixed-foot pixel identity for all 24 PNG frames.
Both animated formats were checked for transparency, infinite looping and 1440 ms total duration.
No Unity scene, prefab or animation controller was modified; Unity playback was not tested.
