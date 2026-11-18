"""Builds the files in this folder from spikes/logo/b-glue-drop.

Usage: python3 build.py PATH/TO/InterDisplay-Bold.ttf
Needs: pip install uharfbuzz fonttools resvg-py
"""
import pathlib, re, shutil, sys

import resvg_py
import uharfbuzz as hb
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

here = pathlib.Path(__file__).resolve().parent
source = here.parent.parent / "spikes" / "logo" / "b-glue-drop"
font_path = sys.argv[1]
lockup_width = 286

for name in ("mark", "mark-dark", "icon"):
    shutil.copy(source / f"{name}.svg", here / f"{name}.svg")

blob = hb.Blob.from_file_path(font_path)
hb_font = hb.Font(hb.Face(blob))
tt = TTFont(font_path)
glyphs = tt.getGlyphSet()
order = tt.getGlyphOrder()


def short(d):
    return re.sub(r"\d+\.\d+", lambda n: f"{float(n.group()):.2f}".rstrip("0").rstrip("."), d)


def outline(svg):
    m = re.search(
        r'<text x="([\d.]+)" y="([\d.]+)"[^>]*font-size="([\d.]+)"[^>]*letter-spacing="([-\d.]+)" '
        r'fill="([^"]+)">(\w+)<tspan fill="([^"]+)">(\w+)</tspan></text>', svg)
    x, y, size, spacing = (float(m.group(i)) for i in (1, 2, 3, 4))
    ink, first, green, second = m.group(5), m.group(6), m.group(7), m.group(8)
    buf = hb.Buffer()
    buf.add_str(first + second)
    buf.guess_segment_properties()
    hb.shape(hb_font, buf, {"kern": True, "liga": False, "calt": False})
    scale = size / hb_font.face.upem
    paths = {ink: [], green: []}
    for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
        pen = SVGPathPen(glyphs)
        transform = (scale, 0, 0, -scale, x + pos.x_offset * scale, y - pos.y_offset * scale)
        glyphs[order[info.codepoint]].draw(TransformPen(pen, transform))
        if pen.getCommands():
            paths[ink if info.cluster < len(first) else green].append(pen.getCommands())
        x += pos.x_advance * scale + spacing
    group = "".join(f'<path d="{short(" ".join(d))}" fill="{color}"/>' for color, d in paths.items())
    svg = svg[:m.start()] + f"<g>{group}</g>" + svg[m.end():]
    return svg.replace('viewBox="0 0 360 64" width="360"', f'viewBox="0 0 {lockup_width} 64" width="{lockup_width}"')


for name in ("lockup", "lockup-dark"):
    (here / f"{name}.svg").write_text(outline((source / f"{name}.svg").read_text()))

for size in (128, 256, 512):
    png = resvg_py.svg_to_bytes(svg_path=str(here / "icon.svg"), width=size)
    (here / f"icon-{size}.png").write_bytes(bytes(png))

png = resvg_py.svg_to_bytes(svg_path=str(here / "lockup.svg"), width=lockup_width * 2)
(here / "lockup.png").write_bytes(bytes(png))
