"""Render texture-preserving idle loops from the existing enemy artwork."""
from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
import zipfile

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageSequence

ROOT = Path(__file__).resolve().parents[2]
SIZE = 768
COUNT = 24
COLS = 6
ROWS = 4
GROUND = 720


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def smooth(a, b, value):
    v = np.clip((value - a) / (b - a), 0.0, 1.0)
    return v * v * (3.0 - 2.0 * v)


def prepare(source):
    raw = np.array(Image.open(source).convert('RGBA'))
    yy, xx = np.where(raw[:, :, 3] > 8)
    box = (int(xx.min()), int(yy.min()), int(xx.max() + 1), int(yy.max() + 1))
    scale = min(672 / (box[2] - box[0]), 656 / (box[3] - box[1]))
    width = round((box[2] - box[0]) * scale)
    height = round((box[3] - box[1]) * scale)
    left, top = (SIZE - width) // 2, GROUND - height
    crop = Image.fromarray(raw).crop(box).resize((width, height), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA', (SIZE, SIZE))
    canvas.paste(crop, (left, top))
    base = np.array(canvas)
    # Near-invisible generation noise is excluded from the render, never from the source.
    base[base[:, :, 3] <= 2] = 0
    sy, sx = np.mgrid[:SIZE, :SIZE].astype(np.float32)
    source_x = (sx - left) / (width / (box[2] - box[0])) + box[0]
    source_y = (sy - top) / (height / (box[3] - box[1])) + box[1]
    return base, source_x, source_y, dict(sourceCrop=list(box), scale=scale,
                                        left=left, top=top, resizedWidth=width, resizedHeight=height)


def scarecrow_motion(x, y, phase):
    # Joint mobility tapers smoothly to zero above both feet.
    mobility = 1.0 - smooth(720, 1035, y)
    sway = math.sin(phase)
    breath = (1.0 - math.cos(phase)) * 0.5
    dx = 4.5 * sway * mobility
    dy = 4.0 * breath * mobility
    arm_band = smooth(430, 530, y) * (1.0 - smooth(830, 980, y))
    left_hand = (1.0 - smooth(360, 535, x)) * arm_band
    right_hand = smooth(875, 1050, x) * arm_band
    delayed = math.sin(phase - 0.45) - math.sin(-0.45)
    dx += 5.0 * delayed * left_hand - 3.6 * delayed * right_hand
    dy += 2.8 * sway * left_hand - 2.1 * sway * right_hand
    head = (1.0 - smooth(370, 490, y)) * (1.0 - smooth(720, 870, x))
    dx += 1.5 * sway * head
    dy += 1.2 * breath * head
    fixed = y >= 1035
    return dx.astype(np.float32), dy.astype(np.float32), fixed


def tail_weights(x, y):
    # Follow the curled tail from its thick base to its tip; motion grows toward the tip.
    points = [(1300, 665), (1450, 545), (1465, 355), (1390, 220), (1260, 150), (1195, 150)]
    lengths = [math.dist(a, b) for a, b in zip(points, points[1:])]
    total = sum(lengths)
    best = np.full_like(x, 1e20)
    progress = np.zeros_like(x)
    walked = 0.0
    for i, (a, b) in enumerate(zip(points, points[1:])):
        vx, vy = b[0] - a[0], b[1] - a[1]
        t = np.clip(((x-a[0])*vx + (y-a[1])*vy) / (vx*vx+vy*vy), 0, 1)
        distance = np.sqrt((x-(a[0]+vx*t))**2 + (y-(a[1]+vy*t))**2)
        use = distance < best
        progress = np.where(use, (walked + t * lengths[i]) / total, progress)
        best = np.minimum(best, distance)
        walked += lengths[i]
    radius = 125 - 55 * progress
    coverage = 1.0 - smooth(radius * 0.58, radius, best)
    return coverage * progress**1.7


def lizard_motion(x, y, phase):
    mobility = 1.0 - smooth(535, 750, y)
    breath = (1.0 - math.cos(phase)) * 0.5
    chest = np.exp(-((x-760)/510)**2) * mobility
    dx = (x-760) * 0.0035 * breath * chest
    dy = -5.0 * breath * mobility
    # A slight independent head nod, with no change to eyes, jaws or painted features.
    head = (1.0 - smooth(400, 590, x)) * (1.0 - smooth(420, 560, y))
    dy -= 0.9 * math.sin(phase) * head
    tail = tail_weights(x, y)
    dx += 6.0 * math.sin(phase) * tail
    dy += 4.5 * (math.sin(phase + 0.3) - math.sin(0.3)) * tail
    fixed = y >= 755
    dx[fixed] = 0
    dy[fixed] = 0
    return dx.astype(np.float32), dy.astype(np.float32), fixed


def render(base, dx, dy, fixed):
    yy, xx = np.mgrid[:SIZE, :SIZE].astype(np.float32)
    # Inverse texture sampling with premultiplied alpha avoids colored edge fringes.
    alpha = base[:, :, 3:4].astype(np.float32) / 255.0
    premultiplied = np.concatenate((base[:, :, :3].astype(np.float32) * alpha,
                                   base[:, :, 3:4].astype(np.float32)), axis=2)
    mx, my = xx.copy(), yy.copy()
    for _ in range(4):
        sampled_dx = cv2.remap(dx, mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
        sampled_dy = cv2.remap(dy, mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
        mx, my = xx - sampled_dx, yy - sampled_dy
    out = cv2.remap(premultiplied, mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    out_alpha = out[:, :, 3:4] / 255.0
    rgb = np.divide(out[:, :, :3], out_alpha, out=np.zeros_like(out[:, :, :3]), where=out_alpha > 0.0001)
    rgba = np.rint(np.clip(np.concatenate((rgb, out[:, :, 3:4]), axis=2), 0, 255)).astype(np.uint8)
    rgba[rgba[:, :, 3] <= 2] = 0
    rgba[fixed] = base[fixed]
    return Image.fromarray(rgba)


def make_gif(frames, path, duration):
    # One shared palette prevents palette flicker; reserve index 255 for transparency.
    palette_sample = Image.new('RGB', (384 * 3, 384 * 2), (26, 25, 24))
    for k, i in enumerate(range(0, COUNT, 4)):
        sample = frames[i].resize((384, 384), Image.Resampling.LANCZOS)
        under = Image.new('RGBA', sample.size, (26, 25, 24, 255))
        under.alpha_composite(sample)
        palette_sample.paste(under.convert('RGB'), ((k % 3)*384, (k // 3)*384))
    palette = palette_sample.quantize(colors=255, method=Image.Quantize.MEDIANCUT)
    palette.putpalette(palette.getpalette()[:765] + [0, 0, 0])
    indexed = []
    for frame in frames:
        rgba = np.array(frame)
        under = Image.new('RGBA', frame.size, (26, 25, 24, 255))
        under.alpha_composite(frame)
        quantized = under.convert('RGB').quantize(palette=palette, dither=Image.Dither.NONE)
        indices = np.array(quantized)
        indices[rgba[:, :, 3] < 128] = 255
        quantized = Image.fromarray(indices, 'P')
        quantized.putpalette(palette.getpalette())
        quantized.info['transparency'] = 255
        indexed.append(quantized)
    indexed[0].save(path, save_all=True, append_images=indexed[1:], duration=duration,
                    loop=0, transparency=255, disposal=2, optimize=False)


def inspect_animation(path, total):
    with Image.open(path) as animation:
        loop = animation.info.get('loop')
        durations, hashes = [], []
        transparent = True
        for i in range(animation.n_frames):
            animation.seek(i)
            rgba = animation.convert('RGBA')
            # Pillow populates animated WebP duration after frame decode.
            durations.append(animation.info.get('duration', 0))
            hashes.append(hashlib.sha256(rgba.tobytes()).hexdigest())
            transparent &= rgba.getpixel((0, 0))[3] == 0
        assert len(set(hashes)) > 1, f'Static animation: {path}'
        assert sum(durations) == total, (path, durations, total)
        assert loop == 0, (path, loop)
        assert transparent, f'Transparency missing: {path}'
        return dict(file=path.name, frames=len(durations), durationMs=sum(durations),
                    infiniteLoop=loop == 0, transparentCorners=transparent,
                    uniqueFrames=len(set(hashes)))


def contact_sheet(frames, path):
    chosen = [0, 6, 12, 18]
    sheet = Image.new('RGB', (256 * 4, 280), (46, 49, 52))
    draw = ImageDraw.Draw(sheet)
    for k, frame_index in enumerate(chosen):
        thumb = frames[frame_index].resize((256, 256), Image.Resampling.LANCZOS)
        sheet.paste(thumb, (k*256, 24), thumb)
        draw.text((k*256+10, 7), f'Frame {frame_index:02d}', fill=(225, 225, 225))
    sheet.save(path)


def build(name, folder, duration, motion):
    source = ROOT / 'GeneratedArt' / folder / f'{name}-v1.png'
    output = source.parent / 'Idle-v1'
    if output.exists():
        raise FileExistsError(f'Refusing to overwrite {output}')
    source_hash = digest(source)
    output.mkdir()
    (output / 'frames').mkdir()
    base, x, y, placement = prepare(source)
    Image.fromarray(base).save(output / f'{name}-idle-reference.png')
    frames, checks, fields = [], [], []
    for i in range(COUNT):
        phase = i * 2 * math.pi / COUNT
        dx, dy, fixed = motion(x, y, phase)
        frame = render(base, dx, dy, fixed)
        array = np.array(frame)
        frame.save(output / 'frames' / f'{name}-idle-{i:02}.png')
        unchanged_feet = bool(np.array_equal(array[fixed], base[fixed]))
        edge = np.concatenate((array[0,:,3], array[-1,:,3], array[:,0,3], array[:,-1,3]))
        transparent_edges = bool(np.max(edge) == 0)
        assert unchanged_feet and transparent_edges, (name, i)
        frames.append(frame)
        checks.append(dict(frame=i, feetUnchanged=unchanged_feet, transparentBorder=transparent_edges,
                           maxHorizontalShiftPixels=round(float(np.max(np.abs(dx))), 3),
                           maxVerticalShiftPixels=round(float(np.max(np.abs(dy))), 3)))
        fields.append((dx, dy))
    start_dx, start_dy, _ = motion(x, y, 0)
    end_dx, end_dy, _ = motion(x, y, 2 * math.pi)
    periodic = bool(np.allclose(start_dx, end_dx, atol=1e-5) and np.allclose(start_dy, end_dy, atol=1e-5))
    assert periodic, 'Non-periodic motion field'
    sheet = Image.new('RGBA', (SIZE * COLS, SIZE * ROWS))
    for i, frame in enumerate(frames):
        sheet.paste(frame, ((i % COLS) * SIZE, (i // COLS) * SIZE))
    sheet.save(output / f'{name}-idle-spritesheet.png')
    make_gif(frames, output / f'{name}-idle-preview.gif', duration)
    frames[0].save(output / f'{name}-idle.webp', save_all=True, append_images=frames[1:],
                   duration=duration, loop=0, lossless=True, quality=100, method=4, exact=True)
    contact_sheet(frames, output / f'{name}-idle-contact-sheet.png')
    description = ('Gentle upper-body sway, delayed claw-hand movement and a small head bob; both feet remain fixed.'
                   if name == 'scarecrow' else
                   'Slow torso breathing, a slight head nod and a gentle curled-tail sway; all four feet remain fixed.')
    metadata = dict(name=f'{name}-idle-v1', frameWidth=SIZE, frameHeight=SIZE,
                    columns=COLS, rows=ROWS, frameCount=COUNT, frameDurationMs=duration,
                    frameRate=1000/duration, durationMs=COUNT*duration, loop=True,
                    groundYFromTop=GROUND, pivotNormalized=[0.5, (SIZE-GROUND)/SIZE],
                    sourceArt=f'../{name}-v1.png', sourceArtSha256=source_hash,
                    method='Existing artwork animated with smooth periodic texture deformation; no AI frame redrawing.',
                    motion=description, placement=placement)
    (output / 'animation.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    animated_checks = [inspect_animation(output / f'{name}-idle-preview.gif', COUNT*duration),
                       inspect_animation(output / f'{name}-idle.webp', COUNT*duration)]
    source_unchanged = digest(source) == source_hash
    assert source_unchanged
    with Image.open(output / f'{name}-idle-spritesheet.png') as packed:
        assert packed.size == (SIZE*COLS, SIZE*ROWS)
        for i, frame in enumerate(frames):
            box = ((i%COLS)*SIZE, (i//COLS)*SIZE, (i%COLS+1)*SIZE, (i//COLS+1)*SIZE)
            assert np.array_equal(np.array(packed.crop(box)), np.array(frame))
    steps = [float(np.max(np.hypot(fields[(i+1)%COUNT][0]-fields[i][0],
                                   fields[(i+1)%COUNT][1]-fields[i][1]))) for i in range(COUNT)]
    validation = dict(sourceUnchanged=source_unchanged, allFramesPassed=True,
                      periodicMotion=periodic, spriteSheetMatchesFrames=True,
                      loopSeamDisplacementPixels=round(steps[-1], 4),
                      largestAdjacentDisplacementPixels=round(max(steps), 4),
                      animated=animated_checks, checks=checks)
    (output / 'validation.json').write_text(json.dumps(validation, indent=2), encoding='utf-8')
    (output / 'README.txt').write_text(
        f'{name.title()} Idle v1\n\n{description}\n\n'
        f'24 transparent PNG frames, 768 x 768 pixels each.\n'
        f'PNG sprite sheet: 6 columns x 4 rows, 4608 x 3072.\n'
        f'Order: left to right, then top to bottom.\n'
        f'{duration} ms per frame; {COUNT*duration/1000:.2f} seconds per infinite loop.\n'
        f'GIF preview and lossless animated WebP included.\n'
        f'Ground baseline: y=720 from top; suggested pivot: (0.5, 0.0625).\n\n'
        f'Original artwork is unchanged. Existing painted details are moved with a smooth deformation field.\n'
        f'No new AI frame generation. PNG/WebP retain full alpha; GIF uses a single shared palette and binary transparency.\n'
        f'validation.json checks original source hash, fixed feet, transparent frame edges, periodic motion,\n'
        f'PNG sheet cell contents and decoded animation timing, looping and transparency.\n'
        f'No Unity scene, prefab, animation controller or runtime code changed. Unity playback was not tested.\n',
        encoding='utf-8')
    archive = source.parent / f'{name}-idle-v1.zip'
    if archive.exists():
        raise FileExistsError(archive)
    with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
        for path in sorted(output.rglob('*')):
            if path.is_file():
                bundle.write(path, Path('Idle-v1') / path.relative_to(output))
    with zipfile.ZipFile(archive) as bundle:
        assert bundle.testzip() is None
    print(json.dumps(dict(name=name, output=str(output), archive=str(archive),
                          sourceUnchanged=source_unchanged, allFramesPassed=True,
                          animated=animated_checks), indent=2), flush=True)


if __name__ == '__main__':
    build('scarecrow', 'Scarecrow', 100, scarecrow_motion)
    build('lizard', 'Lizard', 120, lizard_motion)
