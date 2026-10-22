"""Generates the 24x24 block textures (assets/tiles/tile_sheet.png).

Sheet: 4 variants per row, 24px per tile. Rows:
 0 dirt, 1 grass-topped dirt, 2 stone, 3 mossy stone, 4 ore-flecked stone,
 5 deep stone, 6 deep stone with crystals, 7 dirt cave wall, 8 stone cave wall,
 9 overlays (grass wrapping the left side, grass wrapping the right side),
 10 mining cracks (three growing stages, drawn over a block being mined).

Run:  python tools/make_tiles.py
"""
import os

import numpy as np

from art_common import (ellipse_mask, palette, quantize, shift, to_image, value_noise)

S = 24
VARIANTS = 4
ROWS = 11

DIRT = palette("#3f2c20", "#5a3f2c", "#6e4d34", "#85603f")
GRASS = palette("#264d27", "#356b32", "#4a8a3b", "#68a847", "#8ec35e")
STONE = palette("#4d525f", "#5d6371", "#717887", "#8a91a1")
STONE_CRACK = (0x33, 0x36, 0x42)
MOSS = palette("#27502a", "#386d34", "#4d8a3d")
ORE = palette("#8a4a22", "#c47a3a", "#ebab62")
DEEP = palette("#2f3247", "#3d4160", "#4f5478", "#666c96")
DEEP_CRACK = (0x1f2133)
CRYSTAL = palette("#2a8fa6", "#5fd0e0", "#c4f6ff")
WALL_DIRT = palette("#241811", "#2e2017", "#38281d")
WALL_STONE = palette("#262933", "#2f333f", "#393e4c")
PEBBLE = palette("#58554f", "#86827a", "#aaa69c")
ROOT = (0x9a, 0x76, 0x48)


def paint(index, colors):
    return colors[index]


def dirt_rgb(seed, colors=DIRT, contrast=1.0):
    rng = np.random.default_rng(seed)
    n = 0.55 * value_noise(S, S, 8, seed, wrap=True) + 0.45 * value_noise(S, S, 4, seed + 7, wrap=True)
    n = 0.5 + (n - 0.5) * contrast
    index = quantize(n, [0.38, 0.54, 0.70])
    # random speckles one step lighter or darker keep the surface lively
    speck = rng.random((S, S))
    index = np.where(speck < 0.06, np.maximum(index - 1, 0), index)
    index = np.where(speck > 0.95, np.minimum(index + 1, len(colors) - 1), index)
    return paint(index, colors).copy(), rng


def add_pebbles(rgb, rng, count):
    for _ in range(count):
        cx, cy = rng.integers(5, S - 5), rng.integers(5, S - 5)
        rx, ry = rng.choice([1.6, 2.0, 2.4]), rng.choice([1.3, 1.6, 1.9])
        mask = ellipse_mask((S, S), cx, cy, rx, ry)
        rgb[mask] = PEBBLE[1]
        rgb[mask & ~shift(mask, 1, 1)] = PEBBLE[2]
        rgb[mask & ~shift(mask, -1, -1)] = PEBBLE[0]
        shadow = shift(mask, 1, 1) & ~mask
        rgb[shadow] = (rgb[shadow] * 0.65).astype(np.uint8)


def add_roots(rgb, rng, count):
    for _ in range(count):
        x, y = int(rng.integers(4, S - 8)), int(rng.integers(4, S - 6))
        direction = int(rng.choice([-1, 1]))
        for step in range(int(rng.integers(5, 9))):
            if 1 <= x < S - 1 and 1 <= y < S - 1:
                rgb[y, x] = ROOT
                rgb[y + 1, x] = (0x6a, 0x4c, 0x2c)
            x += direction
            y += int(rng.choice([0, 0, 1, -1]))


def make_dirt(seed):
    rgb, rng = dirt_rgb(seed)
    add_pebbles(rgb, rng, int(rng.integers(1, 3)))
    add_roots(rgb, rng, int(rng.integers(0, 2)))
    return rgb


def make_grass_cap(seed):
    rgb, rng = dirt_rgb(seed + 100)
    # ragged lower edge of the grass: blades hang down further in some columns
    base = 5 + (value_noise(S, S, 3, seed + 3, wrap=True)[0] * 3).astype(int)
    hang = np.where(rng.random(S) < 0.4, rng.integers(1, 4, S), 0)
    depth = base + hang
    shade_noise = value_noise(S, S, 3, seed + 11, wrap=True)
    ys, xs = np.mgrid[0:S, 0:S]
    grass = ys < depth[xs]
    tone = 1.2 + shade_noise * 1.8 + (1.0 - ys / np.maximum(depth[xs], 1)) * 1.3
    index = np.clip(tone.astype(int), 0, len(GRASS) - 1)
    rgb[grass] = GRASS[index][grass]
    # bright top row with occasional taller highlights, dark contact line under the grass
    top_light = rng.random(S) < 0.55
    rgb[0, top_light] = GRASS[4]
    rgb[0, ~top_light] = GRASS[3]
    for x in range(S):
        y = depth[x]
        if y < S:
            rgb[y, x] = (rgb[y, x] * 0.55).astype(np.uint8)
    for x in np.nonzero(rng.random(S) < 0.35)[0]:
        y = depth[x] - 1
        if 0 < y < S:
            rgb[y, x] = GRASS[0]
    add_pebbles(rgb[10:], rng, 1) if False else None
    return rgb


def grass_edges():
    left = np.zeros((S, S, 4), dtype=np.uint8)
    rng = np.random.default_rng(404)
    for y in range(0, 11):
        width = 3 if y < 5 else 2 if y < 8 else 1
        width = max(width + int(rng.integers(-1, 2)), 1) if y > 3 else width
        for x in range(width):
            index = 3 if x == 0 and y < 3 else 2 if x == 0 else 1
            left[y, x] = (*GRASS[index], 255)
    right = left[:, ::-1].copy()
    return left, right


def voronoi_stone(seed, colors, crack, points=6):
    rng = np.random.default_rng(seed)
    centers = rng.random((points, 2)) * S
    ys, xs = np.mgrid[0:S, 0:S]
    px, py = xs + 0.5, ys + 0.5
    best1 = np.full((S, S), 1e9)
    best2 = np.full((S, S), 1e9)
    owner = np.zeros((S, S), dtype=int)
    offsets = np.zeros((S, S, 2))
    for i, (cx, cy) in enumerate(centers):
        for ox in (-S, 0, S):
            for oy in (-S, 0, S):
                dx, dy = px - (cx + ox), py - (cy + oy)
                d = np.sqrt(dx * dx + dy * dy)
                better = d < best1
                second = (~better) & (d < best2)
                best2 = np.where(better, best1, np.where(second, d, best2))
                owner = np.where(better, i, owner)
                offsets[better] = np.stack([dx, dy], axis=-1)[better]
                best1 = np.where(better, d, best1)
    base_tone = (rng.random(points) * 2.2).astype(int)
    index = base_tone[owner].copy()
    # light from the top-left: the lit side of every stone is lighter, the far side darker
    facing = -(offsets[..., 0] + offsets[..., 1]) / (best1 + 1e-6)
    index = index + (facing > 0.55).astype(int) - (facing < -0.55).astype(int)
    index = np.clip(index + (rng.random((S, S)) > 0.93).astype(int), 0, len(colors) - 1)
    rgb = colors[index].copy()
    seam = (best2 - best1) < 1.15
    rgb[seam] = crack
    # a lighter lip right under a crack gives the plates some thickness
    lip = shift(seam, 0, 1) & ~seam
    rgb[lip] = colors[np.clip(index[lip] + 1, 0, len(colors) - 1)]
    return rgb, rng


def make_stone(seed):
    return voronoi_stone(seed, STONE, STONE_CRACK)[0]


def make_moss_stone(seed):
    rgb, rng = voronoi_stone(seed, STONE, STONE_CRACK)
    patch = value_noise(S, S, 6, seed + 5, wrap=True) + (1 - np.mgrid[0:S, 0:S][0] / S) * 0.3
    index = quantize(np.clip((patch - 0.8) * 4.0, 0, 0.999), [0.0001, 0.4, 0.75]) - 1
    mask = index >= 0
    rgb[mask] = MOSS[np.clip(index, 0, 2)][mask]
    return rgb


def make_ore_stone(seed):
    rgb, rng = voronoi_stone(seed, STONE, STONE_CRACK)
    for _ in range(3):
        cx, cy = int(rng.integers(4, S - 5)), int(rng.integers(4, S - 5))
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1), (-1, 0), (0, -1)):
            if rng.random() < 0.8:
                rgb[cy + dy, cx + dx] = ORE[1]
        rgb[cy, cx] = ORE[2]
        rgb[cy + 1, cx + 1] = ORE[0]
    return rgb


def make_deep(seed):
    return voronoi_stone(seed + 50, DEEP, (0x1f, 0x21, 0x33), points=7)[0]


def make_deep_crystal(seed):
    rgb, rng = voronoi_stone(seed + 50, DEEP, (0x1f, 0x21, 0x33), points=7)
    for _ in range(2):
        cx, cy = int(rng.integers(4, S - 6)), int(rng.integers(5, S - 5))
        for i in range(3):
            rgb[cy - i, cx] = CRYSTAL[1]
            rgb[cy - i, cx + 1] = CRYSTAL[0]
        rgb[cy - 2, cx] = CRYSTAL[2]
        rgb[cy + 1, cx + 1] = CRYSTAL[0]
    return rgb


def make_wall(seed, colors, crack=None):
    n = 0.6 * value_noise(S, S, 8, seed, wrap=True) + 0.4 * value_noise(S, S, 4, seed + 3, wrap=True)
    rgb = colors[quantize(n, [0.42, 0.62])].copy()
    rng = np.random.default_rng(seed)
    if crack is not None:
        for _ in range(2):
            x, y = int(rng.integers(3, S - 3)), int(rng.integers(3, S - 3))
            for _ in range(int(rng.integers(4, 8))):
                rgb[y % S, x % S] = crack
                x += int(rng.choice([-1, 1]))
                y += int(rng.choice([0, 1]))
    return rgb


def crack_stages():
    """Three transparent overlays: a few fine cracks, a spreading web, and a nearly shattered block."""
    rng = np.random.default_rng(77)
    stages = []
    paths = []
    for _ in range(6):
        x, y = float(rng.integers(8, 16)), float(rng.integers(8, 16))
        angle = rng.uniform(0, 6.283)
        path = []
        for _ in range(int(rng.integers(7, 12))):
            path.append((int(round(x)), int(round(y))))
            angle += rng.uniform(-0.7, 0.7)
            x += np.cos(angle) * 1.4
            y += np.sin(angle) * 1.4
        paths.append(path)
    for stage, count in enumerate((2, 4, 6)):
        layer = np.zeros((S, S, 4), dtype=np.uint8)
        for path in paths[:count]:
            for x, y in path[: 5 + stage * 3]:
                if 0 <= x < S and 0 <= y < S:
                    layer[y, x] = (18, 12, 14, 210)
                    if 0 <= x + 1 < S and stage > 0:
                        layer[y, x + 1] = (18, 12, 14, 120)
        stages.append(layer)
    return stages


def build():
    sheet = np.zeros((S * ROWS, S * VARIANTS, 4), dtype=np.uint8)

    def place(row, column, rgb, alpha=None):
        out = np.zeros((S, S, 4), dtype=np.uint8)
        out[..., :3] = rgb
        out[..., 3] = 255 if alpha is None else alpha
        sheet[row * S:(row + 1) * S, column * S:(column + 1) * S] = out

    for v in range(VARIANTS):
        seed = 1000 + v * 37
        place(0, v, make_dirt(seed))
        place(1, v, make_grass_cap(seed))
        place(2, v, make_stone(seed))
        place(3, v, make_moss_stone(seed))
        place(4, v, make_ore_stone(seed))
        place(5, v, make_deep(seed))
        place(6, v, make_deep_crystal(seed))
        place(7, v, make_wall(seed, WALL_DIRT))
        place(8, v, make_wall(seed, WALL_STONE, crack=(0x20, 0x23, 0x2c)))
    left, right = grass_edges()
    sheet[9 * S:10 * S, 0:S] = left
    sheet[9 * S:10 * S, S:2 * S] = right
    for stage, layer in enumerate(crack_stages()):
        sheet[10 * S:11 * S, stage * S:(stage + 1) * S] = layer
    return sheet


if __name__ == "__main__":
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
    out_dir = os.path.join(root, "assets", "tiles")
    os.makedirs(out_dir, exist_ok=True)
    sheet = build()
    to_image(sheet[..., :3], sheet[..., 3]).save(os.path.join(out_dir, "tile_sheet.png"))
    print("wrote", os.path.join(out_dir, "tile_sheet.png"))
