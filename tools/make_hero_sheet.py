"""Generates the hero sprite sheets (assets/characters/hero_sheet*.png).

Layout matches the reference: one row per animation, 6 frames per row.
Rows: 0 idle, 1 walk, 2 run, 3 jump, 4 attack (reserved, left empty for later).
`hero_sheet_hold.png` has the near arm raised in a holding pose and `hero_anchors.json`
lists where the hand is in every frame, so the game can place a held item exactly.

Run:  python tools/make_hero_sheet.py [preview.png]
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

FRAME = 56
MARGIN_X, MARGIN_Y = 4, 8  # drawing coordinates are 0..48; the frame adds headroom around them
COLUMNS = 6
ROWS = ["idle", "walk", "run", "jump", "attack"]

YY, XX = np.mgrid[0:FRAME, 0:FRAME]
PX, PY = XX + 0.5 - MARGIN_X, YY + 0.5 - MARGIN_Y


def hexc(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


# Palettes: light, base, shade, deep (hue-shifted ramps rather than plain darkening).
SKIN = dict(light=hexc("#f7cfae"), base=hexc("#e8b08c"), shade=hexc("#c78468"), deep=hexc("#a2624f"))
HAIR = dict(light=hexc("#8a5a3c"), base=hexc("#5e3a2a"), shade=hexc("#3f2620"), deep=hexc("#2a1819"))
JACKET = dict(light=hexc("#7fa3a6"), base=hexc("#4f747b"), shade=hexc("#385760"), deep=hexc("#263e4a"))
JACKET_FAR = dict(light=hexc("#5f8488"), base=hexc("#3d5f66"), shade=hexc("#2d4952"), deep=hexc("#1f3340"))
PANTS = dict(light=hexc("#5d6590"), base=hexc("#3f4668"), shade=hexc("#2d3250"), deep=hexc("#1d2138"))
PANTS_FAR = dict(light=hexc("#4a5178"), base=hexc("#323857"), shade=hexc("#242842"), deep=hexc("#171a2c"))
BOOT = dict(light=hexc("#c0705a"), base=hexc("#8f4a3b"), shade=hexc("#64302b"), deep=hexc("#43201f"))
BOOT_FAR = dict(light=hexc("#9e5a49"), base=hexc("#753c31"), shade=hexc("#512826"), deep=hexc("#381b1b"))
PACK = dict(light=hexc("#b05468"), base=hexc("#7e2d41"), shade=hexc("#561e30"), deep=hexc("#3a1424"))
OUTLINE = hexc("#17111c")
EYE = hexc("#1c1420")
EYE_SHINE = hexc("#fff4e6")
TIE = hexc("#d9b15a")


def clamp01(v):
    return max(0.0, min(1.0, v))


def smooth(t):
    t = clamp01(t)
    return t * t * (3 - 2 * t)


def rot(vec, degrees):
    a = math.radians(degrees)
    c, s = math.cos(a), math.sin(a)
    return (vec[0] * c - vec[1] * s, vec[0] * s + vec[1] * c)


def add(a, b):
    return (a[0] + b[0], a[1] + b[1])


def scale(a, k):
    return (a[0] * k, a[1] * k)


# ---------- shapes ----------
def capsule(a, b, ra, rb=None):
    rb = ra if rb is None else rb
    dx, dy = b[0] - a[0], b[1] - a[1]
    length2 = dx * dx + dy * dy or 1e-9
    t = np.clip(((PX - a[0]) * dx + (PY - a[1]) * dy) / length2, 0, 1)
    cx, cy = a[0] + t * dx, a[1] + t * dy
    r = ra + (rb - ra) * t
    return (PX - cx) ** 2 + (PY - cy) ** 2 <= r * r


def ellipse(center, rx, ry, degrees=0.0):
    a = math.radians(degrees)
    c, s = math.cos(a), math.sin(a)
    x, y = PX - center[0], PY - center[1]
    u, v = x * c + y * s, -x * s + y * c
    return (u / rx) ** 2 + (v / ry) ** 2 <= 1.0


def polygon(points):
    image = Image.new("L", (FRAME, FRAME), 0)
    ImageDraw.Draw(image).polygon([(x - 0.5 + MARGIN_X, y - 0.5 + MARGIN_Y) for x, y in points], fill=255)
    return np.array(image) > 0


def shift(mask, dx, dy):
    out = np.zeros_like(mask)
    h, w = mask.shape
    ys, yd = (slice(max(0, -dy), h - max(0, dy)), slice(max(0, dy), h - max(0, -dy)))
    xs, xd = (slice(max(0, -dx), w - max(0, dx)), slice(max(0, dx), w - max(0, -dx)))
    out[yd, xd] = mask[ys, xs]
    return out


def dilate4(mask):
    return mask | shift(mask, 1, 0) | shift(mask, -1, 0) | shift(mask, 0, 1) | shift(mask, 0, -1)


# ---------- canvas ----------
class Canvas:
    def __init__(self):
        self.rgb = np.zeros((FRAME, FRAME, 3), dtype=np.float32)
        self.filled = np.zeros((FRAME, FRAME), dtype=bool)

    def paint(self, mask, ramp, separate=True, shade=None, light=None):
        """Light comes from the top-left; `shade`/`light` add form shading to larger shapes."""
        if separate:
            seam = dilate4(mask) & ~mask & self.filled
            self.rgb[seam] = self.rgb[seam] * 0.5 + np.array(OUTLINE, dtype=np.float32) * 0.5
        rim_light = mask & ~shift(mask, 1, 1)
        rim_shade = mask & ~shift(mask, -1, -1) & ~rim_light
        colors = np.empty((FRAME, FRAME, 3), dtype=np.float32)
        colors[:] = ramp["base"]
        if shade is not None:
            colors[shade] = ramp["shade"]
        if light is not None:
            colors[light] = ramp["light"]
        colors[rim_shade] = ramp["shade"]
        colors[rim_light] = ramp["light"]
        both = mask & shift(~mask, 1, 1) & shift(~mask, -1, -1)
        colors[both] = ramp["base"]
        self.rgb[mask] = colors[mask]
        self.filled |= mask

    def pixels(self, points, color):
        for x, y in points:
            ix, iy = int(math.floor(x + MARGIN_X)), int(math.floor(y + MARGIN_Y))
            if 0 <= ix < FRAME and 0 <= iy < FRAME and self.filled[iy, ix]:
                self.rgb[iy, ix] = color

    def mask_pixels(self, mask, color, only_filled=True):
        target = mask & self.filled if only_filled else mask
        self.rgb[target] = color

    def finish(self):
        outer = dilate4(self.filled) & ~self.filled
        out = np.zeros((FRAME, FRAME, 4), dtype=np.uint8)
        out[..., :3] = np.clip(self.rgb, 0, 255).astype(np.uint8)
        out[..., 3] = np.where(self.filled, 255, 0)
        for y, x in zip(*np.nonzero(outer)):
            neighbor = None
            for dx, dy in ((0, 1), (1, 0), (-1, 0), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < FRAME and 0 <= ny < FRAME and self.filled[ny, nx]:
                    neighbor = self.rgb[ny, nx]
                    break
            base = np.array(OUTLINE, dtype=np.float32)
            color = base * 0.72 + (neighbor * 0.28 if neighbor is not None else 0)
            out[y, x, :3] = np.clip(color * 0.9, 0, 255).astype(np.uint8)
            out[y, x, 3] = 255
        return Image.fromarray(out, "RGBA")


# ---------- skeleton ----------
THIGH, SHIN = 9.0, 9.0
UPPER_ARM, FOREARM = 6.0, 5.8
TORSO = 11.6


def two_bone(root, target, a, b, bend):
    """Returns the joint between two bones; `bend` (+1/-1) picks which way the joint folds."""
    dx, dy = target[0] - root[0], target[1] - root[1]
    dist = max(abs(a - b) + 0.05, min(a + b - 0.05, math.hypot(dx, dy)))
    base = math.atan2(dy, dx)
    cosine = (a * a + dist * dist - b * b) / (2 * a * dist)
    angle = math.acos(max(-1.0, min(1.0, cosine)))
    joint = (root[0] + a * math.cos(base + bend * angle), root[1] + a * math.sin(base + bend * angle))
    end = (root[0] + dist * math.cos(base), root[1] + dist * math.sin(base))
    return joint, end


def pose(**overrides):
    base = dict(
        pelvis=(24.0, 26.1), lean=3.0, head_tilt=0.0, head_bob=0.0,
        near_foot=(26.0, 43.6, 0.0), far_foot=(21.5, 43.6, 0.0),
        near_arm=(4.0, 8.0), far_arm=(-3.0, 8.0), pack_bounce=0.0, hair_sway=0.0,
        shoulder_lift=0.0,
    )
    base.update(overrides)
    return base


def ease(s):
    return s * s * (3 - 2 * s)


def leg_path(p, stride, stance, lift, toe_off, heel_strike):
    """Foot offset (x, lift, angle) for a leg at phase p in [0,1); phase 0 is heel strike."""
    p %= 1.0
    if p < stance:
        s = p / stance
        x = stride * 0.9 - stride * 1.9 * s
        angle = heel_strike * (1 - smooth(s * 2)) + toe_off * smooth((s - 0.55) * 2.2)
        return x, 0.0, angle
    s = (p - stance) / (1 - stance)
    x = -stride * 1.0 + stride * 1.9 * ease(s)
    height = lift * math.sin(math.pi * s ** 0.85)
    angle = toe_off * 0.9 * (1 - smooth(s * 1.4)) + heel_strike * smooth((s - 0.6) * 2.5)
    return x, height, angle


def idle_pose(t):
    breath = 0.5 - 0.5 * math.cos(2 * math.pi * t / 6)
    sway = math.sin(2 * math.pi * t / 6)
    return pose(
        pelvis=(24.0, 26.1 + 0.3 * breath), lean=2.5 + 0.8 * breath,
        head_bob=-0.45 * breath, near_foot=(26.4, 43.6, 0.0), far_foot=(21.4, 43.6, 0.0),
        near_arm=(3.0 + 1.6 * sway, 6.0 + 2.0 * breath), far_arm=(-3.5 - 1.2 * sway, 8.0),
        pack_bounce=0.4 * breath, hair_sway=1.0 * sway, shoulder_lift=-0.4 * breath,
    )


def walk_pose(t):
    p = t / 6
    stride = 6.6
    nx, nl, na = leg_path(p, stride, 0.5, 4.6, 24, -12)
    fx, fl, fa = leg_path(p + 0.5, stride, 0.5, 4.6, 24, -12)
    bob = 0.95 * math.cos(4 * math.pi * p)
    swing = math.cos(2 * math.pi * p)
    return pose(
        pelvis=(24.0, 26.5 + bob), lean=4.5, head_bob=0.15 * bob,
        near_foot=(24 + nx, 43.6 - nl, na), far_foot=(24 + fx, 43.6 - fl, fa),
        near_arm=(-24 * swing, 12 + 7 * max(0.0, -swing)), far_arm=(24 * swing, 12 + 7 * max(0.0, swing)),
        pack_bounce=0.6 * bob, hair_sway=-2.0 * swing, shoulder_lift=0.2 * bob,
    )


def run_pose(t):
    p = t / 6
    stride = 7.6
    nx, nl, na = leg_path(p, stride, 0.36, 10.0, 38, -14)
    fx, fl, fa = leg_path(p + 0.5, stride, 0.36, 10.0, 38, -14)
    bob = 1.7 * math.cos(4 * math.pi * (p - 0.19)) - 1.0
    swing = math.cos(2 * math.pi * p)
    return pose(
        pelvis=(24.0 + 1.0, 27.8 + bob), lean=14.0, head_tilt=-4.0, head_bob=0.3 * bob,
        near_foot=(24 + nx, 43.6 - nl, na), far_foot=(24 + fx, 43.6 - fl, fa),
        near_arm=(18 - 36 * swing, 62 + 14 * swing), far_arm=(18 + 36 * swing, 62 - 14 * swing),
        pack_bounce=1.2 * bob, hair_sway=-4.0 * swing - 2, shoulder_lift=0.0,
    )


JUMP_POSES = [
    # 0 crouch before take-off
    pose(pelvis=(23.2, 31.4), lean=20.0, head_tilt=-6.0, near_foot=(27.0, 43.6, -4), far_foot=(21.0, 43.6, 2),
         near_arm=(-34.0, 20.0), far_arm=(-40.0, 24.0), pack_bounce=1.2, hair_sway=-2.0),
    # 1 push-off, body stretched up on the toes
    pose(pelvis=(24.0, 25.2), lean=6.0, head_tilt=2.0, near_foot=(24.4, 42.4, 38), far_foot=(21.4, 42.0, 40),
         near_arm=(55.0, 22.0), far_arm=(40.0, 24.0), pack_bounce=-0.6, hair_sway=-3.0),
    # 2 rising, knees drawing up
    pose(pelvis=(24.0, 24.4), lean=8.0, head_tilt=0.0, near_foot=(27.5, 39.4, 18), far_foot=(21.0, 41.2, 28),
         near_arm=(100.0, 18.0), far_arm=(84.0, 22.0), pack_bounce=-0.8, hair_sway=-4.0),
    # 3 apex, tucked
    pose(pelvis=(24.0, 25.4), lean=14.0, head_tilt=-4.0, near_foot=(28.5, 37.6, 10), far_foot=(22.5, 38.2, 22),
         near_arm=(100.0, 30.0), far_arm=(86.0, 28.0), pack_bounce=0.0, hair_sway=-1.0),
    # 4 falling, legs reaching down
    pose(pelvis=(24.0, 25.6), lean=5.0, head_tilt=3.0, near_foot=(26.6, 42.6, 24), far_foot=(21.6, 41.2, 30),
         near_arm=(78.0, 16.0), far_arm=(62.0, 20.0), pack_bounce=1.2, hair_sway=3.5),
    # 5 landing, absorbing the impact
    pose(pelvis=(23.6, 30.4), lean=16.0, head_tilt=-4.0, near_foot=(27.0, 43.6, -2), far_foot=(21.0, 43.6, 0),
         near_arm=(-18.0, 30.0), far_arm=(-26.0, 34.0), pack_bounce=1.5, hair_sway=-1.0),
]


def pose_for(row, frame):
    if row == "idle":
        return idle_pose(frame)
    if row == "walk":
        return walk_pose(frame)
    if row == "run":
        return run_pose(frame)
    if row == "jump":
        return JUMP_POSES[frame]
    return None


# ---------- rendering ----------
def draw_leg(canvas, hip, foot, far):
    pants = PANTS_FAR if far else PANTS
    boots = BOOT_FAR if far else BOOT
    ax, ay, angle = foot
    ankle_target = (ax, ay)
    knee, ankle = two_bone(hip, ankle_target, THIGH, SHIN, -1)  # knees fold forwards, towards +x
    canvas.paint(capsule(hip, knee, 2.9, 2.35), pants)
    canvas.paint(capsule(knee, ankle, 2.3, 1.85), pants)
    direction = rot((1, 0), angle)
    shin = (ankle[0] - knee[0], ankle[1] - knee[1])
    length = math.hypot(*shin) or 1
    up = (-shin[0] / length, -shin[1] / length)
    # boot: a short shaft with a lighter cuff, then a foot with a rounded toe
    shaft_top = add(ankle, scale(up, 3.4))
    canvas.paint(capsule(shaft_top, ankle, 2.25, 2.0), boots)
    cuff = capsule(shaft_top, add(shaft_top, scale(up, -1.0)), 2.35, 2.2)
    canvas.mask_pixels(cuff, boots["light"])
    heel = add(ankle, add(scale(direction, -1.2), scale(rot((0, 1), angle), 1.8)))
    toe = add(ankle, add(scale(direction, 4.0), scale(rot((0, 1), angle), 1.8)))
    canvas.paint(capsule(heel, toe, 1.95, 1.6), boots)
    return knee, ankle


def draw_arm(canvas, shoulder, arm, far):
    ramp = JACKET_FAR if far else JACKET
    alpha, beta = arm
    upper_dir = (math.sin(math.radians(alpha)), math.cos(math.radians(alpha)))
    elbow = add(shoulder, scale(upper_dir, UPPER_ARM))
    fore_dir = (math.sin(math.radians(alpha + beta)), math.cos(math.radians(alpha + beta)))
    wrist = add(elbow, scale(fore_dir, FOREARM))
    canvas.paint(capsule(shoulder, elbow, 2.15, 1.95), ramp)
    canvas.paint(capsule(elbow, wrist, 1.95, 1.7), ramp)
    cuff = add(wrist, scale(fore_dir, -1.0))
    canvas.paint(capsule(cuff, wrist, 1.85, 1.85), dict(JACKET, base=JACKET["light"], shade=JACKET["base"]), separate=False)
    hand = add(wrist, scale(fore_dir, 1.9))
    canvas.paint(capsule(wrist, hand, 1.55, 1.45), SKIN if not far else dict(SKIN, base=SKIN["shade"], light=SKIN["base"]))
    return add(wrist, scale(fore_dir, 1.0))


HOLD_ARM = (76.0, 44.0)  # raised forward so a torch is carried in front of the face


def render(row, frame, hold=False):
    spec = pose_for(row, frame)
    canvas = Canvas()
    if spec is None:
        return canvas.finish(), None
    hand = None
    px, py = spec["pelvis"]
    lean = spec["lean"]
    u = (math.sin(math.radians(lean)), -math.cos(math.radians(lean)))        # up the torso
    back = (-math.cos(math.radians(lean)), -math.sin(math.radians(lean)))    # towards the back
    front = (-back[0], -back[1])
    pelvis = (px, py)
    shoulder = add(add(pelvis, scale(u, TORSO)), (0, spec["shoulder_lift"]))
    hip_near, hip_far = add(pelvis, (0.7, 0.6)), add(pelvis, (-0.7, 0.6))

    # far side first
    draw_arm(canvas, add(shoulder, scale(front, -0.6)), spec["far_arm"], far=True)
    draw_leg(canvas, hip_far, spec["far_foot"], far=True)
    draw_leg(canvas, hip_near, spec["near_foot"], far=False)

    # backpack, on the back and following the torso
    bounce = (0, spec["pack_bounce"])
    pack_c = add(add(add(pelvis, scale(u, 7.4)), scale(back, 5.1)), bounce)
    corners = []
    for ox, oy in ((-3.0, -5.2), (2.4, -5.0), (2.6, 5.0), (-2.8, 5.2)):
        corners.append(add(pack_c, add(scale(back, -ox), scale(u, -oy))))
    pack = polygon(corners)
    pack |= capsule(add(pack_c, scale(u, 4.6)), add(pack_c, scale(u, -4.6)), 2.7, 2.7) & (
        (PX - pack_c[0]) * back[0] + (PY - pack_c[1]) * back[1] > 0.3)
    canvas.paint(pack, PACK, light=shift(pack, 0, 0) & ellipse(add(pack_c, add(scale(back, 1.0), scale(u, 3.0))), 1.6, 1.4))
    flap = polygon([add(pack_c, add(scale(back, 3.0), scale(u, 4.8))), add(pack_c, add(scale(back, -2.2), scale(u, 4.8))),
                    add(pack_c, add(scale(back, -2.4), scale(u, 1.6))), add(pack_c, add(scale(back, 3.2), scale(u, 1.8)))])
    canvas.mask_pixels(flap & ~shift(flap, 0, 1), PACK["shade"])

    # torso (jacket) with a short hem and a collar
    hem = capsule(add(pelvis, scale(u, -0.4)), add(pelvis, scale(u, 1.6)), 4.0, 4.0)
    chest = capsule(add(pelvis, scale(u, 1.0)), add(pelvis, scale(u, TORSO - 0.6)), 3.9, 4.4)
    torso = hem | chest
    lower = capsule(add(pelvis, scale(u, -2.0)), add(pelvis, scale(u, 3.6)), 5.0, 5.0)
    canvas.paint(torso, JACKET, shade=torso & lower & ~shift(torso, 0, -1) | torso & shift(~torso, -1, 0),
                 light=torso & ellipse(add(add(pelvis, scale(u, 9.2)), scale(front, 1.4)), 1.9, 1.6))
    zipper = capsule(add(add(pelvis, scale(u, -0.2)), scale(front, 2.6)), add(add(pelvis, scale(u, 9.6)), scale(front, 2.7)), 0.5, 0.5)
    canvas.mask_pixels(zipper & torso, JACKET["deep"])
    collar = capsule(add(shoulder, scale(u, -0.6)), add(shoulder, scale(u, 1.0)), 3.3, 2.7)
    canvas.paint(collar, dict(JACKET, base=JACKET["light"], shade=JACKET["base"]), separate=True)

    # neck and head
    neck_base = add(shoulder, scale(u, 0.6))
    head_c = add(add(neck_base, scale(u, 5.0)), (0.9, spec["head_bob"]))
    tilt = spec["head_tilt"] + lean * 0.35

    def hp(lx, ly):
        return add(head_c, rot((lx, ly), tilt))

    canvas.paint(capsule(neck_base, hp(0.4, 1.8), 1.9, 1.8), dict(SKIN, base=SKIN["shade"], light=SKIN["base"]), separate=True)
    head = ellipse(hp(0, 0), 4.5, 4.8, tilt) | ellipse(hp(1.5, 2.5), 3.2, 2.3, tilt) | ellipse(hp(4.4, 0.9), 1.0, 1.1, tilt)
    canvas.paint(head, SKIN, separate=True,
                 shade=head & ellipse(hp(-1.6, 2.2), 3.0, 2.5, tilt) & ~ellipse(hp(0.6, -0.6), 3.4, 3.4, tilt))
    # face
    eye = hp(2.7, 0.3)
    canvas.pixels([(eye[0], eye[1]), (eye[0], eye[1] + 1.0)], EYE)
    brow = hp(2.6, -1.6)
    canvas.pixels([(brow[0], brow[1]), (brow[0] + 1, brow[1] - 0.4)], HAIR["shade"])
    canvas.pixels([hp(4.3, 1.7)], SKIN["shade"])
    canvas.pixels([hp(3.0, 3.1)], SKIN["deep"])
    canvas.pixels([hp(1.9, 1.5)], SKIN["light"])
    # hair: cap over the back and top, a swept fringe, a bun with a tie and a short tail
    sway = spec["hair_sway"]
    cap = ellipse(hp(-1.3, -1.1), 5.4, 5.5, tilt)
    dx, dy = PX - head_c[0], PY - head_c[1]
    ca, sa = math.cos(math.radians(tilt)), math.sin(math.radians(tilt))
    local_x, local_y = dx * ca + dy * sa, -dx * sa + dy * ca
    cut = None
    face = (local_x > -0.9) & (local_y > -2.7)
    hair = cap & ~face & (local_y < 2.4)
    hair |= capsule(hp(0.2, -3.7), hp(3.9, -2.5), 1.25, 0.85)
    hair |= capsule(hp(-0.4, -3.0), hp(-0.2, -0.4), 0.9, 0.8)
    tail = capsule(hp(-4.0, -3.6), hp(-5.5 - sway * 0.5, 1.8), 1.0, 0.7)
    bun_c = hp(-3.6 + sway * 0.2, -5.2)
    bun = ellipse(bun_c, 2.8, 2.6)
    hair |= tail
    canvas.paint(hair, HAIR, separate=True, light=hair & ellipse(hp(0.0, -3.7), 2.6, 1.0, tilt),
                 shade=hair & ellipse(hp(-3.6, 1.2), 2.2, 1.8, tilt))
    canvas.paint(bun, HAIR, separate=True, light=bun & ellipse(add(bun_c, (-0.6, -0.9)), 1.2, 0.9))
    tie = capsule(hp(-2.6, -3.2), hp(-1.4, -4.0), 0.8, 0.8) & (hair | bun)
    canvas.mask_pixels(tie, TIE)

    if hold:
        hand = draw_arm(canvas, add(shoulder, scale(front, 0.4)), HOLD_ARM, far=False)
    else:
        draw_arm(canvas, add(shoulder, scale(front, 0.4)), spec["near_arm"], far=False)
    anchor = None if hand is None else [round(hand[0] + MARGIN_X, 2), round(hand[1] + MARGIN_Y, 2)]
    return canvas.finish(), anchor


def build_sheet(hold):
    sheet = Image.new("RGBA", (FRAME * COLUMNS, FRAME * len(ROWS)), (0, 0, 0, 0))
    anchors = {}
    for r, row in enumerate(ROWS):
        anchors[row] = []
        for c in range(COLUMNS):
            image, anchor = render(row, c, hold)
            sheet.paste(image, (c * FRAME, r * FRAME))
            anchors[row].append(anchor)
    return sheet, anchors


def preview(sheet, path, zoom=5):
    labels = Image.new("RGBA", (70, sheet.height * zoom), (60, 72, 70, 255))
    big = sheet.resize((sheet.width * zoom, sheet.height * zoom), Image.NEAREST)
    canvas = Image.new("RGBA", (labels.width + big.width, big.height), (96, 112, 108, 255))
    canvas.paste(labels, (0, 0))
    canvas.alpha_composite(big, (labels.width, 0))
    draw = ImageDraw.Draw(canvas)
    for r, row in enumerate(ROWS):
        draw.text((6, r * FRAME * zoom + FRAME * zoom // 2), row.upper(), fill=(255, 255, 255, 255))
    canvas.convert("RGB").save(path)


if __name__ == "__main__":
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
    out_dir = os.path.join(root, "assets", "characters")
    os.makedirs(out_dir, exist_ok=True)
    full, _ = build_sheet(False)
    holding, anchors = build_sheet(True)
    full.save(os.path.join(out_dir, "hero_sheet.png"))
    holding.save(os.path.join(out_dir, "hero_sheet_hold.png"))
    with open(os.path.join(out_dir, "hero_anchors.json"), "w") as handle:
        json.dump({"frame": FRAME, "columns": COLUMNS, "rows": ROWS, "hand": anchors}, handle)
    if len(sys.argv) > 1:
        preview(full, sys.argv[1])
        preview(holding, sys.argv[1].replace(".png", "_hold.png"))
    print("wrote", out_dir)
