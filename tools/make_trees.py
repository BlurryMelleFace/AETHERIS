"""Generates the background tree sprites (assets/foliage/trees/*.png + trees.json).

Four species with several variants each. Every sprite is drawn 1:1 (one art pixel = one world pixel),
the trunk is centred, and the last EMBED rows sit below the ground line so nothing floats on slopes.
Trunks are a little under one block (24px) wide; their tile footprint in the game is one block.

Run:  python tools/make_trees.py [preview.png]
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image

from art_common import (aniso_noise, bayer, capsule_mask, hash2, palette, quantize, shift, value_noise)

TILE = 24
EMBED = 8

BARK_PINE = palette("#241913", "#37271f", "#4d372b", "#69493a", "#8a6248")
BARK_RED = palette("#2c1612", "#43241b", "#5f3425", "#82492f", "#a8663f")
BARK_BIRCH = palette("#6d695c", "#a29d8b", "#cfcab8", "#ebe7d6", "#faf7ea")
BARK_OAK = palette("#2b1f17", "#3f2d21", "#5a4130", "#77573f", "#98714f")
BLACK_MARK = np.array([0x1e, 0x1a, 0x1c], dtype=np.uint8)
NEEDLE = palette("#10271f", "#17382b", "#214b34", "#2f6341", "#43804e", "#64a05c")
NEEDLE_RED = palette("#0f2723", "#173a35", "#204f47", "#2f6a5a", "#468a6e", "#6aaa84")
LEAF_BIRCH = palette("#2f5a2a", "#46782f", "#659a38", "#88b946", "#aed362")
LEAF_OAK = palette("#1c4421", "#2a5e28", "#3d7a30", "#58973c", "#7bb84c")
LEAF_AUTUMN = palette("#5a1f15", "#8a3318", "#b9501d", "#da792a", "#f2a447")
MOSS = palette("#2a5a2a", "#3d7c35", "#56994a")


class Canvas:
    def __init__(self, width, height):
        self.w, self.h = width, height
        self.rgb = np.zeros((height, width, 3), dtype=np.uint8)
        self.alpha = np.zeros((height, width), dtype=bool)

    @property
    def shape(self):
        return (self.h, self.w)

    def paint(self, mask, colors):
        self.rgb[mask] = colors[mask]
        self.alpha |= mask

    def image(self):
        out = np.zeros((self.h, self.w, 4), dtype=np.uint8)
        out[..., :3] = self.rgb
        out[..., 3] = np.where(self.alpha, 255, 0)
        return Image.fromarray(out, "RGBA")


def grid(canvas):
    return np.mgrid[0:canvas.h, 0:canvas.w]


def interp_columns(points, xs):
    px = [p[0] for p in points]
    return np.interp(xs, px, [p[1] for p in points]), np.interp(xs, px, [p[2] for p in points])


# ---------- trunks ----------
def draw_trunk(canvas, cx, y0, ground, width, ramp, seed, flare=7, moss=True, grooves=0.7):
    ys, xs = grid(canvas)
    px, py = xs + 0.5, ys + 0.5
    below = np.clip((py - (ground - 26)) / 26.0, 0, 1)
    extra = flare * below ** 1.6
    half = width / 2.0 + extra
    inside = (np.abs(px - cx) <= half) & (py >= y0) & (py < canvas.h)
    u = np.clip((px - (cx - half)) / (2 * half), 0, 1)
    fine = aniso_noise(canvas.w, canvas.h, 1.6, 9.0, seed)
    big = value_noise(canvas.w, canvas.h, 11, seed + 1)
    tone = (len(ramp) - 1.6) - (len(ramp) - 1.6) * 0.95 * u + (fine - 0.5) * 1.9 + (big - 0.5) * 0.9
    index = np.clip(np.round(tone).astype(int), 0, len(ramp) - 1)
    groove = (hash2(xs // 2, 0, seed) > grooves) & (fine > 0.35) & (u > 0.12) & (u < 0.88)
    index = np.where(groove, np.maximum(index - 1, 0), index)
    colors = ramp[index]
    # a lit left edge and a dark right edge outline the trunk
    left_edge = inside & ~shift(inside, 1, 0)
    right_edge = inside & ~shift(inside, -1, 0)
    colors[left_edge] = ramp[min(len(ramp) - 1, 3)]
    colors[right_edge] = ramp[0]
    if moss:
        patch = value_noise(canvas.w, canvas.h, 5, seed + 9) > 0.68
        base = (py > ground - 30) & (u < 0.45) & patch
        mi = np.clip(np.round(0.8 + (1 - u) * 2.2 + (fine - 0.5) * 1.6).astype(int), 0, 2)
        colors = np.where((inside & base)[..., None], MOSS[mi], colors)
    canvas.paint(inside, colors)
    return inside, half, u


def draw_knots(canvas, rng, cx, y_lo, y_hi, half, ramp, count):
    for _ in range(count):
        ky = rng.uniform(y_lo, y_hi)
        kx = cx + rng.uniform(-half * 0.45, half * 0.45)
        ys, xs = grid(canvas)
        mask = ((xs + 0.5 - kx) / 3.0) ** 2 + ((ys + 0.5 - ky) / 5.0) ** 2 <= 1.0
        mask &= canvas.alpha
        canvas.rgb[mask] = ramp[0]
        inner = ((xs + 0.5 - kx) / 1.7) ** 2 + ((ys + 0.5 - ky) / 3.2) ** 2 <= 1.0
        canvas.rgb[inner & mask] = ramp[1]
        rim = mask & ~shift(mask, 1, 1)
        canvas.rgb[rim] = ramp[3]


def draw_stubs(canvas, rng, cx, y_lo, y_hi, half, ramp, count, leafy=True, needles=None):
    for _ in range(count):
        side = rng.choice([-1, 1])
        y = rng.uniform(y_lo, y_hi)
        length = rng.uniform(6, 11)
        a = (cx + side * (half - 1), y)
        b = (a[0] + side * length, y - length * rng.uniform(0.1, 0.5))
        stub = capsule_mask(canvas.shape, a, b, 2.0, 1.0)
        shade = ramp[np.clip(np.round(2.2 - (np.mgrid[0:canvas.h, 0:canvas.w][0] - y) * 0.15).astype(int), 0, len(ramp) - 1)]
        canvas.paint(stub, shade)
        if leafy and needles is not None:
            tuft = capsule_mask(canvas.shape, b, (b[0] + side * 4, b[1] + 3), 2.2, 0.8)
            canvas.paint(tuft, needles[np.clip(np.round(2 + hash2(*np.mgrid[0:canvas.h, 0:canvas.w][::-1], 5) * 2).astype(int), 0, len(needles) - 1)])


# ---------- conifers ----------
def draw_frond(canvas, rng, start, length, side, droop, thickness, ramp, shade_seed, curl=0.12):
    steps = max(6, int(length / 3))
    pts = []
    for i in range(steps + 1):
        t = i / steps
        x = start[0] + side * length * t
        y = start[1] + droop * t ** 1.35 - curl * length * t ** 4
        r = thickness * (1.0 - 0.82 * t ** 0.85) + 0.9
        pts.append((x, y, r))
    mask = np.zeros(canvas.shape, dtype=bool)
    for (x0, y0, r0), (x1, y1, r1) in zip(pts[:-1], pts[1:]):
        mask |= capsule_mask(canvas.shape, (x0, y0), (x1, y1), r0, r1)
    ys, xs = grid(canvas)
    px, py = xs + 0.5, ys + 0.5
    cy, rr = interp_columns([(x, y, r) for x, y, r in pts][::1] if side > 0 else pts[::-1], px[0])
    v = (py - cy[None, :]) / np.maximum(rr[None, :], 0.5)
    clump = value_noise(canvas.w, canvas.h, 3, shade_seed)
    t_along = np.clip((px - start[0]) * side / max(length, 1), 0, 1)
    tone = 2.9 - 1.6 * np.clip(v, -1.3, 1.3) + (clump - 0.5) * 1.8 - 0.5 * side * 0.4 + 0.35 * (1 - t_along)
    tone = tone + (bayer(canvas.shape) - 0.5) * 1.1 + (hash2(xs, ys, shade_seed + 21) - 0.5) * 0.7
    index = np.clip(np.round(tone).astype(int), 0, len(ramp) - 1)
    colors = ramp[index]
    lower = mask & ~shift(mask, 0, -1)
    upper = mask & ~shift(mask, 0, 1)
    colors[lower] = ramp[np.maximum(index[lower] - 2, 0)]
    colors[upper & (hash2(xs, ys, shade_seed) > 0.45)] = ramp[min(len(ramp) - 1, 5)]
    # jagged needle tips hanging from the underside
    spikes = shift(lower & (hash2(xs, ys, shade_seed + 3) > 0.42), 0, -1) & ~mask
    spikes |= shift(lower & (hash2(xs, ys, shade_seed + 8) > 0.72), 0, -2) & ~mask
    colors[spikes] = ramp[1]
    canvas.paint(mask | spikes, colors)
    # drooping needle tufts along the frond
    for _ in range(int(length / 11)):
        t = rng.uniform(0.25, 0.95)
        x = start[0] + side * length * t
        y = start[1] + droop * t ** 1.35 + thickness * (1 - t) * 0.4
        tuft = capsule_mask(canvas.shape, (x, y), (x + side * rng.uniform(-1, 4), y + rng.uniform(5, 9)), 1.9, 0.7)
        tuft &= ~mask
        tuft_ys = np.mgrid[0:canvas.h, 0:canvas.w][0]
        tuft_colors = ramp[np.clip(np.round(2.2 - (tuft_ys - y) * 0.12 + (clump - 0.5)).astype(int), 0, len(ramp) - 1)]
        canvas.paint(tuft, tuft_colors)


def make_conifer(height_tiles, seed, redwood=False):
    rng = np.random.default_rng(seed)
    h = height_tiles * TILE + EMBED
    w = 232 if not redwood else 168
    canvas = Canvas(w, h)
    cx = w // 2
    ground = h - EMBED
    bare = int(height_tiles * TILE * (0.46 if redwood else 0.30))
    canopy_bottom = ground - bare
    top = 4
    ramp = NEEDLE_RED if redwood else NEEDLE
    bark = BARK_RED if redwood else BARK_PINE
    trunk_w = 22 if redwood else 20
    inside, half, _ = draw_trunk(canvas, cx, top + 10, ground, trunk_w, bark, seed, flare=8 if redwood else 7, grooves=0.62 if redwood else 0.7)
    draw_knots(canvas, rng, cx, canopy_bottom, ground - 14, trunk_w / 2, bark, 3 if not redwood else 5)
    draw_stubs(canvas, rng, cx, canopy_bottom + 4, ground - 30, trunk_w / 2, bark, 5, needles=ramp)

    tiers = 11 if redwood else 7 + (height_tiles >= 12) + (height_tiles >= 14)
    reach = 52 if redwood else 98
    for i in range(tiers):
        f = i / (tiers - 1)
        y = top + 12 + f * (canopy_bottom - top - 26)
        length = (14 + (reach - 14) * f ** 0.85) * rng.uniform(0.94, 1.06)
        thick = (4.0 + 5.0 * f) * (0.8 if redwood else 1.0)
        for side in (-1, 1):
            fronds = [(1.0, 0.0, 0.40), (0.78, 4.0, 0.36), (0.55, 8.0, 0.30)]
            for k, (scale, dy, droop_f) in enumerate(fronds):
                L = length * scale * rng.uniform(0.92, 1.05)
                draw_frond(canvas, rng, (cx + side * 3, y + dy * (0.6 + f)), L, side,
                           L * droop_f * rng.uniform(0.9, 1.15) + 5, thick * (1.0 - 0.1 * k), ramp,
                           seed * 13 + i * 7 + k * 3 + (side > 0))
    # leader: the pointed top of the tree
    spire = capsule_mask(canvas.shape, (cx, top + 2), (cx, top + 26), 1.8, 5.0)
    canvas.paint(spire, ramp[np.clip(np.round(3.2 + (value_noise(w, h, 3, seed) - 0.5) * 2).astype(int), 0, 5)])
    return canvas


# ---------- broadleaf trees ----------
def leaf_blob(canvas, cx, cy, r, ramp, seed, squash=0.82, light=1.0):
    ys, xs = grid(canvas)
    px, py = xs + 0.5 - cx, (ys + 0.5 - cy) / squash
    angle = np.arctan2(py, px)
    wob = 1 + 0.16 * np.sin(angle * 3 + seed) + 0.1 * np.sin(angle * 5 + seed * 2.1) + 0.06 * np.sin(angle * 9 + seed * 0.7)
    dist = np.sqrt(px * px + py * py)
    mask = dist < r * wob
    clump = value_noise(canvas.w, canvas.h, 3, seed + 4)
    shade = -(px * 0.55 + py * 0.85) / max(r, 1) * 1.6 * light + 2.5
    shade = shade - np.clip((py / max(r, 1)) - 0.45, 0, 1) * 2.2
    tone = shade + (clump - 0.5) * 1.7 + (bayer(canvas.shape) - 0.5) * 0.9
    index = np.clip(np.round(tone).astype(int), 0, len(ramp) - 1)
    colors = ramp[index]
    sparkle = hash2(xs, ys, seed + 1)
    colors[mask & (sparkle > 0.955)] = ramp[len(ramp) - 1]
    colors[mask & (sparkle < 0.04)] = ramp[0]
    lower = mask & ~shift(mask, 0, -1)
    colors[lower] = ramp[np.maximum(index[lower] - 1, 0)]
    canvas.paint(mask, colors)
    # loose leaves just outside the silhouette
    specks = dilate_ring(mask) & (hash2(xs, ys, seed + 2) > 0.8) & ~mask
    canvas.paint(specks, ramp[np.clip(index + 1, 0, len(ramp) - 1)])


def dilate_ring(mask):
    return shift(mask, 1, 0) | shift(mask, -1, 0) | shift(mask, 0, 1) | shift(mask, 0, -1)


def limb(canvas, a, b, ra, rb, ramp, seed):
    mask = capsule_mask(canvas.shape, a, b, ra, rb)
    ys, xs = grid(canvas)
    n = aniso_noise(canvas.w, canvas.h, 2.0, 6.0, seed)
    side = np.clip((xs + 0.5 - min(a[0], b[0])) / max(abs(a[0] - b[0]) + 2 * ra, 1), 0, 1)
    tone = 3.2 - 2.0 * side + (n - 0.5) * 1.8
    colors = ramp[np.clip(np.round(tone).astype(int), 0, len(ramp) - 1)]
    colors[mask & ~shift(mask, 0, 1)] = ramp[3]
    colors[mask & ~shift(mask, 0, -1)] = ramp[0]
    canvas.paint(mask, colors)


def make_birch(height_tiles, seed):
    rng = np.random.default_rng(seed)
    h = height_tiles * TILE + EMBED
    w = 196
    canvas = Canvas(w, h)
    cx = w // 2
    ground = h - EMBED
    trunk_top = ground - int(height_tiles * TILE * 0.72)
    inside, half, u = draw_trunk(canvas, cx, trunk_top, ground, 16, BARK_BIRCH, seed, flare=4, moss=False, grooves=0.97)
    ys, xs = grid(canvas)
    # dark horizontal birch marks, shorter marks on the shaded side
    y = ground - 18.0
    while y > trunk_top + 8:
        span = rng.uniform(0.35, 0.8)
        x0 = cx - half + rng.uniform(0, 16 * (1 - span))
        row = (ys == int(y)) | ((ys == int(y) + 1) & (rng.random() < 0.4))
        canvas.rgb[inside & row & (xs + 0.5 >= x0) & (xs + 0.5 <= x0 + 16 * span)] = BLACK_MARK
        y -= rng.uniform(8, 19)
    # a few leaf clusters and thin white branches
    tips = [(cx, trunk_top - 8)]
    for i in range(6):
        side = -1 if i % 2 == 0 else 1
        by = trunk_top + 14 + i * (ground - trunk_top - 70) / 7 * 0.9
        length = rng.uniform(26, 44) * (1.0 - i * 0.04)
        angle = math.radians(rng.uniform(32, 58))
        a = (cx + side * 6, by)
        b = (a[0] + side * length * math.cos(angle), a[1] - length * math.sin(angle))
        limb(canvas, a, b, 2.6, 1.2, BARK_BIRCH, seed + i)
        canvas.rgb[capsule_mask(canvas.shape, a, (a[0] + side * 3, a[1] - 1), 1.4) & canvas.alpha] = BLACK_MARK
        tips.append(b)
    for k, (tx, ty) in enumerate(sorted(tips, key=lambda p: -p[1])):
        leaf_blob(canvas, tx, ty - 6, rng.uniform(15, 23), LEAF_BIRCH, seed * 5 + k * 3, squash=0.85)
    for k in range(4):
        leaf_blob(canvas, cx + rng.uniform(-22, 22), trunk_top - rng.uniform(0, 30), rng.uniform(12, 18), LEAF_BIRCH, seed * 7 + k, squash=0.85)
    return canvas


def make_oak(height_tiles, seed, autumn=False):
    rng = np.random.default_rng(seed)
    h = height_tiles * TILE + EMBED
    w = 264
    canvas = Canvas(w, h)
    cx = w // 2
    ground = h - EMBED
    trunk_h = int(height_tiles * TILE * 0.40)
    split_y = ground - trunk_h
    crown_cy = split_y - 44
    draw_trunk(canvas, cx, split_y - 4, ground, 22, BARK_OAK, seed, flare=8, grooves=0.62)
    draw_knots(canvas, rng, cx, split_y + 10, ground - 18, 11, BARK_OAK, 3)
    leaves = LEAF_AUTUMN if autumn else LEAF_OAK
    # visible limbs fan out from the top of the trunk into the crown
    for side, spread, rise in ((-1, 62, 34), (1, 58, 30), (-1, 36, 62), (1, 40, 58), (-1, 14, 70), (1, 12, 74)):
        a = (cx + side * 4, split_y + 2)
        b = (cx + side * spread, split_y - rise)
        limb(canvas, a, b, 4.2, 1.8, BARK_OAK, seed + spread)
    lobes = []
    for i in range(15):
        angle = math.pi * (i + rng.uniform(-0.2, 0.2)) / 14
        rx, ry = 66 + rng.uniform(-6, 8), 40 + rng.uniform(-4, 6)
        lobes.append((cx - math.cos(angle) * rx, crown_cy - math.sin(angle) * ry + 16, rng.uniform(22, 31)))
    for i in range(6):
        lobes.append((cx + rng.uniform(-44, 44), crown_cy + rng.uniform(-18, 22), rng.uniform(26, 34)))
    for k, (lx, ly, lr) in enumerate(sorted(lobes, key=lambda b: -b[1])):
        leaf_blob(canvas, lx, ly, lr, leaves, seed * 3 + k * 7, squash=0.82)
    return canvas


SPECIES = [
    # name, file prefix, maker, heights, weight
    ("pine", "pine", lambda ht, s: make_conifer(ht, s), [10, 11, 12, 13, 14], 38, 20, 0.30, 7.0),
    ("redwood", "redwood", lambda ht, s: make_conifer(ht, s, redwood=True), [14, 15, 14], 16, 22, 0.46, 5.0),
    ("birch", "birch", lambda ht, s: make_birch(ht, s), [10, 11, 12, 13], 20, 16, 0.38, 8.0),
    ("oak", "oak", lambda ht, s: make_oak(ht, s), [9, 10, 11], 18, 22, 0.40, 6.0),
    ("autumn_oak", "oak_autumn", lambda ht, s: make_oak(ht, s, autumn=True), [10, 11], 8, 22, 0.40, 6.0),
]


def build(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    entries, previews = [], []
    for name, prefix, maker, heights, weight, trunk_px, leaf_start, sway in SPECIES:
        for index, ht in enumerate(heights):
            canvas = maker(ht, 100 + index * 31 + len(name) * 7)
            file = f"{prefix}_{index}.png"
            canvas.image().save(os.path.join(out_dir, file))
            entries.append(dict(file=file, species=name, width=canvas.w, height=canvas.h, height_tiles=ht,
                                weight=weight / len(heights), trunk_px=trunk_px, leaf_start=leaf_start, sway=sway,
                                bare_rows=int(round(ht * leaf_start)),
                                trunk_rows=min(ht - 2, int(round(ht * (leaf_start + 0.18))))))
            previews.append(canvas.image())
    with open(os.path.join(out_dir, "trees.json"), "w") as handle:
        json.dump(dict(embed=EMBED, trees=entries), handle, indent=1)
    return previews


if __name__ == "__main__":
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
    images = build(os.path.join(root, "assets", "foliage", "trees"))
    if len(sys.argv) > 1:
        total = sum(i.width for i in images) + 8 * len(images)
        sheet = Image.new("RGBA", (total, max(i.height for i in images)), (92, 140, 190, 255))
        x = 0
        for image in images:
            sheet.alpha_composite(image, (x, sheet.height - image.height))
            x += image.width + 8
        sheet.convert("RGB").save(sys.argv[1])
    print("wrote", len(images), "trees")
