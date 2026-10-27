#!/usr/bin/env python3
"""Generates the frontend scrapbook textures (deterministic, seeded; made in-house, no third-party art).

Usage: python3 tools/ui/make_ui_textures.py   (writes HarvestPanic/Assets/Art/UI/Textures/*.png)
Outputs:
  T_UI_PaperGrain.png  256x256 tileable fibre/speckle overlay (transparent; tinted by the card colour underneath)
  T_UI_Tape.png        256x72  masking-tape strip with torn zig-zag ends
  T_UI_FadeLeft.png    512x8   ink gradient, opaque left -> clear right (menu readability over the 3D farm)
  T_UI_FadeBottom.png  8x256   ink gradient, clear top -> opaque bottom (footer band)
  T_UI_Vignette.png    512x512 soft radial edge darkening
  T_UI_Underline.png   512x40  hand-drawn ink underline stroke
"""
import math, os, random
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "HarvestPanic", "Assets", "Art", "UI", "Textures")
INK = (43, 33, 24)
os.makedirs(OUT, exist_ok=True)


def save(img, name):
    img.save(os.path.join(OUT, name), optimize=True)
    print("wrote", name, img.size)


def tileable_blur(arr, radius):
    h, w = arr.shape
    big = np.tile(arr, (3, 3))
    im = Image.fromarray(np.clip(big * 255, 0, 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(radius))
    out = np.asarray(im, dtype=np.float32)[h:2 * h, w:2 * w] / 255.0
    return out


def paper_grain():
    rng = np.random.default_rng(1977)
    n = 256
    coarse = tileable_blur(rng.random((n, n), dtype=np.float32), 6)
    fine = tileable_blur(rng.random((n, n), dtype=np.float32), 0.8)
    a = (coarse - coarse.mean()) * 0.9 + (fine - fine.mean()) * 0.6
    a = np.clip(a * 0.55 + 0.03, 0, 0.12)
    # fibres: short wrapped strokes
    fib = Image.new("L", (n, n), 0)
    px = fib.load()
    r = random.Random(7)
    for _ in range(220):
        x, y = r.random() * n, r.random() * n
        ang = r.random() * math.pi
        ln = 6 + r.random() * 18
        for t in range(int(ln * 2)):
            xx = int(x + math.cos(ang) * t * 0.5) % n
            yy = int(y + math.sin(ang) * t * 0.5) % n
            px[xx, yy] = min(255, px[xx, yy] + 40)
    fa = np.asarray(fib, dtype=np.float32) / 255.0 * 0.10
    alpha = np.clip(a + fa, 0, 0.16)
    rgba = np.zeros((n, n, 4), dtype=np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = INK
    rgba[..., 3] = (alpha * 255).astype(np.uint8)
    save(Image.fromarray(rgba, "RGBA"), "T_UI_PaperGrain.png")


def tape():
    w, h = 256, 72
    rng = random.Random(11)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    teeth = 7
    left = [rng.uniform(2, 12) for _ in range(teeth + 1)]
    right = [rng.uniform(2, 12) for _ in range(teeth + 1)]
    streak = [rng.uniform(-10, 10) for _ in range(h)]
    for y in range(h):
        t = y / (h - 1) * teeth
        i = min(int(t), teeth - 1)
        f = t - i
        tri = 1 - abs(f - 0.5) * 2  # zig-zag
        lx = left[i] * (1 - f) + left[i + 1] * f + tri * 6
        rx = w - (right[i] * (1 - f) + right[i + 1] * f + tri * 6)
        for x in range(w):
            if x < lx or x > rx:
                continue
            edge = min(x - lx, rx - x)
            a = 0.86 if edge > 1.5 else 0.6
            base = (243, 228, 176)
            s = streak[y] * 0.5 + rng.uniform(-3, 3)
            col = tuple(int(max(0, min(255, c + s))) for c in base)
            px[x, y] = col + (int(a * 255),)
    save(img, "T_UI_Tape.png")


def fade_left():
    w, h = 512, 8
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = INK
    xs = np.linspace(0, 1, w)
    a = (1 - xs) ** 1.6 * 0.78
    rgba[..., 3] = (a * 255).astype(np.uint8)[None, :]
    save(Image.fromarray(rgba, "RGBA"), "T_UI_FadeLeft.png")


def fade_bottom():
    w, h = 8, 256
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = INK
    ys = np.linspace(0, 1, h)
    a = ys ** 1.8 * 0.72
    rgba[..., 3] = (a * 255).astype(np.uint8)[:, None]
    save(Image.fromarray(rgba, "RGBA"), "T_UI_FadeBottom.png")


def vignette():
    n = 512
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    d = np.sqrt(((xs - n / 2) / (n / 2)) ** 2 + ((ys - n / 2) / (n / 2)) ** 2)
    a = np.clip((d - 0.55) / 0.85, 0, 1) ** 1.5 * 0.65
    rgba = np.zeros((n, n, 4), dtype=np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = INK
    rgba[..., 3] = (a * 255).astype(np.uint8)
    save(Image.fromarray(rgba, "RGBA"), "T_UI_Vignette.png")


def underline():
    w, h = 512, 40
    scale = 4
    big = Image.new("L", (w * scale, h * scale), 0)
    px = big.load()
    rng = random.Random(3)
    for k in range(2):
        y0 = h * scale * (0.45 + 0.12 * k)
        amp = 3 * scale
        for x in range(int(w * scale * 0.02), int(w * scale * (0.98 - 0.08 * k))):
            t = x / (w * scale)
            y = y0 + math.sin(t * 7.0 + k) * amp - t * 6 * scale * (1 - 2 * k)
            thick = (5.5 - 2.0 * k) * scale * (0.65 + 0.35 * math.sin(t * math.pi))
            for dy in range(int(-thick), int(thick) + 1):
                yy = int(y + dy)
                if 0 <= yy < h * scale:
                    px[x, yy] = 255
    small = big.resize((w, h), Image.LANCZOS)
    rgba = Image.new("RGBA", (w, h), INK + (0,))
    rgba.putalpha(small)
    save(rgba, "T_UI_Underline.png")


if __name__ == "__main__":
    paper_grain(); tape(); fade_left(); fade_bottom(); vignette(); underline()
