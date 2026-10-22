"""Generates the forest-floor props atlas (assets/foliage/props.png + props.json).

Props are drawn 1:1 with their feet on the bottom row; the game stands them on the ground line.

Run:  python tools/make_props.py [preview.png]
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image

from art_common import bayer, capsule_mask, hash2, palette, shift, value_noise
from make_trees import (BARK_OAK, Canvas, LEAF_OAK, MOSS, dilate_ring, leaf_blob)

GRASS = palette("#1f4524", "#2f6330", "#43833a", "#62a346", "#8bc45c")
STONE = palette("#3f4350", "#565b6a", "#6e7485", "#8b92a4", "#adb4c4")
BERRY = np.array([0xc0, 0x2f, 0x44], dtype=np.uint8)
BERRY_LIGHT = np.array([0xf0, 0x7a, 0x84], dtype=np.uint8)
WOOD = palette("#4a3322", "#6b4a31", "#8f6743", "#b88a58", "#d9b27a")
FLOWERS = {
    "white": (np.array([0xf6, 0xf3, 0xea]), np.array([0xe3, 0xcf, 0x7a]), np.array([0xc9, 0xc6, 0xbd])),
    "yellow": (np.array([0xf5, 0xd0, 0x4a]), np.array([0xc9, 0x7a, 0x1f]), np.array([0xd8, 0xa0, 0x30])),
    "pink": (np.array([0xe9, 0x74, 0x9a]), np.array([0xf6, 0xd7, 0x6a]), np.array([0xb8, 0x4a, 0x72])),
    "blue": (np.array([0x7d, 0x9b, 0xf0]), np.array([0xf3, 0xe6, 0x9a]), np.array([0x55, 0x6c, 0xc4])),
}
CAP_RED = (np.array([0xc7, 0x3b, 0x3b]), np.array([0x8d, 0x24, 0x2b]), np.array([0xe8, 0x7c, 0x6c]))
CAP_BROWN = (np.array([0xa9, 0x7a, 0x4e]), np.array([0x75, 0x51, 0x32]), np.array([0xc9, 0xa0, 0x70]))
STEM = np.array([0xe6, 0xdc, 0xc4], dtype=np.uint8)


def blades(canvas, rng, base_x, base_y, count, spread, height, lean=0.5, tone=2.0):
    for i in range(count):
        x0 = base_x + rng.uniform(-spread, spread)
        h = height * rng.uniform(0.55, 1.0)
        lx = (x0 - base_x) / max(spread, 1) * lean * 4 + rng.uniform(-2.5, 2.5)
        pts = [(x0 + lx * (t ** 2), base_y - h * t) for t in np.linspace(0, 1, max(4, int(h / 2)))]
        for (xa, ya), (xb, yb) in zip(pts[:-1], pts[1:]):
            t = (base_y - ya) / max(h, 1)
            mask = capsule_mask(canvas.shape, (xa, ya), (xb, yb), 1.0 - 0.25 * t, 0.9 - 0.4 * t)
            index = int(np.clip(round(tone + t * 1.6 + rng.uniform(-0.6, 0.6)), 0, 4))
            canvas.paint(mask, np.broadcast_to(GRASS[index], canvas.rgb.shape))


def tuft(width, height, seed, count):
    rng = np.random.default_rng(seed)
    canvas = Canvas(width, height)
    blades(canvas, rng, width / 2, height - 0.5, count, width * 0.36, height - 2)
    return canvas


def flower_patch(kind, seed, stems=4):
    petals, center, shade = FLOWERS[kind]
    rng = np.random.default_rng(seed)
    canvas = Canvas(30, 30)
    blades(canvas, rng, 15, 29.5, 9, 10, 14)
    for i in range(stems):
        x = 15 + (i - (stems - 1) / 2) * rng.uniform(4.0, 6.5)
        top = 29.5 - rng.uniform(11, 21)
        sway = rng.uniform(-1.5, 1.5)
        canvas.paint(capsule_mask(canvas.shape, (x, 29.5), (x + sway, top), 0.7, 0.6), np.broadcast_to(GRASS[2], canvas.rgb.shape))
        hx, hy = x + sway, top
        head = capsule_mask(canvas.shape, (hx - 1.5, hy), (hx + 1.5, hy), 2.1) | capsule_mask(canvas.shape, (hx, hy - 1.5), (hx, hy + 1.5), 2.1)
        canvas.paint(head, np.broadcast_to(petals.astype(np.uint8), canvas.rgb.shape))
        lower = head & ~shift(head, 0, -1)
        canvas.rgb[lower] = shade
        canvas.rgb[int(hy), int(hx)] = center
    return canvas


def fern(size, seed):
    rng = np.random.default_rng(seed)
    w, h = size
    canvas = Canvas(w, h)
    for k in range(7):
        angle = math.radians(-70 + k * 23 + rng.uniform(-6, 6))
        length = h * rng.uniform(0.7, 1.0) * (1 - abs(k - 3) * 0.08)
        base = (w / 2, h - 0.5)
        pts = []
        for t in np.linspace(0, 1, 14):
            x = base[0] + math.sin(angle) * length * t + math.sin(angle) * 6 * t * t
            y = base[1] - math.cos(angle) * length * t + length * 0.55 * t ** 2.4
            pts.append((x, y))
        for (xa, ya), (xb, yb) in zip(pts[:-1], pts[1:]):
            canvas.paint(capsule_mask(canvas.shape, (xa, ya), (xb, yb), 0.8), np.broadcast_to(GRASS[1], canvas.rgb.shape))
        for i, (x, y) in enumerate(pts[2:-1]):
            span = 4.2 * (1 - i / 12) + 1.2
            for side in (-1, 1):
                leaflet = capsule_mask(canvas.shape, (x, y), (x + side * span, y + span * 0.55), 1.1, 0.5)
                canvas.paint(leaflet, np.broadcast_to(GRASS[2 + (side > 0) + (i % 3 == 0)], canvas.rgb.shape))
    return canvas


def bush(width, height, seed, berries=False, flowers=None):
    rng = np.random.default_rng(seed)
    canvas = Canvas(width, height)
    blobs = []
    for i in range(int(width / 9)):
        blobs.append((width * (0.18 + 0.64 * i / max(1, int(width / 9) - 1)) + rng.uniform(-3, 3), height - rng.uniform(8, height * 0.55), rng.uniform(9, 14)))
    blobs.append((width / 2, height * 0.42, height * 0.42))
    for k, (x, y, r) in enumerate(sorted(blobs, key=lambda b: -b[1])):
        leaf_blob(canvas, x, y, r, LEAF_OAK, seed * 3 + k, squash=0.85)
    # flat base so the bush sits on the ground
    canvas.alpha[height - 2:, :] &= canvas.alpha[height - 4:height - 2, :].any(axis=0)[None, :]
    ys, xs = np.mgrid[0:height, 0:width]
    if berries:
        for _ in range(int(width / 4)):
            x, y = rng.integers(5, width - 5), rng.integers(4, height - 5)
            if canvas.alpha[y, x]:
                canvas.rgb[y, x] = BERRY
                canvas.rgb[y - 1, x] = BERRY_LIGHT if canvas.alpha[y - 1, x] else BERRY
    if flowers:
        petals, center, _ = FLOWERS[flowers]
        for _ in range(int(width / 5)):
            x, y = rng.integers(4, width - 4), rng.integers(3, height - 6)
            if canvas.alpha[y, x]:
                canvas.rgb[y, x] = petals
                canvas.rgb[y + 1, x] = center
    return canvas


def rock(width, height, seed, mossy=True):
    rng = np.random.default_rng(seed)
    canvas = Canvas(width, height)
    ys, xs = np.mgrid[0:height, 0:width]
    px, py = xs + 0.5 - width / 2, ys + 0.5 - height * 0.62
    angle = np.arctan2(py / 0.9, px)
    wob = 1 + 0.12 * np.sin(angle * 2 + seed) + 0.08 * np.sin(angle * 5 + seed * 1.7)
    dist = np.sqrt((px / (width / 2)) ** 2 + (py / (height * 0.62)) ** 2)
    mask = (dist < wob) & (ys + 0.5 <= height - 0.5)
    mask &= ys > 0
    n = value_noise(width, height, 4, seed)
    light = -(px / (width / 2)) * 0.9 - (py / (height * 0.62)) * 1.2
    tone = 2.2 + light * 1.6 + (n - 0.5) * 1.8 + (bayer((height, width)) - 0.5) * 0.7
    index = np.clip(np.round(tone).astype(int), 0, 4)
    colors = STONE[index]
    # facets: a few darker crack lines and a lit top edge
    crack = (value_noise(width, height, 7, seed + 2) > 0.5) & (np.abs(value_noise(width, height, 3, seed + 3) - 0.5) < 0.04)
    colors[crack & mask] = STONE[0]
    colors[mask & ~shift(mask, 0, 1) & (hash2(xs, ys, seed) > 0.4)] = STONE[4]
    colors[mask & ~shift(mask, 0, -1)] = STONE[0]
    canvas.paint(mask, colors)
    if mossy:
        patch = (value_noise(width, height, 5, seed + 9) + (1 - ys / height) * 0.5 > 0.95) & mask
        mi = np.clip(np.round((value_noise(width, height, 3, seed + 4)) * 3).astype(int) - 0, 0, 2)
        canvas.rgb[patch] = MOSS[mi][patch]
    return canvas


def pebbles(seed):
    canvas = Canvas(26, 10)
    rng = np.random.default_rng(seed)
    for x, w, h in ((6, 8, 6), (15, 6, 4), (21, 4, 3)):
        part = rock(w + 2, h + 2, seed + x, mossy=False)
        canvas.paint(np.pad(part.alpha, ((10 - part.h, 0), (x - 1 - (part.w // 2) + 2, 26 - (x - 1 - (part.w // 2) + 2) - part.w)))[:10, :26] if False else np.zeros((10, 26), dtype=bool), np.zeros((10, 26, 3), dtype=np.uint8))
    return canvas


def mushrooms(seed, palette_caps):
    cap, dark, dot = palette_caps
    rng = np.random.default_rng(seed)
    canvas = Canvas(28, 18)
    for x, h, r in ((8, 9, 5), (17, 12, 6), (23, 7, 4)):
        stem = capsule_mask(canvas.shape, (x, 17.5), (x, 17.5 - h), 1.8, 1.5)
        canvas.paint(stem, np.broadcast_to(STEM, canvas.rgb.shape))
        cap_mask = capsule_mask(canvas.shape, (x - r * 0.45, 17.5 - h), (x + r * 0.45, 17.5 - h), r * 0.62)
        cap_mask &= np.mgrid[0:18, 0:28][0] <= 17.5 - h + 1
        canvas.paint(cap_mask, np.broadcast_to(cap.astype(np.uint8), canvas.rgb.shape))
        canvas.rgb[cap_mask & ~shift(cap_mask, 0, -1)] = dark
        for k in range(3):
            dx, dy = rng.integers(-3, 3), rng.integers(-3, 0)
            yy, xx = int(17.5 - h + dy + 1), int(x + dx)
            if 0 <= yy < 18 and 0 <= xx < 28 and cap_mask[yy, xx]:
                canvas.rgb[yy, xx] = dot
    return canvas


def stump(seed):
    rng = np.random.default_rng(seed)
    canvas = Canvas(34, 30)
    ys, xs = np.mgrid[0:30, 0:34]
    px, py = xs + 0.5, ys + 0.5
    top_y = 10
    half = 11 + (py > 22) * ((py - 22) / 8.0) * 4
    body = (np.abs(px - 17) <= half) & (py >= top_y) & (py < 30)
    n = np.zeros((30, 34))
    from art_common import aniso_noise
    n = aniso_noise(34, 30, 1.8, 7.0, seed)
    u = np.clip((px - (17 - half)) / (2 * half), 0, 1)
    tone = 3.1 - 2.0 * u + (n - 0.5) * 1.8
    colors = np.array([[0x2c, 0x20, 0x18], [0x40, 0x30, 0x24], [0x5b, 0x44, 0x32], [0x78, 0x59, 0x40], [0x96, 0x72, 0x50]], dtype=np.uint8)[np.clip(np.round(tone).astype(int), 0, 4)]
    canvas.paint(body, colors)
    top = (((px - 17) / 11.5) ** 2 + ((py - top_y) / 4.5) ** 2) <= 1.0
    rings = np.sqrt(((px - 17) / 11.5) ** 2 + ((py - top_y) / 4.5) ** 2)
    ring_index = np.clip(np.round(2.2 + np.sin(rings * 14) * 1.1).astype(int), 1, 4)
    canvas.paint(top, WOOD[ring_index])
    canvas.rgb[top & ~shift(top, 0, -1)] = WOOD[0]
    moss = (value_noise(34, 30, 4, seed + 1) > 0.62) & body & (py > 20) & (u < 0.55)
    canvas.rgb[moss] = MOSS[1]
    return canvas


def log(seed):
    rng = np.random.default_rng(seed)
    w, h = 80, 24
    canvas = Canvas(w, h)
    from art_common import aniso_noise
    ys, xs = np.mgrid[0:h, 0:w]
    px, py = xs + 0.5, ys + 0.5
    body = (py >= 6) & (py < h) & (px > 8) & (px < w - 1)
    n = aniso_noise(w, h, 9.0, 1.8, seed)
    v = np.clip((py - 6) / (h - 6), 0, 1)
    tone = 3.2 - 2.0 * v + (n - 0.5) * 1.6
    colors = np.array([[0x2c, 0x20, 0x18], [0x40, 0x30, 0x24], [0x5b, 0x44, 0x32], [0x78, 0x59, 0x40], [0x96, 0x72, 0x50]], dtype=np.uint8)[np.clip(np.round(tone).astype(int), 0, 4)]
    canvas.paint(body, colors)
    end = (((px - 10) / 5.5) ** 2 + ((py - 15) / 9.0) ** 2) <= 1.0
    rings = np.sqrt(((px - 10) / 5.5) ** 2 + ((py - 15) / 9.0) ** 2)
    canvas.paint(end, WOOD[np.clip(np.round(2.4 + np.sin(rings * 9) * 1.1).astype(int), 1, 4)])
    canvas.rgb[end & ~shift(end, 1, 0)] = WOOD[0]
    moss = (value_noise(w, h, 5, seed + 1) > 0.52) & body & (py < 13)
    canvas.rgb[moss] = MOSS[np.clip(np.round(value_noise(w, h, 3, seed + 2) * 2).astype(int), 0, 2)][moss]
    canvas.paint(capsule_mask(canvas.shape, (50, 6.5), (58, 1.5), 2.2, 1.0), np.broadcast_to(BARK_OAK[2], canvas.rgb.shape))  # a broken branch stub
    return canvas


def build_props():
    props = []

    def add(name, kind, canvas, **extra):
        props.append((name, kind, canvas, extra))

    for i, (w, h, n) in enumerate([(12, 11, 5), (18, 15, 8), (24, 20, 11), (16, 13, 6), (22, 17, 9)]):
        add(f"tuft_{i}", "tuft", tuft(w, h, 40 + i, n))
    for i, kind in enumerate(["white", "yellow", "pink", "blue", "white", "yellow"]):
        add(f"flowers_{kind}_{i}", "flower", flower_patch(kind, 60 + i, stems=3 + i % 3))
    add("fern_0", "fern", fern((30, 24), 71))
    add("fern_1", "fern", fern((40, 30), 72))
    add("bush_0", "bush", bush(34, 24, 81))
    add("bush_1", "bush", bush(46, 30, 82, berries=True))
    add("bush_2", "bush", bush(58, 34, 83))
    add("bush_3", "bush", bush(40, 26, 84, flowers="pink"))
    add("rock_0", "rock", rock(18, 12, 91))
    add("rock_1", "rock", rock(28, 19, 92))
    add("rock_2", "rock", rock(42, 28, 93))
    add("rock_3", "rock", rock(24, 15, 94, mossy=False))
    add("mushroom_red", "mushroom", mushrooms(95, CAP_RED))
    add("mushroom_brown", "mushroom", mushrooms(96, CAP_BROWN))
    add("stump_0", "stump", stump(97))
    add("log_0", "log", log(98), width_tiles=4)
    return props


def pack(props, width=420):
    placements, x, y, row_h = [], 2, 2, 0
    for name, kind, canvas, extra in props:
        if x + canvas.w + 2 > width:
            x, y, row_h = 2, y + row_h + 2, 0
        placements.append((name, kind, canvas, extra, x, y))
        x += canvas.w + 2
        row_h = max(row_h, canvas.h)
    height = y + row_h + 2
    atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    entries = []
    for name, kind, canvas, extra, px, py in placements:
        atlas.alpha_composite(canvas.image(), (px, py))
        entries.append(dict(name=name, kind=kind, x=px, y=py, w=canvas.w, h=canvas.h, **extra))
    return atlas, entries


if __name__ == "__main__":
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
    out_dir = os.path.join(root, "assets", "foliage")
    atlas, entries = pack(build_props())
    atlas.save(os.path.join(out_dir, "props.png"))
    with open(os.path.join(out_dir, "props.json"), "w") as handle:
        json.dump(dict(props=entries), handle, indent=1)
    if len(sys.argv) > 1:
        big = Image.new("RGBA", atlas.size, (84, 126, 176, 255))
        big.alpha_composite(atlas)
        big.resize((big.width * 3, big.height * 3), Image.NEAREST).convert("RGB").save(sys.argv[1])
    print("wrote", len(entries), "props", atlas.size)
