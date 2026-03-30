"""
KMux app icon generator — v2.
Visually represents the app's core identity:
  - Split-pane terminal layout  (multiplexer)
  - Blue accent dividers        (KMux branding)
  - ">_" prompt + sparkle dot   (Claude Code integration)
Color palette: Catppuccin Mocha
"""

from PIL import Image, ImageDraw, ImageFont

# Catppuccin Mocha palette
C_BASE     = (0x1e, 0x1e, 0x2e, 255)  # main background
C_MANTLE   = (0x18, 0x18, 0x25, 255)  # pane background
C_SURFACE0 = (0x31, 0x32, 0x44, 255)  # active pane bg
C_SURFACE1 = (0x45, 0x47, 0x5a, 255)  # inactive text lines
C_OVERLAY0 = (0x6c, 0x70, 0x86, 255)  # dim text
C_TEXT     = (0xcd, 0xd6, 0xf4, 255)  # bright text
C_ACCENT   = (0x89, 0xb4, 0xfa, 255)  # blue accent
C_GREEN    = (0xa6, 0xe3, 0xa1, 255)  # green (Claude spark)
C_TRANSP   = (0, 0, 0, 0)


def aa(img, scale=4):
    """Simple anti-alias: draw at 4x then downscale."""
    return img.resize((img.width // scale, img.height // scale), Image.LANCZOS)


def draw_icon(size):
    """
    Layout (classic tmux split):
      ┌─────────────┬──────────┐
      │             │  pane B  │
      │   pane A    ├──────────┤
      │  (active)   │  pane C  │
      └─────────────┴──────────┘
    Pane A occupies ~58% of width.
    Pane B/C each occupy ~42% width, 50% height each.
    """
    S = size * 4  # work at 4× for AA
    img = Image.new("RGBA", (S, S), C_TRANSP)
    d = ImageDraw.Draw(img)

    # ── Background rounded rect ──────────────────────────────────────────────
    pad = S * 0.055
    r   = S * 0.17
    d.rounded_rectangle([pad, pad, S - pad, S - pad], radius=r, fill=C_BASE)

    # ── Pane geometry ────────────────────────────────────────────────────────
    inner_x0 = S * 0.10
    inner_y0 = S * 0.10
    inner_x1 = S * 0.90
    inner_y1 = S * 0.90
    div_w    = max(2, S * 0.025)   # divider thickness

    split_x  = inner_x0 + (inner_x1 - inner_x0) * 0.57   # vertical divider
    split_y  = inner_y0 + (inner_y1 - inner_y0) * 0.50   # horizontal divider (right side)

    # Pane A (left, active) — slightly lighter bg
    d.rounded_rectangle(
        [inner_x0, inner_y0, split_x - div_w / 2, inner_y1],
        radius=r * 0.55, fill=C_SURFACE0
    )
    # Pane B (top-right)
    d.rounded_rectangle(
        [split_x + div_w / 2, inner_y0, inner_x1, split_y - div_w / 2],
        radius=r * 0.45, fill=C_MANTLE
    )
    # Pane C (bottom-right)
    d.rounded_rectangle(
        [split_x + div_w / 2, split_y + div_w / 2, inner_x1, inner_y1],
        radius=r * 0.45, fill=C_MANTLE
    )

    # ── Divider lines (accent blue) ──────────────────────────────────────────
    # Vertical divider
    d.rectangle(
        [split_x - div_w / 2, inner_y0, split_x + div_w / 2, inner_y1],
        fill=C_ACCENT
    )
    # Horizontal divider (right side only)
    d.rectangle(
        [split_x + div_w / 2, split_y - div_w / 2, inner_x1, split_y + div_w / 2],
        fill=C_ACCENT
    )

    # ── Pane A content: terminal prompt lines ────────────────────────────────
    if size >= 48:
        font_sz = int(S * 0.072)
        try:
            font_mono = ImageFont.truetype("C:/Windows/Fonts/consolab.ttf", font_sz)
            font_mono_sm = ImageFont.truetype("C:/Windows/Fonts/consolab.ttf", int(S * 0.060))
        except OSError:
            try:
                font_mono = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", font_sz)
                font_mono_sm = font_mono
            except OSError:
                font_mono = font_mono_sm = ImageFont.load_default()

        a_cx = (inner_x0 + split_x - div_w / 2) / 2
        a_cy = (inner_y0 + inner_y1) / 2

        # Two dim "history" lines above
        for i, line in enumerate(["> git status", "> npm run dev"]):
            ly = a_cy - font_sz * 2.2 + i * font_sz * 1.4
            bb = d.textbbox((0, 0), line, font=font_mono_sm)
            lx = a_cx - (bb[2] - bb[0]) / 2
            d.text((lx, ly), line, font=font_mono_sm, fill=C_OVERLAY0)

        # Active prompt ">_" in accent color
        prompt = ">_"
        bb = d.textbbox((0, 0), prompt, font=font_mono)
        px = a_cx - (bb[2] - bb[0]) / 2
        py = a_cy + font_sz * 0.3
        d.text((px, py), prompt, font=font_mono, fill=C_ACCENT)

        # ── Claude spark (✦) in top-right corner of pane A ──────────────────
        spark_r = S * 0.038
        sx = split_x - div_w / 2 - spark_r * 2.2
        sy = inner_y0 + spark_r * 2.2
        _draw_sparkle(d, sx, sy, spark_r, C_GREEN)

        # ── Pane B / C: dim stub lines ───────────────────────────────────────
        _draw_stub_lines(d, split_x + div_w / 2, inner_y0,
                         inner_x1, split_y - div_w / 2,
                         font_mono_sm, C_OVERLAY0)
        _draw_stub_lines(d, split_x + div_w / 2, split_y + div_w / 2,
                         inner_x1, inner_y1,
                         font_mono_sm, C_SURFACE1)

    elif size >= 24:
        # Medium: just draw the active prompt ">" in pane A
        font_sz = int(S * 0.30)
        try:
            font = ImageFont.truetype("C:/Windows/Fonts/consolab.ttf", font_sz)
        except OSError:
            font = ImageFont.load_default()
        a_cx = (inner_x0 + split_x - div_w / 2) / 2
        a_cy = (inner_y0 + inner_y1) / 2
        bb = d.textbbox((0, 0), ">", font=font)
        d.text((a_cx - (bb[2] - bb[0]) / 2, a_cy - (bb[3] - bb[1]) / 2 - bb[1]),
               ">", font=font, fill=C_ACCENT)

    # Downscale for anti-aliasing
    return img.resize((size, size), Image.LANCZOS)


def _draw_sparkle(d, cx, cy, r, color):
    """Draw a 4-point star (✦) as a Claude spark indicator."""
    pts = []
    import math
    for i in range(8):
        angle = math.radians(i * 45)
        dist  = r if i % 2 == 0 else r * 0.35
        pts.append((cx + dist * math.cos(angle), cy + dist * math.sin(angle)))
    d.polygon(pts, fill=color)


def _draw_stub_lines(d, x0, y0, x1, y1, font, color):
    """Draw 2-3 short simulated text lines inside a pane."""
    pad_x = (x1 - x0) * 0.12
    pad_y = (y1 - y0) * 0.18
    cx    = (x0 + x1) / 2
    line_h = (y1 - y0 - 2 * pad_y) / 3.0

    for i, text in enumerate(["> ls", "  ..."]):
        bb = d.textbbox((0, 0), text, font=font)
        tw = bb[2] - bb[0]
        th = bb[3] - bb[1]
        lx = x0 + pad_x
        ly = y0 + pad_y + i * line_h
        if lx + tw < x1 - pad_x and ly + th < y1 - pad_y:
            d.text((lx, ly), text, font=font, fill=color)


def main():
    sizes   = [16, 24, 32, 48, 64, 128, 256]
    images  = [draw_icon(s) for s in sizes]

    ico_path = r"D:\github\kmux\src\KMux.App\Assets\kmux.ico"
    images[0].save(
        ico_path, format="ICO",
        sizes=[(s, s) for s in sizes],
        append_images=images[1:]
    )
    print(f"Saved: {ico_path}")

    png_path = r"D:\github\kmux\src\KMux.App\Assets\kmux_preview.png"
    images[-1].save(png_path)
    print(f"Preview: {png_path}")


if __name__ == "__main__":
    main()
