Soldier Fire v2

Revision: added visible upward muzzle recoil, followed by a smooth return to the original aim.
Maximum upper-body/rifle rotation: 2.2 degrees counterclockwise (muzzle rises).
Maximum vertical body lift: 5 pixels. Existing backward recoil remains 16 pixels.
Rifle, hands, head and torso share one rigid transform; there is no weapon scaling or redrawing.
The muzzle flash follows the barrel position and angle. Both boots remain fixed.

Preview: soldier-fire-preview.gif, loops with a 1200 ms pause after each shot.
Single-play lossless animation: soldier-fire.webp.
Transparent sprite sheet: soldier-fire-spritesheet.png.
Grid: 4 columns x 4 rows, each cell 768 x 768 pixels.
Read left to right, then top to bottom. 16 frames at 20 fps, 800 ms total.
Shot event: frame 02 (zero based), 100 ms. Flash on frames 02 and 03.
Individual images: frames/soldier-fire-00.png through soldier-fire-15.png.
Ground baseline: y=720 from top. Pivot: (0.5, 0.0625), matching Idle v2.

Original artwork and Fire v1 remain unchanged.
Original artwork and muzzle-flash texture were made with built-in imagegen; this revision reuses those textures with animation transforms.
validation.json records fixed-foot pixel checks, constant transformed rifle length, transparent borders, matching start/end poses, original-source hash and playback metadata.
No audio is included. Unity scene, prefab and Animator Controller references were not modified, and Unity playback was not tested.
