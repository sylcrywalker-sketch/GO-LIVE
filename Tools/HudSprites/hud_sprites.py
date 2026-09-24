# Rasterizes the GO! LIVE HUD sprites (white, alpha = coverage) into PNGs. Pure Python: SDF strokes, 4x4 supersampling.
# Usage: python Tools/HudSprites/hud_sprites.py Assets/Game/UI/HUD/Sprites
# The PNGs keep their .meta files (Sprite import, 9-slice borders): regenerate over them, do not delete them.
# Icons are drawn on a 24-unit grid; the 9-slice pieces are authored at 2x (Image.pixelsPerUnitMultiplier 2, bar 4).
import math, os, struct, sys, zlib

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)


def write_png(path, size_x, size_y, alpha_rows):
    raw = bytearray()
    for row in alpha_rows:
        raw.append(0)
        for a in row:
            raw += bytes((255, 255, 255, a))
    def chunk(tag, body):
        return struct.pack('>I', len(body)) + tag + body + struct.pack('>I', zlib.crc32(tag + body) & 0xffffffff)
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', size_x, size_y, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b'')
    open(path, 'wb').write(png)


def d_segment(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    wx, wy = px - ax, py - ay
    t = max(0.0, min(1.0, (wx * vx + wy * vy) / (vx * vx + vy * vy + 1e-12)))
    dx, dy = wx - vx * t, wy - vy * t
    return math.hypot(dx, dy)


def d_arc(px, py, cx, cy, r, a0, a1):
    # y-down coordinates; angles in degrees, a0 < a1, measured from +x toward +y (clockwise on screen).
    ang = math.degrees(math.atan2(py - cy, px - cx))
    while ang < a0:
        ang += 360.0
    while ang > a0 + 360.0:
        ang -= 360.0
    if ang <= a1:
        return abs(math.hypot(px - cx, py - cy) - r)
    e0 = (cx + r * math.cos(math.radians(a0)), cy + r * math.sin(math.radians(a0)))
    e1 = (cx + r * math.cos(math.radians(a1)), cy + r * math.sin(math.radians(a1)))
    return min(math.hypot(px - e0[0], py - e0[1]), math.hypot(px - e1[0], py - e1[1]))


def sd_round_rect(px, py, x0, y0, x1, y1, r):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    hx, hy = (x1 - x0) / 2 - r, (y1 - y0) / 2 - r
    qx, qy = abs(px - cx) - hx, abs(py - cy) - hy
    outside = math.hypot(max(qx, 0.0), max(qy, 0.0))
    inside = min(max(qx, qy), 0.0)
    return outside + inside - r


def quad_points(p0, p1, p2, n=16):
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append(((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                    (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]))
    return pts


class Shape:
    """Union of stroked primitives (distance field in design units) plus filled discs."""

    def __init__(self, stroke):
        self.stroke = stroke
        self.items = []
        self.discs = []

    def line(self, ax, ay, bx, by):
        self.items.append(lambda x, y: d_segment(x, y, ax, ay, bx, by))
        return self

    def polyline(self, pts):
        for a, b in zip(pts, pts[1:]):
            self.line(a[0], a[1], b[0], b[1])
        return self

    def arc(self, cx, cy, r, a0, a1):
        self.items.append(lambda x, y: d_arc(x, y, cx, cy, r, a0, a1))
        return self

    def rrect(self, x0, y0, x1, y1, r):
        self.items.append(lambda x, y: abs(sd_round_rect(x, y, x0, y0, x1, y1, r)))
        return self

    def disc(self, cx, cy, r):
        self.discs.append((cx, cy, r))
        return self

    def coverage(self, x, y, unit):
        # unit = design units per output pixel; returns coverage of the pixel-sized sample at (x, y)
        d = min(f(x, y) for f in self.items) if self.items else 1e9
        a = max(0.0, min(1.0, (self.stroke / 2 - d) / unit + 0.5))
        for cx, cy, r in self.discs:
            a = max(a, max(0.0, min(1.0, (r - math.hypot(x - cx, y - cy)) / unit + 0.5)))
        return a


def render(shape, grid, size, name, samples=4):
    unit = grid / size
    rows = []
    for py in range(size):
        row = []
        for px in range(size):
            acc = 0.0
            for sy in range(samples):
                for sx in range(samples):
                    x = (px + (sx + 0.5) / samples) * unit
                    y = (py + (sy + 0.5) / samples) * unit
                    acc += shape.coverage(x, y, unit)
            row.append(int(round(255 * acc / (samples * samples))))
        rows.append(row)
    write_png(os.path.join(OUT, name), size, size, rows)
    print('wrote', name)


def render_sdf(sdf, width, height, name, samples=4):
    rows = []
    for py in range(height):
        row = []
        for px in range(width):
            acc = 0.0
            for sy in range(samples):
                for sx in range(samples):
                    x = px + (sx + 0.5) / samples
                    y = py + (sy + 0.5) / samples
                    acc += max(0.0, min(1.0, 0.5 - sdf(x, y)))
            row.append(int(round(255 * acc / (samples * samples))))
        rows.append(row)
    write_png(os.path.join(OUT, name), width, height, rows)
    print('wrote', name)


STROKE = 1.8

# Hunger: fork (three tines joined by a bowl, long handle) and a knife with a curved blade.
hunger = Shape(STROKE)
hunger.line(4.2, 2.6, 4.2, 8.6).line(7.2, 2.6, 7.2, 21.4).line(10.2, 2.6, 10.2, 8.6)
hunger.arc(7.2, 8.6, 3.0, 0, 180)
hunger.polyline(quad_points((18.6, 2.6), (14.4, 4.6), (14.6, 10.2)))
hunger.line(14.6, 10.2, 14.6, 12.6).line(14.6, 12.6, 18.6, 13.4).line(18.6, 2.6, 18.6, 21.4)
render(hunger, 24.0, 64, 'hud_icon_hunger.png')

# Concentration: a simple brain, two lobed hemispheres split by a centre line, one fold on each side.
brain = Shape(STROKE)
left_lobes = [(9.3, 6.9, 2.6, 185, 300), (6.4, 10.3, 2.7, 120, 262), (6.3, 14.7, 2.8, 92, 215), (9.6, 17.0, 2.5, 55, 170)]
for cx, cy, r, a0, a1 in left_lobes:
    brain.arc(cx, cy, r, a0, a1)
    brain.arc(24 - cx, cy, r, 180 - a1, 180 - a0)
brain.line(12, 4.5, 12, 19.6)
brain.arc(7.4, 12.2, 2.5, -55, 55)
brain.arc(16.6, 12.2, 2.5, 125, 235)
render(brain, 24.0, 64, 'hud_icon_concentration.png')

# Wallet: body, clasp pocket with a stud, and the edge of a card.
wallet = Shape(STROKE)
wallet.rrect(3.0, 6.8, 21.0, 19.6, 2.6)
wallet.rrect(14.6, 10.6, 21.0, 15.6, 1.6)
wallet.disc(17.4, 13.1, 1.15)
wallet.polyline([(6.0, 6.8), (15.8, 3.6), (17.2, 6.8)])
render(wallet, 24.0, 64, 'hud_icon_wallet.png')

# 9-slice pieces, authored at 2x (Image.pixelsPerUnitMultiplier = 2 on the HUD).
render_sdf(lambda x, y: sd_round_rect(x, y, 0, 0, 48, 48, 16), 48, 48, 'hud_panel.png')
render_sdf(lambda x, y: abs(sd_round_rect(x, y, 1.0, 1.0, 47.0, 47.0, 15)) - 1.0, 48, 48, 'hud_panel_outline.png')
render_sdf(lambda x, y: sd_round_rect(x, y, 0, 0, 32, 16, 8), 32, 16, 'hud_bar.png')
render_sdf(lambda x, y: abs(sd_round_rect(x, y, 1.0, 1.0, 39.0, 39.0, 9)) - 1.1, 40, 40, 'hud_keycap.png')
