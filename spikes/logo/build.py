import math, pathlib

OUT = pathlib.Path(__file__).parent
LIGHT = dict(bg="#FAF8F4", ink="#17241D", main="#1D6B4F", accent="#C2693E", on="#FAF8F4")
DARK = dict(bg="#17241D", ink="#F1EEE7", main="#6FCF9F", accent="#E08A5C", on="#17241D")


def seal_path(cx, cy, r, bumps, amp):
    pts = []
    for i in range(360):
        t = math.radians(i)
        rr = r + amp * math.cos(bumps * t) + 0.6 * math.sin(3 * t + 1)
        pts.append((cx + rr * math.cos(t), cy + rr * math.sin(t)))
    return "M" + " L".join(f"{x:.2f} {y:.2f}" for x, y in pts) + " Z"


def bracket(c, tile):
    bg = f'<rect width="64" height="64" rx="14" fill="{c["main"]}"/>'
    s = c["on"]
    return bg + (
        f'<g fill="none" stroke="{s}" stroke-linecap="round" stroke-linejoin="round">'
        f'<path d="M21 15 Q10 32 21 49" stroke-width="4.5"/>'
        f'<path d="M43 15 Q54 32 43 49" stroke-width="4.5"/>'
        f'<path d="M24.5 33 L30 38.5 L40 26.5" stroke-width="5.5"/></g>'
    )


def drop(c, tile):
    body = "M32 3 C32 3 53 25 53 39.5 A21 21 0 0 1 11 39.5 C11 25 32 3 32 3 Z"
    if tile:
        return (f'<rect width="64" height="64" rx="14" fill="{c["main"]}"/>'
                f'<g transform="translate(32 32) scale(0.72) translate(-32 -33)">'
                f'<path d="{body}" fill="{c["on"]}"/>'
                f'<path d="M22 40 L29 47 L42 32" fill="none" stroke="{c["main"]}" stroke-width="6" stroke-linecap="round" stroke-linejoin="round"/></g>')
    return (f'<path d="{body}" fill="{c["main"]}"/>'
            f'<path d="M22 40 L29 47 L42 32" fill="none" stroke="{c["on"]}" stroke-width="5.5" stroke-linecap="round" stroke-linejoin="round"/>')


def bonded(c, tile):
    stroke = c["on"] if tile else c["main"]
    bg = f'<rect width="64" height="64" rx="14" fill="{c["main"]}"/>' if tile else ""
    g = ('<g transform="translate(32 32) scale(0.78) translate(-32 -32)">' if tile else "<g>")
    return bg + g + (
        f'<path d="M7 28.5 L18 39.5" stroke="{stroke}" stroke-width="9" stroke-linecap="round"/>'
        f'<path d="M34.3 38.4 L54 15.5" stroke="{stroke}" stroke-width="9" stroke-linecap="round"/>'
        f'<circle cx="26.5" cy="47.5" r="6.5" fill="{c["accent"]}"/></g>'
    )


def blocks(c, tile):
    m = c["main"]
    sq = "".join(
        f'<rect x="{x}" y="{y}" width="27" height="27" rx="6" fill="{col}"/>'
        for (x, y), col in zip([(3, 3), (34, 3), (3, 34), (34, 34)], [m, m, m, c["accent"]])
    )
    return sq + (f'<path d="M14 33 L26 45 L50 17" fill="none" stroke="{c["bg"] if not tile else "#FAF8F4"}" '
                 f'stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/>')


def gring(c, tile):
    cx = cy = 32
    r = 21

    def p(deg):
        t = math.radians(deg)
        return cx + r * math.cos(t), cy + r * math.sin(t)

    sx, sy = p(-50)
    ex, ey = p(8)
    stroke = c["on"] if tile else c["main"]
    bg = f'<rect width="64" height="64" rx="14" fill="{c["main"]}"/>' if tile else ""
    g = ('<g transform="translate(32 32) scale(0.8) translate(-32 -32)">' if tile else "<g>")
    return bg + g + (
        f'<path d="M{sx:.2f} {sy:.2f} A{r} {r} 0 1 0 {ex:.2f} {ey:.2f} L36 {ey:.2f}" fill="none" stroke="{stroke}" stroke-width="7.5" stroke-linecap="round" stroke-linejoin="round"/>'
        f'<path d="M22 31 L29 38 L50 10" fill="none" stroke="{c["accent"]}" stroke-width="6.5" stroke-linecap="round" stroke-linejoin="round"/></g>'
    )


def seal(c, tile):
    body = c["main"]
    if tile:
        return (f'<rect width="64" height="64" rx="14" fill="{c["main"]}"/>'
                f'<path d="{seal_path(32, 32, 23, 9, 1.6)}" fill="{c["on"]}"/>'
                f'<circle cx="32" cy="32" r="16.5" fill="none" stroke="{c["main"]}" stroke-width="1.6"/>'
                f'<path d="M24.5 32.5 L30 38 L40 26" fill="none" stroke="{c["main"]}" stroke-width="4.5" stroke-linecap="round" stroke-linejoin="round"/>')
    return (f'<path d="{seal_path(32, 32, 28.5, 9, 2)}" fill="{body}"/>'
            f'<circle cx="32" cy="32" r="20.5" fill="none" stroke="{c["on"]}" stroke-opacity="0.55" stroke-width="1.8"/>'
            f'<path d="M22.5 32.5 L29.5 39.5 L42 24.5" fill="none" stroke="{c["on"]}" stroke-width="5.5" stroke-linecap="round" stroke-linejoin="round"/>')


CONCEPTS = [
    ("a-bracket-check", "A. Bracket check", bracket,
     "A check inside parentheses, read as Verify(…). Closest to the current docs-site mark."),
    ("b-glue-drop", "B. Glue drop", drop,
     "A drop of glue with a check cut out. Puts the Glue half of the name into the mark."),
    ("c-bonded-check", "C. Bonded check", bonded,
     "A check in two pieces held together by a terracotta joint. Separate packages that fit together."),
    ("d-four-blocks", "D. Four blocks", blocks,
     "Four tiles for the four packages, with one check across all of them."),
    ("e-g-ring", "E. G with a check", gring,
     "A G whose crossbar meets a check that leaves through the opening."),
    ("f-seal", "F. Seal", seal,
     "A pressed seal with a check. Reads as verified or approved."),
]

TILE_NATIVE = {"a-bracket-check", "d-four-blocks"}


def svg(inner, size=64):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="{size}" height="{size}">{inner}</svg>'


FONT = "-apple-system, BlinkMacSystemFont, 'SF Pro Display', 'Segoe UI', Inter, Helvetica, Arial, sans-serif"


def lockup(mark_inner, c):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 360 64" width="360" height="64">'
            f'<g transform="translate(0 6) scale(0.8125)">{mark_inner}</g>'
            f'<text x="66" y="42" font-family="{FONT}" font-size="30" font-weight="700" letter-spacing="-0.6" fill="{c["ink"]}">'
            f'Verification<tspan fill="{c["main"]}">Glue</tspan></text></svg>')


cards = []
for key, title, fn, blurb in CONCEPTS:
    d = OUT / key
    d.mkdir(exist_ok=True)
    mark_l, mark_d = fn(LIGHT, False), fn(DARK, False)
    icon = fn(LIGHT, True) if key not in TILE_NATIVE else mark_l
    (d / "mark.svg").write_text(svg(mark_l))
    (d / "mark-dark.svg").write_text(svg(mark_d))
    (d / "icon.svg").write_text(svg(icon))
    (d / "lockup.svg").write_text(lockup(mark_l, LIGHT))
    (d / "lockup-dark.svg").write_text(lockup(mark_d, DARK))
    cards.append(f'''
<section>
  <h2>{title}</h2><p>{blurb}</p>
  <div class="row">
    <div class="pane light">{svg(mark_l, 128)}</div>
    <div class="pane dark">{svg(mark_d, 128)}</div>
    <div class="pane light small">{svg(icon, 64)}{svg(icon, 32)}{svg(icon, 16)}</div>
  </div>
  <div class="lock light">{lockup(mark_l, LIGHT)}</div>
  <div class="lock dark">{lockup(mark_d, DARK)}</div>
</section>''')

html = f'''<!doctype html><html><head><meta charset="utf-8"><title>VerificationGlue logo concepts</title>
<style>
body{{margin:0;padding:40px;background:#EFECE5;font-family:{FONT};color:#17241D}}
h1{{font-size:28px;letter-spacing:-0.5px;margin:0 0 24px}}
.grid{{display:grid;grid-template-columns:repeat(3,1fr);gap:24px}}
section{{background:#fff;border-radius:14px;padding:20px}}
h2{{font-size:18px;margin:0 0 4px}} p{{font-size:13px;color:#55615A;margin:0 0 14px;min-height:34px}}
.row{{display:grid;grid-template-columns:1fr 1fr 1fr;gap:10px;margin-bottom:10px}}
.pane{{border-radius:10px;display:flex;align-items:center;justify-content:center;gap:10px;height:150px}}
.pane svg{{max-width:100%}}
.light{{background:#FAF8F4}} .dark{{background:#17241D}}
.lock{{border-radius:10px;padding:14px 16px;margin-top:8px}} .lock svg{{width:100%;height:auto;display:block}}
</style></head><body><h1>VerificationGlue logo concepts</h1><div class="grid">{"".join(cards)}</div></body></html>'''
(OUT / "index.html").write_text(html)
print("ok")
