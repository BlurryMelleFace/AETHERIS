"""Shared helpers for the procedural pixel-art generators in tools/."""
import math

import numpy as np
from PIL import Image


def hexc(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def palette(*hexes):
    return np.array([hexc(h) for h in hexes], dtype=np.uint8)


def smoothstep(t):
    return t * t * (3 - 2 * t)


def value_noise(width, height, cell, seed, wrap=False):
    """Smooth value noise in [0, 1]; with wrap=True it tiles seamlessly (cell must divide the size)."""
    rng = np.random.default_rng(seed)
    gx, gy = int(math.ceil(width / cell)) + 2, int(math.ceil(height / cell)) + 2
    if wrap:
        gx, gy = int(round(width / cell)), int(round(height / cell))
    grid = rng.random((gy, gx))
    ys = (np.arange(height) + 0.5) / cell
    xs = (np.arange(width) + 0.5) / cell
    y0, x0 = np.floor(ys).astype(int), np.floor(xs).astype(int)
    fy, fx = smoothstep(ys - y0)[:, None], smoothstep(xs - x0)[None, :]
    y1, x1 = y0 + 1, x0 + 1
    if wrap:
        y0, y1, x0, x1 = y0 % gy, y1 % gy, x0 % gx, x1 % gx
    a = grid[y0][:, x0]
    b = grid[y0][:, x1]
    c = grid[y1][:, x0]
    d = grid[y1][:, x1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def aniso_noise(width, height, cell_x, cell_y, seed):
    """Value noise stretched differently along x and y, e.g. long vertical bark streaks."""
    rng = np.random.default_rng(seed)
    gx, gy = int(math.ceil(width / cell_x)) + 2, int(math.ceil(height / cell_y)) + 2
    grid = rng.random((gy, gx))
    ys = (np.arange(height) + 0.5) / cell_y
    xs = (np.arange(width) + 0.5) / cell_x
    y0, x0 = np.floor(ys).astype(int), np.floor(xs).astype(int)
    fy, fx = smoothstep(ys - y0)[:, None], smoothstep(xs - x0)[None, :]
    a, b = grid[y0][:, x0], grid[y0][:, x0 + 1]
    c, d = grid[y0 + 1][:, x0], grid[y0 + 1][:, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def hash2(x, y, seed=0):
    """Cheap deterministic per-pixel hash in [0, 1) for arrays of integer coordinates."""
    h = (x * 374761393 + y * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65536.0


def capsule_mask(shape, a, b, ra, rb=None):
    """Filled capsule between points a and b, evaluated only inside its bounding box."""
    height, width = shape
    rb = ra if rb is None else rb
    r = max(ra, rb) + 1
    x0, x1 = int(max(0, min(a[0], b[0]) - r)), int(min(width, max(a[0], b[0]) + r + 1))
    y0, y1 = int(max(0, min(a[1], b[1]) - r)), int(min(height, max(a[1], b[1]) + r + 1))
    out = np.zeros(shape, dtype=bool)
    if x1 <= x0 or y1 <= y0:
        return out
    yy, xx = np.mgrid[y0:y1, x0:x1]
    px, py = xx + 0.5, yy + 0.5
    dx, dy = b[0] - a[0], b[1] - a[1]
    length2 = dx * dx + dy * dy or 1e-9
    t = np.clip(((px - a[0]) * dx + (py - a[1]) * dy) / length2, 0, 1)
    cx, cy = a[0] + t * dx, a[1] + t * dy
    rad = ra + (rb - ra) * t
    out[y0:y1, x0:x1] = (px - cx) ** 2 + (py - cy) ** 2 <= rad * rad
    return out


def bayer(shape):
    """4x4 ordered-dither thresholds in (0, 1) tiled over the given (height, width)."""
    m = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], dtype=float)
    h, w = shape
    return np.tile((m + 0.5) / 16.0, (h // 4 + 1, w // 4 + 1))[:h, :w]


def quantize(values, edges):
    """Maps values in [0, 1] to ramp indices using ascending thresholds."""
    index = np.zeros(values.shape, dtype=int)
    for edge in edges:
        index += (values >= edge).astype(int)
    return index


def to_image(rgb, alpha=None):
    height, width = rgb.shape[:2]
    out = np.zeros((height, width, 4), dtype=np.uint8)
    out[..., :3] = rgb
    out[..., 3] = 255 if alpha is None else alpha
    return Image.fromarray(out, "RGBA")


def shift(mask, dx, dy):
    out = np.zeros_like(mask)
    h, w = mask.shape
    ys, yd = (slice(max(0, -dy), h - max(0, dy)), slice(max(0, dy), h - max(0, -dy)))
    xs, xd = (slice(max(0, -dx), w - max(0, dx)), slice(max(0, dx), w - max(0, -dx)))
    out[yd, xd] = mask[ys, xs]
    return out


def dilate4(mask):
    return mask | shift(mask, 1, 0) | shift(mask, -1, 0) | shift(mask, 0, 1) | shift(mask, 0, -1)


def ellipse_mask(shape, cx, cy, rx, ry):
    height, width = shape
    yy, xx = np.mgrid[0:height, 0:width]
    return ((xx + 0.5 - cx) / rx) ** 2 + ((yy + 0.5 - cy) / ry) ** 2 <= 1.0
