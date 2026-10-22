Soldier Fire v1

Single-shot rifle animation: aim, brief muzzle flash, backward recoil, return to aim.
Character artwork is the same rigid upper body used by Soldier Idle v2.
The rifle never changes length or shape, and the boots remain fixed.

Preview: soldier-fire-preview.gif (loops with a 1200 ms pause after each shot).
Single-play lossless animation: soldier-fire.webp.
Transparent sprite sheet: soldier-fire-spritesheet.png.
Grid: 4 columns x 4 rows. Cell size: 768 x 768 pixels.
Order: left to right, then top to bottom. 16 frames at 20 fps (50 ms per frame).
One firing action lasts 800 ms and is not intended to loop during gameplay.
Shot event: frame 02 (zero based), at 100 ms. Muzzle flash appears on frames 02 and 03.
Individual PNGs: frames/soldier-fire-00.png through soldier-fire-15.png.
Separate muzzle flash: effects/muzzle-flash.png, generated with built-in imagegen.
Ground baseline: y=720 from top; suggested pivot: (0.5, 0.0625), matching Idle v2.

Source artwork: ../Idle-v1/frames/soldier-idle-00.png. Existing files unchanged.
Character motion uses rigid upper-body translation, smoothly tapered through the legs.
validation.json verifies the unchanged rifle pixels before the effect overlay, fixed feet, transparent borders, identical first and last poses, source hash, playback duration and loop settings.
No audio is included.
No Unity scene, prefab, script or Animator Controller was modified. Unity runtime playback has not been tested.
