"""Figma .fig (decoded canvas.json) -> PNG sprites (via SVG) + layout.json for the Unity importer."""
import json, struct, math, hashlib, os, sys, base64, shutil

SRC = sys.argv[1]          # canvas.json
IMGDIR = sys.argv[2]       # extracted images/ dir
OUT = sys.argv[3]          # output dir
K = 1.5                    # 960x720 design -> content scaled to 1080 px height
SCALE = K                  # sprite pixel density (px per Figma px) => sprites are native 1080p pixels
OUT_W, OUT_H = 1920, 1080  # Full HD menus
DESIGN_W = OUT_W / K       # 1280 design units: the 4:3 frame widened to 16:9

d = json.load(open(SRC))
nodes = d['nodeChanges']
blobs = [bytes.fromhex(b['bytes']['bytes']) if isinstance(b.get('bytes'), dict) else b'' for b in d['blobs']]

def gid(g): return f"{g['sessionID']}:{g['localID']}"
byid = {gid(n['guid']): n for n in nodes}
kids = {}
for n in nodes:
    p = n.get('parentIndex')
    if p: kids.setdefault(gid(p['guid']), []).append((p['position'], gid(n['guid'])))
for k in kids: kids[k] = [c for _, c in sorted(kids[k])]
def children(n): return [byid[c] for c in kids.get(gid(n['guid']), []) if byid[c].get('visible', True)]

# ---------- geometry ----------
def path_d(idx, sx=1.0, sy=1.0, ox=0.0, oy=0.0):
    b = blobs[idx]; i = 0; out = []
    def f():
        nonlocal i; v = struct.unpack_from('<f', b, i)[0]; i += 4; return v
    def pt(): x = f(); y = f(); return f"{ox + x*sx:.3f} {oy + y*sy:.3f}"
    while i < len(b):
        c = b[i]; i += 1
        if c == 0: out.append('Z')
        elif c == 1: out.append('M' + pt())
        elif c == 2: out.append('L' + pt())
        elif c == 3: out.append('Q' + pt() + ' ' + pt())
        elif c == 4: out.append('C' + pt() + ' ' + pt() + ' ' + pt())
        else: raise ValueError(f'bad cmd {c}')
    return ' '.join(out)

def mat(t): return [[t['m00'], t['m01'], t['m02']], [t['m10'], t['m11'], t['m12']], [0, 0, 1]]
def mul(a, b): return [[sum(a[i][k]*b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
def inv(m):
    a, b, c = m[0]; d_, e, f = m[1]; det = a*e - b*d_
    return [[e/det, -b/det, (b*f - c*e)/det], [-d_/det, a/det, (c*d_ - a*f)/det], [0, 0, 1]]
def svgm(m): return f"matrix({m[0][0]:.6f} {m[1][0]:.6f} {m[0][1]:.6f} {m[1][1]:.6f} {m[0][2]:.4f} {m[1][2]:.4f})"
def apply(m, x, y): return (m[0][0]*x + m[0][1]*y + m[0][2], m[1][0]*x + m[1][1]*y + m[1][2])

def rgba(c, op=1.0):
    return f"rgb({round(c['r']*255)},{round(c['g']*255)},{round(c['b']*255)})", c.get('a', 1)*op

_uid = [0]
def uid(p='g'):
    _uid[0] += 1; return f"{p}{_uid[0]}"

def corner_radii(n):
    return [n.get(k, n.get('cornerRadius', 0)) for k in ('rectangleTopLeftCornerRadius', 'rectangleTopRightCornerRadius', 'rectangleBottomRightCornerRadius', 'rectangleBottomLeftCornerRadius')]

def rrect_d(w, h, r):
    tl, tr, br, bl = [min(x, w/2, h/2) for x in r]
    return (f"M{tl} 0 H{w-tr} " + (f"A{tr} {tr} 0 0 1 {w} {tr} " if tr else "") + f"V{h-br} " +
            (f"A{br} {br} 0 0 1 {w-br} {h} " if br else "") + f"H{bl} " + (f"A{bl} {bl} 0 0 1 0 {h-bl} " if bl else "") +
            f"V{tl} " + (f"A{tl} {tl} 0 0 1 {tl} 0 " if tl else "") + "Z")

def fill_paths(n):
    """[(d, fillrule)] of the node's fill shape, in node-local coords."""
    if n.get('fillGeometry'):
        return [(path_d(g['commandsBlob']), 'evenodd' if g['windingRule'] == 'ODD' else 'nonzero') for g in n['fillGeometry']]
    w, h = n['size']['x'], n['size']['y']
    if n['type'] == 'ELLIPSE':
        return [(f"M0 {h/2} A{w/2} {h/2} 0 1 0 {w} {h/2} A{w/2} {h/2} 0 1 0 0 {h/2} Z", 'nonzero')]
    return [(rrect_d(w, h, corner_radii(n)), 'nonzero')]

def stroke_paths(n):
    return [(path_d(g['commandsBlob']), 'evenodd' if g['windingRule'] == 'ODD' else 'nonzero') for g in n.get('strokeGeometry', [])]

def glyph_paths(n):
    out = []
    for g in n.get('derivedTextData', {}).get('glyphs', []):
        if 'commandsBlob' not in g: continue
        s = g['fontSize']
        out.append((path_d(g['commandsBlob'], s, -s, g['position']['x'], g['position']['y']), 'nonzero'))
    return out

# ---------- paints ----------
def paint_ref(p, w, h, defs, white=False):
    """Returns (fill, fill-opacity) for an SVG element in node-local coords."""
    op = p.get('opacity', 1)
    if white: return 'white', 1
    t = p['type']
    if t == 'SOLID':
        c, a = rgba(p['color'], op); return c, a
    if t.startswith('GRADIENT'):
        gid_ = uid('gr')
        gm = mul([[w, 0, 0], [0, h, 0], [0, 0, 1]], inv(mat(p['transform'])))
        stops = ''.join(f'<stop offset="{s["position"]:.4f}" stop-color="{rgba(s["color"])[0]}" stop-opacity="{s["color"]["a"]*op:.4f}"/>' for s in p['stops'])
        if t == 'GRADIENT_LINEAR':
            defs.append(f'<linearGradient id="{gid_}" gradientUnits="userSpaceOnUse" x1="0" y1="0.5" x2="1" y2="0.5" gradientTransform="{svgm(gm)}">{stops}</linearGradient>')
        else:
            defs.append(f'<radialGradient id="{gid_}" gradientUnits="userSpaceOnUse" cx="0.5" cy="0.5" r="0.5" gradientTransform="{svgm(gm)}">{stops}</radialGradient>')
        return f'url(#{gid_})', 1
    if t == 'IMAGE':
        h_ = p['image']['hash']['bytes']; path = os.path.join(IMGDIR, h_)
        from PIL import Image as PI
        iw, ih = PI.open(path).size
        data = base64.b64encode(open(path, 'rb').read()).decode()
        pid = uid('pat')
        # FILL = cover
        s = max(w/iw, h/ih); dw, dh = iw*s, ih*s
        defs.append(f'<pattern id="{pid}" patternUnits="userSpaceOnUse" width="{w}" height="{h}"><image href="data:image/jpeg;base64,{data}" x="{(w-dw)/2}" y="{(h-dh)/2}" width="{dw}" height="{dh}" preserveAspectRatio="none" opacity="{op}"/></pattern>')
        return f'url(#{pid})', 1
    raise ValueError(t)

def visible(ps): return [p for p in (ps or []) if p.get('visible', True)]

# ---------- effects ----------
def effect_margin(n):
    m = 0
    for e in visible(n.get('effects')):
        r = e.get('radius', 0)
        if e['type'] == 'FOREGROUND_BLUR': m = max(m, r*1.6)
        elif e['type'] == 'DROP_SHADOW':
            m = max(m, r*1.6 + e.get('spread', 0) + max(abs(e['offset']['x']), abs(e['offset']['y'])))
    if n.get('strokeGeometry') and n.get('strokeAlign') != 'INSIDE': m = max(m, n.get('strokeWeight', 1))
    return m

def shape_svg(n, defs, ctx_bg=None, white=False, with_shadow=True):
    """SVG for node's own visuals (fills, strokes, effects), node-local coords. Text nodes contribute only their shadows."""
    w, h = n['size']['x'], n['size']['y']
    eff = visible(n.get('effects'))
    is_text = n['type'] == 'TEXT'
    geo = glyph_paths(n) if is_text else fill_paths(n)
    body = []
    if not is_text:
        for p in visible(n.get('fillPaints')):
            f, a = paint_ref(p, w, h, defs, white)
            for dd, rule in geo: body.append(f'<path d="{dd}" fill-rule="{rule}" fill="{f}" fill-opacity="{a:.4f}"/>')
        sp = stroke_paths(n)
        for p in visible(n.get('strokePaints')):
            f, a = paint_ref(p, w, h, defs, white)
            for dd, rule in sp: body.append(f'<path d="{dd}" fill-rule="{rule}" fill="{f}" fill-opacity="{a:.4f}"/>')
            if n.get('_synth_stroke'):  # geometry regenerated after resizing: inside-aligned stroke
                sw = n.get('strokeWeight', 1)
                dd = rrect_d(w - sw, h - sw, [max(0, r - sw/2) for r in corner_radii(n)])
                body.append(f'<path transform="translate({sw/2} {sw/2})" d="{dd}" fill="none" stroke="{f}" stroke-opacity="{a:.4f}" stroke-width="{sw}"/>')
    inner = ''.join(body)
    # inner shadow
    for e in eff:
        if e['type'] == 'INNER_SHADOW' and inner:
            fid = uid('is'); c, a = rgba(e['color'])
            defs.append(f'<filter id="{fid}" x="-50%" y="-50%" width="200%" height="200%"><feOffset in="SourceAlpha" dx="{e["offset"]["x"]}" dy="{e["offset"]["y"]}" result="o"/>'
                        f'<feGaussianBlur in="o" stdDeviation="{e["radius"]/2}" result="b"/><feComposite in="SourceAlpha" in2="b" operator="out" result="s"/>'
                        f'<feFlood flood-color="{c}" flood-opacity="{a}"/><feComposite in2="s" operator="in" result="sh"/>'
                        f'<feMerge><feMergeNode in="SourceGraphic"/><feMergeNode in="sh"/></feMerge></filter>')
            inner = f'<g filter="url(#{fid})">{inner}</g>'
    pre = ''
    if with_shadow:
        for e in eff:
            if e['type'] == 'DROP_SHADOW':
                fid = uid('ds'); c, a = rgba(e['color'])
                shp = ''.join(f'<path d="{dd}" fill-rule="{rule}" fill="black"/>' for dd, rule in geo)
                sp = e.get('spread', 0)
                morph = f'<feMorphology operator="dilate" radius="{sp}" in="SourceAlpha" result="m"/>' if sp else ''
                defs.append(f'<filter id="{fid}" x="-100%" y="-100%" width="300%" height="300%" filterUnits="objectBoundingBox">{morph}'
                            f'<feGaussianBlur in="{"m" if sp else "SourceAlpha"}" stdDeviation="{e["radius"]/2}" result="b"/><feOffset in="b" dx="{e["offset"]["x"]}" dy="{e["offset"]["y"]}" result="o"/>'
                            f'<feFlood flood-color="{c}" flood-opacity="{a}"/><feComposite in2="o" operator="in"/></filter>')
                pre += f'<g filter="url(#{fid})">{shp}</g>'
    if is_text: inner = ''
    out = pre + inner
    for e in eff:
        if e['type'] == 'FOREGROUND_BLUR' and out:
            fid = uid('fb')
            defs.append(f'<filter id="{fid}" x="-200%" y="-200%" width="500%" height="500%"><feGaussianBlur stdDeviation="{e["radius"]/2}"/></filter>')
            out = f'<g filter="url(#{fid})">{out}</g>'
    if ctx_bg is not None:
        for e in eff:
            if e['type'] == 'BACKGROUND_BLUR':
                fid = uid('bb'); cid = uid('cp')
                defs.append(f'<filter id="{fid}" x="-10%" y="-10%" width="120%" height="120%"><feGaussianBlur stdDeviation="{e["radius"]/2}"/></filter>')
                defs.append(f'<clipPath id="{cid}">' + ''.join(f'<path d="{dd}"/>' for dd, _ in geo) + '</clipPath>')
                out = f'<g clip-path="url(#{cid})"><g transform="{svgm(inv(ctx_bg[1]))}"><use href="#{ctx_bg[0]}" filter="url(#{fid})"/></g></g>' + out
    return out

def subtree_svg(n, defs, bgctx=None, abs_m=None):
    """Node + descendants, node-local coords. bgctx: (stack_id_fn) for background blur inside backdrops."""
    out = shape_svg(n, defs, bgctx if bgctx and abs_m is not None and bgctx[0] else None)
    kid = ''
    for c in children(n):
        km = mul(abs_m, mat(c['transform'])) if abs_m is not None else None
        kid += f'<g transform="{svgm(mat(c["transform"]))}">{subtree_svg(c, defs, (bgctx[0], km) if bgctx else None, km)}</g>'
    if kid and n['type'] == 'FRAME' and not n.get('frameMaskDisabled', False) and n['opacity'] >= 0:
        pass  # clipping of small frames ignored; root screens are clipped in Unity by RectMask2D
    out += kid
    if n['opacity'] < 1: out = f'<g opacity="{n["opacity"]:.4f}">{out}</g>'
    return out

# ---------- rasterize ----------
os.makedirs(f'{OUT}/svg', exist_ok=True); os.makedirs(f'{OUT}/Sprites', exist_ok=True)
jobs = {}
def emit(name_hint, body, defs, x0, y0, w, h, density=None):
    """Register an SVG covering [x0,y0,w,h] (local coords). Returns sprite file name (deduped)."""
    D = density or SCALE * 1.5
    W, H = max(1, math.ceil(w*D)), max(1, math.ceil(h*D))
    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{W}" height="{H}" viewBox="{x0} {y0} {W/D} {H/D}">'
           f'<defs>{"".join(defs)}</defs>{body}</svg>')
    key = hashlib.sha1(svg.encode()).hexdigest()[:10]
    safe = ''.join(ch if ch.isalnum() else '_' for ch in name_hint).strip('_')[:40]
    fn = jobs.get(key)
    if not fn:
        fn = f'{safe}_{key}.png'; jobs[key] = fn
        open(f'{OUT}/svg/{fn[:-4]}.svg', 'w').write(svg)
    return fn, W/D, H/D

def has_text(n): return n['type'] == 'TEXT' or any(has_text(c) for c in children(n))
def own_visual(n):
    return bool(visible(n.get('fillPaints')) or (visible(n.get('strokePaints')) and n.get('strokeGeometry')) or
                any(e['type'] in ('DROP_SHADOW', 'FOREGROUND_BLUR', 'INNER_SHADOW') for e in visible(n.get('effects'))))
def tintable(n):
    f = visible(n.get('fillPaints'))
    return (n['type'] != 'TEXT' and len(f) == 1 and f[0]['type'] == 'SOLID' and not visible(n.get('strokePaints'))
            and not visible(n.get('effects')) and not children(n))

KEEP_LIVE_PARENT_PREFIX = ('Setting ·',)   # toggles / volume bars stay as separate objects

def flattenable(n, parent):
    if n['type'] != 'FRAME' or not children(n): return False
    if has_text(n): return False
    p = parent
    while p is not None:
        if p['name'].startswith(KEEP_LIVE_PARENT_PREFIX) and n['name'] == 'Frame': return False
        p = byid.get(gid(p['parentIndex']['guid'])) if p.get('parentIndex') else None
    return True

def color_list(c, op): return [round(c['r'], 4), round(c['g'], 4), round(c['b'], 4), round(c.get('a', 1)*op, 4)]

def build(n, parent):
    w, h = n['size']['x'], n['size']['y']
    t = n['transform']
    assert abs(t['m01']) < 1e-6 and abs(t['m10']) < 1e-6 or not children(n), n['name']
    node = {'name': n['name'], 'x': t['m02'], 'y': t['m12'], 'w': w, 'h': h}
    rotated = abs(t['m01']) > 1e-6 or abs(t['m10']) > 1e-6
    if n['type'] == 'TEXT':
        node['kind'] = 'text'
        fp = visible(n.get('fillPaints'))[0]
        dt = n['derivedTextData']
        ls = n.get('letterSpacing', {'value': 0, 'units': 'PERCENT'})
        spacing = ls['value'] / n['fontSize'] * 100 if ls['units'] == 'PIXELS' else ls['value']
        txt = {'chars': n['textData']['characters'], 'family': n['fontName']['family'], 'style': n['fontName']['style'],
               'size': n['fontSize'], 'spacing': round(spacing, 3), 'align': n.get('textAlignHorizontal', 'LEFT'),
               'baseline': dt['baselines'][0]['position']['y'], 'lines': len(dt['baselines']),
               'autoWidth': n.get('textAutoResize') == 'WIDTH_AND_HEIGHT', 'opacity': n['opacity']}
        if fp['type'] == 'SOLID':
            txt['color'] = color_list(fp['color'], fp.get('opacity', 1))
        else:
            # horizontal gradient across the text box (all gradients in this file are horizontal)
            m = inv(mat(fp['transform'])); a = apply(m, 0, .5); b = apply(m, 1, .5)
            txt['color'] = [1, 1, 1, 1]
            txt['gradient'] = {'from': a[0], 'to': b[0], 'stops': [[s['position']] + color_list(s['color'], fp.get('opacity', 1)) for s in fp['stops']]}
        node['text'] = txt
        if any(e['type'] == 'DROP_SHADOW' for e in visible(n.get('effects'))):
            defs = []; m = effect_margin(n)
            body = shape_svg(n, defs)
            fn, vw, vh = emit(n['name'] + '_glow', body, defs, -m, -m, w + 2*m, h + 2*m)
            node['glow'] = {'sprite': fn, 'x': -m, 'y': -m, 'w': vw, 'h': vh}
        return node

    if not children(n) and not own_visual(n): return None
    flatten = rotated or not children(n) or flattenable(n, parent)
    if flatten:
        defs = []
        if rotated:
            # render in parent space, emit axis-aligned box
            pm = mat(t); m = effect_margin(n) + 1
            pts = [apply(pm, x, y) for x, y in ((0, 0), (w, 0), (0, h), (w, h))]
            x0, y0 = min(p[0] for p in pts) - m, min(p[1] for p in pts) - m
            x1, y1 = max(p[0] for p in pts) + m, max(p[1] for p in pts) + m
            body = f'<g transform="{svgm(pm)}">{subtree_svg(n, defs)}</g>'
            fn, vw, vh = emit(n['name'], body, defs, x0, y0, x1 - x0, y1 - y0)
            node.update({'kind': 'image', 'x': x0, 'y': y0, 'w': vw, 'h': vh, 'sprite': fn})
            return node
        m = math.ceil(max([effect_margin(n)] + [effect_margin(c) for c in children(n)]))
        tint = tintable(n)
        if tint:
            body = shape_svg(n, defs, white=True)
            fp = visible(n['fillPaints'])[0]
            node['color'] = color_list(fp['color'], fp.get('opacity', 1) * n['opacity'])
        else:
            body = subtree_svg(n, defs)
        fn, vw, vh = emit(n['name'], body, defs, -m, -m, w + 2*m, h + 2*m)
        node.update({'kind': 'image', 'sprite': fn, 'x': t['m02'] - m, 'y': t['m12'] - m, 'w': vw, 'h': vh,
                     'shape': {'x': m, 'y': m, 'w': w, 'h': h}})
        return node

    node['kind'] = 'frame'
    if n['opacity'] < 1: node['opacity'] = n['opacity']
    if own_visual(n):
        defs = []; m = math.ceil(effect_margin(n))
        fl = visible(n.get('fillPaints'))
        if len(fl) == 1 and fl[0]['type'] == 'SOLID' and not visible(n.get('strokePaints')) and not visible(n.get('effects')):
            body = shape_svg(n, defs, white=True); fp = visible(n['fillPaints'])[0]
            col = color_list(fp['color'], fp.get('opacity', 1))
        else:
            body = shape_svg(n, defs); col = None
        fn, vw, vh = emit(n['name'] + '_bg', body, defs, -m, -m, w + 2*m, h + 2*m)
        node['visual'] = {'sprite': fn, 'x': -m, 'y': -m, 'w': vw, 'h': vh}
        if col: node['visual']['color'] = col
    node['children'] = [x for x in (build(c, n) for c in children(n)) if x]
    return node

CONTENT = ('Title and menu', 'Settings content', 'Pause content')
screens = []
page = [n for n in nodes if n['type'] == 'CANVAS' and n['name'] == 'Page 1'][0]
for fr in children(page):
    w, h = fr['size']['x'], fr['size']['y']
    # backdrop: frame fill + every non-content child, with background blur emulated against layers beneath
    defs = []; layers = []; stack_ids = []
    import copy
    def widen(c):
        c = copy.deepcopy(c); t = c['transform']; cw = c['size']['x']
        if cw >= 0.9 * w:   # full-width layers / the framed panel: keep margins, stretch
            c['size']['x'] = cw + (DESIGN_W - w)
            if c.get('strokeGeometry'): c['_synth_stroke'] = True
            c.pop('fillGeometry', None); c.pop('strokeGeometry', None)
        else:               # sparkles, glows, accents: keep size, spread proportionally
            t['m02'] = (t['m02'] + cw / 2) * DESIGN_W / w - cw / 2
        return c
    frw = copy.deepcopy(fr); frw['size']['x'] = DESIGN_W; frw.pop('fillGeometry', None)
    base = shape_svg(frw, defs)
    lid = uid('L'); layers.append(f'<g id="{lid}">{base}</g>'); stack_ids.append(lid)
    live = None
    for c in children(fr):
        if c['name'] in CONTENT: live = c; continue
        c = widen(c)
        cm = mat(c['transform'])
        prev = uid('S'); defs.append(f'<g id="{prev}">' + ''.join(f'<use href="#{s}"/>' for s in stack_ids) + '</g>')
        inner = subtree_svg(c, defs, (prev, cm), cm)
        lid = uid('L'); layers.append(f'<g id="{lid}" transform="{svgm(cm)}">{inner}</g>'); stack_ids.append(lid)
    # defs must contain the layers referenced by <use>: put layers in defs, draw by use
    body = ''.join(f'<use href="#{s}"/>' for s in stack_ids)
    cid = uid('clip'); defs.append(f'<clipPath id="{cid}"><rect width="{DESIGN_W}" height="{h}"/></clipPath>')
    defs = layers + defs
    fn, _, _ = emit(fr['name'] + '_backdrop', f'<g clip-path="url(#{cid})">{body}</g>', defs, 0, 0, DESIGN_W, h, density=K)
    root = build(live, fr)
    key = fr['name'].split('·')[-1].strip().replace(' ', '')
    screens.append({'name': fr['name'], 'key': key, 'w': w, 'h': h, 'backdrop': fn, 'content': root})

json.dump({'source': 'UI (Copy).fig', 'scale': SCALE, 'screens': screens}, open(f'{OUT}/layout.json', 'w'), indent=1, ensure_ascii=False)
print(len(jobs), 'sprites')

# ---------- flat layout for Unity's JsonUtility ----------
def flat(screen):
    out = []
    def blank():
        return {'id': 0, 'parent': -1, 'name': '', 'kind': '', 'x': 0, 'y': 0, 'w': 0, 'h': 0, 'opacity': 1,
                'sprite': '', 'hasColor': False, 'color': [1, 1, 1, 1],
                'visualSprite': '', 'visualX': 0, 'visualY': 0, 'visualW': 0, 'visualH': 0, 'visualHasColor': False, 'visualColor': [1, 1, 1, 1],
                'glowSprite': '', 'glowX': 0, 'glowY': 0, 'glowW': 0, 'glowH': 0,
                'shapeX': 0, 'shapeY': 0, 'shapeW': 0, 'shapeH': 0,
                'chars': '', 'family': '', 'style': '', 'fontSize': 0, 'spacing': 0, 'align': '', 'baseline': 0, 'lines': 1,
                'autoWidth': False, 'textColor': [1, 1, 1, 1], 'hasGradient': False, 'gradFrom': 0, 'gradTo': 1, 'gradStops': []}
    def walk(n, parent):
        e = blank(); e['id'] = len(out); e['parent'] = parent; out.append(e)
        e.update({k: n[k] for k in ('name', 'kind', 'x', 'y', 'w', 'h')})
        e['opacity'] = n.get('opacity', 1)
        if n['kind'] == 'image':
            e['sprite'] = n['sprite']
            if 'color' in n: e['hasColor'] = True; e['color'] = n['color']
            sh = n.get('shape', {'x': 0, 'y': 0, 'w': n['w'], 'h': n['h']})
            e.update({'shapeX': sh['x'], 'shapeY': sh['y'], 'shapeW': sh['w'], 'shapeH': sh['h']})
        if 'visual' in n:
            v = n['visual']; e.update({'visualSprite': v['sprite'], 'visualX': v['x'], 'visualY': v['y'], 'visualW': v['w'], 'visualH': v['h']})
            if 'color' in v: e['visualHasColor'] = True; e['visualColor'] = v['color']
        if 'glow' in n:
            g = n['glow']; e.update({'glowSprite': g['sprite'], 'glowX': g['x'], 'glowY': g['y'], 'glowW': g['w'], 'glowH': g['h']})
        if n['kind'] == 'text':
            t = n['text']
            e.update({'chars': t['chars'], 'family': t['family'], 'style': t['style'], 'fontSize': t['size'], 'spacing': t['spacing'],
                      'align': t['align'], 'baseline': t['baseline'], 'lines': t['lines'], 'autoWidth': t['autoWidth'],
                      'textColor': t['color'][:3] + [t['color'][3] * t['opacity']]})
            if 'gradient' in t:
                g = t['gradient']; e['hasGradient'] = True; e['gradFrom'] = g['from']; e['gradTo'] = g['to']
                e['gradStops'] = [{'t': s[0], 'r': s[1], 'g': s[2], 'b': s[3], 'a': s[4] * t['opacity']} for s in g['stops']]
        for c in n.get('children', []): walk(c, e['id'])
    walk(screen['content'], -1)
    out[0]['x'] += (DESIGN_W - screen['w']) / 2   # centre the content in the widened frame
    for e in out:
        for k in ('x', 'y', 'w', 'h', 'visualX', 'visualY', 'visualW', 'visualH', 'glowX', 'glowY', 'glowW', 'glowH', 'fontSize', 'baseline', 'shapeX', 'shapeY', 'shapeW', 'shapeH'):
            e[k] = round(e[k] * K, 3)
    relayout(screen['key'], out)
    return out

SCALED = ('w', 'h', 'visualX', 'visualY', 'visualW', 'visualH', 'glowX', 'glowY', 'glowW', 'glowH', 'fontSize', 'baseline', 'shapeX', 'shapeY', 'shapeW', 'shapeH')

def scale_subtree(out, nid, s):
    def kids(i): return [e for e in out if e['parent'] == i]
    def scale_node(e):
        for k in SCALED: e[k] *= s
    scale_node(out[nid])
    stack = kids(nid)
    while stack:
        d = stack.pop()
        d['x'] *= s; d['y'] *= s
        scale_node(d)
        stack.extend(kids(d['id']))

CONTENT_FRAC = {'Café emblem': (0.2375, 0.039, 0.525, 0.578)}

def visual_box(e):
    if e['name'] in CONTENT_FRAC and e['shapeH']:
        fx, fy, fw, fh = CONTENT_FRAC[e['name']]
        return (e['shapeX'] + fx * e['shapeW'], e['shapeY'] + fy * e['shapeH'], fw * e['shapeW'], fh * e['shapeH'])
    if e['kind'] == 'image' and e['shapeH']:
        return e['shapeX'], e['shapeY'], e['shapeW'], e['shapeH']
    return 0, 0, e['w'], e['h']

DIVIDER_Y = 412
ICON_TOP, ICON_H = 92, 128
CAP_GAP, CAP_FS = 26, 19
TITLE_FS, BASE_Y = 108, 380
BUTTONS_TOP = 540
OUT_SIZE = (1920, 1080)

def node(out, name, parent=None):
    return next(c for c in out if c['name'] == name and (parent is None or c['parent'] == parent))

def abs_xy(out, e):
    x = y = 0
    while e is not None:
        x += e['x']; y += e['y']
        e = out[e['parent']] if e['parent'] >= 0 else None
    return x, y

def place(out, e, ax, ay):
    px, py = abs_xy(out, out[e['parent']]) if e['parent'] >= 0 else (0, 0)
    e['x'] = ax - px; e['y'] = ay - py

def center_x(out, e, ay):
    vx, vy, vw, vh = visual_box(e)
    place(out, e, OUT_SIZE[0] / 2 - vw / 2 - vx, ay - vy)

def icon_and_caption(out, icon, caption):
    scale_subtree(out, icon['id'], ICON_H / visual_box(icon)[3])
    center_x(out, icon, ICON_TOP)
    scale_subtree(out, caption['id'], CAP_FS / caption['fontSize'])
    center_x(out, caption, ICON_TOP + ICON_H + CAP_GAP)

def title_on_baseline(out, frame, text):
    scale_subtree(out, frame['id'], TITLE_FS / text['fontSize'])
    tx, ty = abs_xy(out, text)
    fx, fy = abs_xy(out, frame)
    center_x(out, frame, fy + (BASE_Y - (ty + text['baseline'])))

def stack_below(out, items, buttons):
    y = DIVIDER_Y
    for e, s, gap in items:
        if s != 1: scale_subtree(out, e['id'], s)
        center_x(out, e, y)
        y += visual_box(e)[3] + gap
    assert y + 24 <= BUTTONS_TOP, ('text below divider runs into buttons', y)
    center_x(out, buttons, BUTTONS_TOP)
    return BUTTONS_TOP + visual_box(buttons)[3]

def scale_button_labels(out, prefix, s):
    for btn in [x for x in out if x['name'].startswith(prefix)]:
        grp = next(c for c in out if c['parent'] == btn['id'] and c['name'] == 'Frame')
        scale_subtree(out, grp['id'], s)
        grp['x'] = (btn['w'] - grp['w']) / 2
        grp['y'] = (btn['h'] - grp['h']) / 2

def relayout(key, out):
    if key == 'Mainmenu':
        scale_button_labels(out, 'Menu button', 1.35)
        icon_and_caption(out, node(out, 'Café emblem', 0), node(out, 'Game category', 0))
        logo = node(out, 'Title logo', 0)
        title_on_baseline(out, logo, node(out, 'Title signature', logo['id']))
        bottom = stack_below(out, [(node(out, 'Title flourish', 0), 1.0, 10), (node(out, 'Subtitle', 0), 1.6, 0)],
                             node(out, 'Main menu', 0))
        alpha = {'Subtitle': 0.9, 'Game category': 1.0}
    elif key == 'Pause':
        scale_button_labels(out, 'Pause btn', 1.3)
        header = node(out, 'Pause header', 0)
        ring, eyebrow = node(out, 'Pause icon ring', header['id']), node(out, 'Pause eyebrow', header['id'])
        for e in (ring, eyebrow):
            ax, ay = abs_xy(out, e)
            e['parent'] = 0
            place(out, e, ax, ay)
        header['w'] = header['h'] = 0
        icon_and_caption(out, ring, eyebrow)
        title = node(out, 'Pause title', 0)
        title_on_baseline(out, title, title)
        bottom = stack_below(out, [(node(out, 'Flourish', 0), 1.0, 10), (node(out, 'Pause flavour', 0), 1.3, 0)],
                             node(out, 'Pause actions', 0))
        stats = node(out, 'Session info', 0)
        stats['visualSprite'] = ''
        center_x(out, stats, bottom + 12)
        assert bottom + 12 + visual_box(stats)[3] <= 1036
        alpha = {'Pause flavour': 0.85, 'Pause eyebrow': 1.0}
    else:
        return
    print(key, 'content bottom', round(bottom))
    assert bottom <= 1000, (key, bottom)
    for name, a in alpha.items():
        for e in out:
            if e['name'] == name and e['kind'] == 'text':
                e['textColor'][3] = a
    for e in out:
        for k in ('x', 'y') + SCALED: e[k] = round(e[k], 3)

json.dump({'scale': 1.0, 'screens': [{'name': s['name'], 'key': s['key'], 'width': OUT_W, 'height': OUT_H, 'backdrop': s['backdrop'], 'nodes': flat(s)} for s in screens]},
          open(f'{OUT}/hexhearth_layout.json', 'w'), indent=1, ensure_ascii=False)
