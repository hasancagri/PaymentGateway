#!/usr/bin/env python3
"""İLKE VII görsel katman (Miro) — PG tek Payment BC sürümü (ECommerceAgent build-miro-wall.py'den port).
EventStorming storm markdown'ını (docs/eventstorming/payment-pay-by-link.md) Miro Canvas Composer SVG'sine
çevirir. Tek-akış zaman çizgisi (adımlar sol→sağ) + tip-renk lejandı + hotspot paneli.

Kullanım: python3 scripts/build-miro-wall.py
Çıktı: miro-wall.svg (.gitignore). Board'a basma Claude Miro MCP'siyle (canvas_create_from_svg, 200k limit).
Gerçek-kaynak storm markdown'dır — süreç değişince script yeniden koşar, board yeniden basılır.
Yüzey EN (başlık/lejant/sticky adları, tr→en haritası), anlatılar TR."""
import re, html, os, json

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(REPO, "docs", "eventstorming", "payment-pay-by-link.md")
OUT = os.path.join(REPO, "miro-wall.svg")
TR_EN = json.load(open(os.path.join(REPO, "scripts", "miro-sticky-tr-en.json"), encoding="utf-8"))

TYPES = {
    "🟦": ("Command", "blue"), "🟧": ("Event", "orange"), "🟪": ("Policy", "violet"),
    "🟨": ("Aggregate", "yellow"), "🟩": ("Read model", "green"),
    "🩷": ("External system", "light_pink"), "🟥": ("Hotspot", "red"), "👤": ("Actor", "light_yellow"),
}
LEGEND_FILL = {"blue": "#c6dcff", "orange": "#f8d3af", "violet": "#dedaff", "yellow": "#fff6b6",
               "green": "#adf0c7", "light_pink": "#ffc6c6", "red": "#ff6464", "light_yellow": "#fffbed"}
EMOJI_RE = "|".join(TYPES.keys())
STICKY_RE = rf"({EMOJI_RE})(⭐)?\s*\*\*(.+?)\*\*"

SW, SH, SGAP, SLOT_MIN, STEP_GAP, PAD = 199, 228, 24, 380, 100, 48
FLOW_H, HS_H, GAP = 520, 640, 160
TITLE_H = 230

text = open(SRC, encoding="utf-8").read()

def esc(s): return html.escape(s, quote=True)
def strip_mark(s): return re.sub(r"_\(planned\)_|`|⭐|⇨|⇦", "", s).strip()

missing = set()
def sticky_en(name):
    plain = re.sub(r"\s+", " ", strip_mark(name)).strip()
    en = TR_EN.get(plain)
    if en is None:
        missing.add(plain); return plain
    return en

# --- Süreç adımları ---
steps = []
proc = re.search(r"## Süreç\n(.*?)(?=\n## |\Z)", text, re.S)
if proc:
    for raw in re.split(r"\n(?=\s{0,1}\d+\.\s)", proc.group(1)):
        raw = raw.strip()
        if not re.match(r"^\d+\.", raw): continue
        num = re.match(r"^(\d+)\.", raw).group(1)
        flat = re.sub(r"\s+", " ", raw)
        stickies = [(m.group(1), bool(m.group(2)), m.group(3)) for m in re.finditer(STICKY_RE, flat)]
        nm = re.search(r" — (.+)$", flat)
        nar = strip_mark(re.sub(rf"({EMOJI_RE})|\*\*", "", nm.group(1))) if nm else ""
        if stickies: steps.append(dict(num=num, stickies=stickies, nar=nar))

# --- Hotspot'lar ---
hotspots = []
hs = re.search(r"## Açık Hotspot'lar\n(.*?)(?=\n## |\Z)", text, re.S)
if hs:
    for line in re.split(r"\n(?=- )", hs.group(1).strip()):
        line = line.strip()
        if "🟥" not in line: continue
        b = re.search(r"\*\*(.+?)\*\*", line)
        if b:
            label = b.group(1)
        else:
            label = re.sub(r"^-\s*🟥\s*\((open|closed)[^)]*\)\s*", "", line)
            label = re.split(r" — |→", label)[0].strip().strip('"')
        hotspots.append(re.sub(r"\s+", " ", label)[:48])

# --- SVG ---
sticky_total = 0
flow = []
x = PAD
for step in steps:
    n = len(step["stickies"])
    slot = max(n * SW + (n - 1) * SGAP, SLOT_MIN)
    for i, (emoji, piv, name) in enumerate(step["stickies"]):
        _, color = TYPES[emoji]
        sx = x + i * (SW + SGAP)
        content = esc(sticky_en(name))
        if piv: content = f"&lt;b&gt;{content}&lt;/b&gt;"
        flow.append(f'<rect data-type="sticky" x="{sx}" y="{PAD}" width="{SW}" height="{SH}" data-color="{color}" data-content="{content}" />')
        sticky_total += 1
    nar = f'{step["num"]}. {step["nar"]}' if step["nar"] else f'Adım {step["num"]}'
    if len(nar) > 240: nar = nar[:237] + "..."
    flow.append(f'<textArea x="{x}" y="{PAD + SH + 16}" width="{slot}" font-size="14" fill="#595959">{esc(nar)}</textArea>')
    x += slot + STEP_GAP
flow_w = max(x - STEP_GAP + PAD, 900)

# Hotspot frame (kırmızı sticky satır(lar)ı, 6/satır sarma)
hs_svg = []
PERROW = 6
for idx, label in enumerate(hotspots):
    r, c = divmod(idx, PERROW)
    hx = PAD + c * (SW + SGAP)
    hy = PAD + r * (SH + SGAP)
    hs_svg.append(f'<rect data-type="sticky" x="{hx}" y="{hy}" width="{SW}" height="{SH}" data-color="red" data-content="{esc(label)}" />')
hs_rows = (len(hotspots) + PERROW - 1) // PERROW
hs_h = max(PAD * 2 + hs_rows * (SH + SGAP), 300)
hs_w = max(PAD * 2 + PERROW * (SW + SGAP), 900)

y_flow = TITLE_H
y_hs = y_flow + FLOW_H + GAP

head = [
    '<text x="0" y="70" font-size="67" font-weight="bold" fill="#1a1a1a" font-family="noto_sans">EventStorming Wall - PG Pay-by-Link</text>',
    f'<text x="0" y="120" font-size="22" fill="#595959" font-family="noto_sans">Payment BC - Act 1 - {len(steps)} steps - {sticky_total} stickies - {len(hotspots)} hotspots - source: docs/eventstorming/payment-pay-by-link.md (read-only, generated)</text>',
]
lx = 0
for emoji, (typ, color) in TYPES.items():
    head.append(f'<rect x="{lx}" y="160" width="170" height="44" rx="6" fill="{LEGEND_FILL[color]}" stroke="none" data-content="{esc(typ)}" data-text-color="#1a1a1a" data-font-size="14" />')
    lx += 190

flow_g = (f'<g transform="translate(0,{y_flow})" data-frame="Pay-by-Link Flow (Act 1)">\n'
          f'<rect data-type="frame" x="0" y="0" width="{flow_w}" height="{FLOW_H}" fill="#f7f7f7" />\n'
          + "\n".join(flow) + "\n</g>")
hs_g = (f'<g transform="translate(0,{y_hs})" data-frame="Open Hotspots">\n'
        f'<rect data-type="frame" x="0" y="0" width="{hs_w}" height="{hs_h}" fill="#fff0f0" />\n'
        + "\n".join(hs_svg) + "\n</g>")

full = "<svg>\n" + "\n".join(head) + "\n" + flow_g + "\n" + hs_g + "\n</svg>"
open(OUT, "w", encoding="utf-8").write(full)
for m in sorted(missing):
    print(f"UYARI: ceviri yok (TR kaldi): {m!r} -> scripts/miro-sticky-tr-en.json'a ekle")
print(f"OK {os.path.relpath(OUT, REPO)} - {len(steps)} adim, {sticky_total} sticky, {len(hotspots)} hotspot, {len(full)} karakter{' - UYARILI' if missing else ''}")
