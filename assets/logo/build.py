"""Builds the logo files in this folder.

Usage: python3 build.py [PATH/TO/InterDisplay-Bold.ttf]
Needs: pip install resvg-py
With a font path it also rebuilds wordmark.svg, which needs: pip install uharfbuzz fonttools
"""
import pathlib, re, shutil, sys

import resvg_py

here = pathlib.Path(__file__).resolve().parent
source = here.parent.parent / "spikes" / "logo" / "b-glue-drop"

DROP = "M32 3 C32 3 53 25 53 39.5 A21 21 0 0 1 11 39.5 C11 25 32 3 32 3 Z"
CHECK = "M22 40 L29 47 L42 32"

LIGHT = {"green": "#1D6B4F", "ink": "#17241D", "background": "#FAF8F4", "border": "#E4E0D6"}
DARK = {"green": "#6FCF9F", "ink": "#F1EEE7", "background": "#17241D", "border": "#2E3D35"}

# The wordmark is set at 30 px with its baseline at y 42 and its first glyph at x 66.
WORDMARK_X, WORDMARK_SIZE, WORDMARK_BASELINE, WORDMARK_SPACING = 66, 30, 42, -0.6

# The drop's round body is centred on the middle of the capitals, and the gap to the
# wordmark is about half the cap height.
MARK_SCALE, MARK_BODY_Y, MARK_GAP = 0.7, 31.5, 11
CARD_PAD_X, CARD_PAD_Y = 20, 12
CARD_PNG_SCALE = 1.25


def short(d):
    return re.sub(r"\d+\.\d+", lambda n: f"{float(n.group()):.2f}".rstrip("0").rstrip("."), d)


def outline_wordmark(font_path):
    import uharfbuzz as hb
    from fontTools.pens.boundsPen import BoundsPen
    from fontTools.pens.svgPathPen import SVGPathPen
    from fontTools.pens.transformPen import TransformPen
    from fontTools.ttLib import TTFont

    hb_font = hb.Font(hb.Face(hb.Blob.from_file_path(font_path)))
    tt = TTFont(font_path)
    glyphs, order = tt.getGlyphSet(), tt.getGlyphOrder()
    buf = hb.Buffer()
    buf.add_str("VerificationGlue")
    buf.guess_segment_properties()
    hb.shape(hb_font, buf, {"kern": True, "liga": False, "calt": False})
    scale = WORDMARK_SIZE / hb_font.face.upem
    x, y = WORDMARK_X, WORDMARK_BASELINE
    paths = {"ink": [], "green": []}
    bounds = BoundsPen(glyphs)
    for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
        transform = (scale, 0, 0, -scale, x + pos.x_offset * scale, y - pos.y_offset * scale)
        pen = SVGPathPen(glyphs)
        glyphs[order[info.codepoint]].draw(TransformPen(pen, transform))
        glyphs[order[info.codepoint]].draw(TransformPen(bounds, transform))
        if pen.getCommands():
            paths["ink" if info.cluster < len("Verification") else "green"].append(pen.getCommands())
        x += pos.x_advance * scale + WORDMARK_SPACING
    left, _, right, _ = bounds.bounds
    body = "".join(f'<path id="{k}" d="{short(" ".join(d))}" fill="{LIGHT[k]}"/>' for k, d in paths.items())
    (here / "wordmark.svg").write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{left:.2f} 0 {right - left:.2f} 64" '
        f'width="{right - left:.2f}" height="64">{body}</svg>\n')


def read_wordmark():
    svg = (here / "wordmark.svg").read_text()
    left, _, width, _ = (float(v) for v in re.search(r'viewBox="([^"]+)"', svg).group(1).split())
    d = dict(re.findall(r'<path id="(\w+)" d="([^"]+)"', svg))
    return left, left + width, d["ink"], d["green"]


def mark_group(colors, x, y, scale, stroke=5.5):
    return (f'<g transform="translate({x:.2f} {y:.2f}) scale({scale})">'
            f'<path d="{DROP}" fill="{colors["green"]}"/>'
            f'<path d="{CHECK}" fill="none" stroke="{colors["background"]}" stroke-width="{stroke}" '
            f'stroke-linecap="round" stroke-linejoin="round"/></g>')


def lockup(colors, card):
    left, right, ink, green = read_wordmark()
    mark_x = -11 * MARK_SCALE
    mark_y = MARK_BODY_Y - 39.5 * MARK_SCALE
    shift = 53 * MARK_SCALE + mark_x + MARK_GAP - left
    width = right + shift
    pad_x, pad_y = (CARD_PAD_X, CARD_PAD_Y) if card else (0, 0)
    w, h = width + 2 * pad_x, 64 + 2 * pad_y
    parts = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w:.2f} {h:.2f}" width="{w:.0f}" height="{h:.0f}">']
    if card:
        parts.append(f'<rect x="0.5" y="0.5" width="{w - 1:.2f}" height="{h - 1:.2f}" rx="12" '
                     f'fill="{colors["background"]}" stroke="{colors["border"]}"/>')
    parts.append(f'<g transform="translate({pad_x} {pad_y})">')
    parts.append(mark_group(colors, mark_x, mark_y, MARK_SCALE))
    parts.append(f'<g transform="translate({shift:.2f} 0)"><path d="{ink}" fill="{colors["ink"]}"/>'
                 f'<path d="{green}" fill="{colors["green"]}"/></g></g></svg>\n')
    return "".join(parts), w


def icon(drop_scale, stroke):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">'
            f'<rect width="64" height="64" rx="14" fill="{LIGHT["green"]}"/>'
            f'<g transform="translate(32 32) scale({drop_scale}) translate(-32 -33)">'
            f'<path d="{DROP}" fill="{LIGHT["background"]}"/>'
            f'<path d="{CHECK}" fill="none" stroke="{LIGHT["green"]}" stroke-width="{stroke}" '
            f'stroke-linecap="round" stroke-linejoin="round"/></g></svg>\n')


def png(svg_name, png_name, width):
    data = resvg_py.svg_to_bytes(svg_path=str(here / svg_name), width=round(width))
    (here / png_name).write_bytes(bytes(data))


if len(sys.argv) > 1:
    outline_wordmark(sys.argv[1])

for name in ("mark", "mark-dark"):
    shutil.copy(source / f"{name}.svg", here / f"{name}.svg")

(here / "icon.svg").write_text(icon(0.72, 6))
(here / "icon-small.svg").write_text(icon(0.86, 8))
for size in (16, 32, 48):
    png("icon-small.svg", f"icon-{size}.png", size)
for size in (128, 256, 512):
    png("icon.svg", f"icon-{size}.png", size)

for name, colors, card in (("lockup", LIGHT, False), ("lockup-dark", DARK, False),
                           ("lockup-card", LIGHT, True), ("lockup-card-dark", DARK, True)):
    svg, width = lockup(colors, card)
    (here / f"{name}.svg").write_text(svg)
    if card:
        png(f"{name}.svg", f"{name}.png", width * CARD_PNG_SCALE)
