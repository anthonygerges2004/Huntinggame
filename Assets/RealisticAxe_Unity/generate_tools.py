#!/usr/bin/env python3
"""Realistic tree-harvesting tools for Unity: felling axe + pickaxe.
Usage: python3 generate_tools.py [out_dir]
Outputs: FellingAxe.obj, Pickaxe.obj, Tools.mtl, Wood_albedo/normal.png,
         Steel_albedo/normal/MetalSmooth.png, preview.png
Orientation: handle along +Y, blade/pick pointing +Z, 1 unit = 1 m, pivot at the grip.
"""
import math, os, sys
from collections import defaultdict
import numpy as np
from PIL import Image, ImageDraw

OUT = sys.argv[1] if len(sys.argv) > 1 else "RealisticAxe"
os.makedirs(OUT, exist_ok=True)

# ------------------------------------------------------------------ textures
def tn(h, w, fx, fy, seed):
    r = np.random.default_rng(seed + 500)
    F = np.fft.fft2(r.standard_normal((h, w)).astype(np.float32))
    ky = (np.fft.fftfreq(h)[:, None] * h).astype(np.float32)
    kx = (np.fft.fftfreq(w)[None, :] * w).astype(np.float32)
    o = np.real(np.fft.ifft2(F * np.exp(-((kx / fx) ** 2 + (ky / fy) ** 2)))).astype(np.float32)
    return (o - o.mean()) / (o.std() + 1e-9)

def sm(a, lo, hi):
    t = np.clip((a - lo) / (hi - lo), 0, 1); return t * t * (3 - 2 * t)

def normal_map(hmap, k, path):
    gx = (np.roll(hmap, -1, 1) - np.roll(hmap, 1, 1)) * 0.5
    gy = (np.roll(hmap, 1, 0) - np.roll(hmap, -1, 0)) * 0.5
    nv = np.stack([-gx * k, -gy * k, np.ones_like(hmap)], -1)
    nv /= np.linalg.norm(nv, axis=-1, keepdims=True)
    Image.fromarray(((nv * 0.5 + 0.5) * 255).astype(np.uint8)).save(path)

def make_wood():
    h, w = 2048, 512
    fib = tn(h, w, 70, 9, 1); fib2 = tn(h, w, 170, 26, 2)
    warp = tn(h, w, 3, 5, 3) * 0.09 + tn(h, w, 2, 2, 4) * 0.12
    cols = np.arange(w)[None, :] / w
    rings = (np.sin(2 * np.pi * (cols * 6 + warp)) * 0.5 + 0.5) ** 1.6      # growth-ring lines
    light, dark = np.array([206, 168, 118.]), np.array([132, 90, 56.])
    t = np.clip(0.55 * rings + 0.14 * fib + 0.10 * fib2 + 0.1, 0, 1)[..., None]
    col = light + (dark - light) * t
    col *= (0.95 + 0.05 * fib2)[..., None] * (0.93 + 0.08 * tn(h, w, 2, 3, 6))[..., None]
    pores = sm(tn(h, w, 280, 45, 5), 1.3, 2.2)
    col *= (1 - 0.45 * pores)[..., None]
    rows = np.linspace(0, 1, h)[:, None]                                    # 0 = top of handle
    grip = sm(rows, 0.45, 0.62) * (1 - sm(rows, 0.9, 0.97))                 # hand-polished, oiled grip
    col *= (1 - 0.18 * grip)[..., None] * np.array([1.0, 0.95, 0.88])
    col *= (1 - 0.25 * (1 - sm(rows, 0.0, 0.05)))[..., None]                # darker where it meets the head
    Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(f"{OUT}/Wood_albedo.png")
    normal_map(0.6 * fib + 0.4 * fib2 - 1.2 * pores, 3.0, f"{OUT}/Wood_normal.png")
    return np.array(Image.open(f"{OUT}/Wood_albedo.png").resize((128, 512)))

def make_steel(n=1024):
    dimple = tn(n, n, 38, 38, 11) * 0.5 + tn(n, n, 110, 110, 12) * 0.25
    brush = tn(n, n, 4, 150, 13)
    patina = sm(tn(n, n, 6, 6, 14) + 0.4 * tn(n, n, 30, 30, 15), 0.2, 1.6)
    rows = np.linspace(1, 0, n)[:, None]                                    # v = 1 at the top row
    bevel = sm(rows, 0.90, 0.955) * np.ones((1, n))
    base = 112 * (1 + 0.05 * dimple + 0.045 * brush)
    col = np.stack([base * 1.0, base * 1.02, base * 1.06], -1)
    col = col * (1 - 0.38 * patina[..., None]) + np.array([60, 52, 46.]) * (0.38 * patina[..., None])
    col = col * (1 - bevel[..., None]) + np.array([196, 200, 206.]) * bevel[..., None]
    img = Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)); d = ImageDraw.Draw(img)
    r = np.random.default_rng(9)
    hmap = dimple * 0.5
    sc = Image.new("L", (n, n), 0); sd = ImageDraw.Draw(sc)
    for _ in range(220):
        x, y = r.integers(0, n, 2); a = r.uniform(0, math.pi); ln = r.integers(20, 160)
        sd.line([(x, y), (x + math.cos(a) * ln, y + math.sin(a) * ln)], fill=int(r.integers(40, 150)), width=1)
    scr = np.array(sc).astype(np.float32) / 255
    arr = np.array(img).astype(np.float32) + scr[..., None] * 38
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(f"{OUT}/Steel_albedo.png")
    normal_map(hmap - scr * 0.35, 9.0, f"{OUT}/Steel_normal.png")
    metal = np.clip(0.93 - 0.3 * patina - 0.05 * scr, 0, 1)
    smooth = np.clip(0.42 + 0.12 * dimple - 0.28 * patina - 0.12 * scr + 0.38 * bevel, 0.05, 0.95)
    ms = np.dstack([metal * 255, metal * 255, metal * 255, smooth * 255]).astype(np.uint8)
    Image.fromarray(ms, "RGBA").save(f"{OUT}/Steel_MetalSmooth.png")
    return np.array(Image.open(f"{OUT}/Steel_albedo.png").resize((512, 512)))

wood_small = make_wood(); steel_small = make_steel()
print("textures done")

# ------------------------------------------------------------------ geometry helpers
def sgnpow(a, p): return np.sign(a) * np.abs(a) ** p

def loft(rings, uvs, cap0=True, cap1=True):
    K = len(rings[0]); tri = []
    cen = [r[:-1].mean(0) for r in rings]
    side = []
    for j in range(len(rings) - 1):
        A, B, UA, UB = rings[j], rings[j + 1], uvs[j], uvs[j + 1]
        for i in range(K - 1):
            side.append((A[i], A[i + 1], B[i + 1], UA[i], UA[i + 1], UB[i + 1]))
            side.append((A[i], B[i + 1], B[i], UA[i], UB[i + 1], UB[i]))
    # orient sides outward using a middle quad
    mid = len(rings) // 2; j = min(mid, len(rings) - 2); i = K // 4
    t = side[(j * (K - 1) + i) * 2]
    n = np.cross(t[1] - t[0], t[2] - t[0])
    if np.dot(n, (t[0] + t[1] + t[2]) / 3 - cen[j]) < 0:
        side = [(a, c, b, ua, uc, ub) for (a, b, c, ua, ub, uc) in side]
    tri += side
    def cap(ring, uv, away):
        c = ring[:-1].mean(0); vv = uv[:, 1].mean(); out = []
        for i in range(K - 1):
            a, b = ring[i], ring[i + 1]
            nn = np.cross(a - c, b - c)
            if np.dot(nn, away) >= 0: out.append((c, a, b, np.array([.5, vv]), uv[i], uv[i + 1]))
            else: out.append((c, b, a, np.array([.5, vv]), uv[i + 1], uv[i]))
        return out
    if cap0: tri += cap(rings[0], uvs[0], cen[0] - cen[1])
    if cap1: tri += cap(rings[-1], uvs[-1], cen[-1] - cen[-2])
    return tri

def smooth_normals(tri, thr=38):
    P = np.array([[t[0], t[1], t[2]] for t in tri])
    fn = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]); area = np.linalg.norm(fn, axis=1)
    fn = fn / (area[:, None] + 1e-14)
    key = np.round(P.reshape(-1, 3) / 1e-5).astype(np.int64)
    d = defaultdict(list)
    for idx, k in enumerate(map(tuple, key)): d[k].append(idx // 3)
    cosT = math.cos(math.radians(thr)); Nn = np.zeros((len(tri), 3, 3))
    for ti in range(len(tri)):
        for vi in range(3):
            arr = np.array(d[tuple(key[ti * 3 + vi])])
            m = (fn[arr] @ fn[ti]) > cosT
            n = (fn[arr[m]] * area[arr[m]][:, None]).sum(0)
            ln = np.linalg.norm(n)
            Nn[ti, vi] = n / ln if ln > 1e-12 else np.array([0, 1., 0])
    return Nn

# ------------------------------------------------------------------ handle
def handle(top_y, scale_x=1.0, curve=1.0, K=30):
    d = np.concatenate([[-0.004], np.linspace(0, 0.905, 80)])
    kd = [0, 0.10, 0.20, 0.42, 0.68, 0.80, 0.875, 0.905]
    ax = np.interp(d, kd, [0.0262, 0.0262, 0.0215, 0.0186, 0.0197, 0.0235, 0.0300, 0.0300]) * scale_x
    az = np.interp(d, kd, [0.0155, 0.0155, 0.0145, 0.0130, 0.0138, 0.0158, 0.0185, 0.0185])
    nz = 1 + 0.025 * np.sin(d * 23 + 1) + 0.015 * np.sin(d * 51)
    ax = ax * nz; az = az * nz
    ax[0] *= 0.9; az[0] *= 0.9
    xo = curve * (-0.030 * np.clip((d - 0.15) / 0.70, 0, 1) ** 1.8 - 0.012 * np.clip((d - 0.84) / 0.065, 0, 1) ** 2)
    rows = [(top_y - dd, a, b, o, dd) for dd, a, b, o in zip(d, ax, az, xo)]
    for phi in (0.45, 0.85, 1.2, 1.45):                    # rounded knob end
        rows.append((top_y - 0.905 - 0.012 * math.sin(phi), ax[-1] * math.cos(phi), az[-1] * math.cos(phi), xo[-1], 0.905 + 0.012 * math.sin(phi)))
    ph = np.linspace(0, 2 * math.pi, K + 1)
    rings, uvs = [], []
    for y, a, b, o, dd in rows:
        ring = np.stack([o + a * np.cos(ph), np.full_like(ph, y), b * np.sin(ph)], -1)
        rings.append(ring)
        uvs.append(np.stack([ph / (2 * math.pi), np.full_like(ph, 1 - min(dd, 0.917) / 0.917)], -1))
    return loft(rings, uvs)

# ------------------------------------------------------------------ axe head (felling axe)
def axe_head(M=14, S=52):
    rings, uvs = [], []
    vs = np.linspace(-1, 1, M + 1)
    for s in np.linspace(0, 1, S):
        x = -0.075 + 0.22 * s
        hy = np.interp(x, [-0.075, -0.06, -0.03, 0.0, 0.03, 0.06, 0.10, 0.145], [0.024, 0.030, 0.040, 0.043, 0.044, 0.052, 0.064, 0.078])
        w = np.clip((x - 0.03) / 0.115, 0, 1)
        my = -0.010 * w
        h0 = np.interp(x, [-0.075, -0.05, -0.03, 0.0, 0.03], [0.0195, 0.0205, 0.0225, 0.0242, 0.0242])
        hz = 0.0012 + (h0 - 0.0012) * (1 - w) ** 1.7 if x > 0.03 else h0
        c = np.interp(x, [-0.075, -0.03, 0.03, 0.145], [0.10, 0.20, 0.35, 0.55])
        xp = x - 0.032 * w ** 2.2 * vs ** 2                     # curved bit edge, toes pulled back
        top = [(xp[j], my + hy * vs[j], hz * (1 - c * vs[j] ** 2)) for j in range(M + 1)]
        bot = [(xp[j], my + hy * vs[j], -hz * (1 - c * vs[j] ** 2)) for j in range(M, -1, -1)]
        ring = np.array(top + bot + [top[0]], float)
        rings.append(ring)
        u = np.linspace(0, 1, len(ring))
        uvs.append(np.stack([u, np.full_like(u, s)], -1))
    return loft(rings, uvs)

# ------------------------------------------------------------------ pick head (pickaxe)
def pick_head(S=70, K=28):
    R = 0.27 / math.sin(0.55); rings, uvs = [], []
    ph = np.linspace(0, 2 * math.pi, K + 1)
    for s in np.linspace(-1, 1, S):
        th = 0.55 * s; a = abs(s)
        c = np.array([R * math.sin(th), -R * (1 - math.cos(th)) + 0.0, 0.0])
        Nv = np.array([math.sin(th), math.cos(th), 0.0]); Zv = np.array([0, 0, 1.0])
        boss = math.exp(-(s / 0.17) ** 2)
        rN = (0.0012 + (0.029 - 0.0012) * (1 - a) ** 1.35) * (1 + 0.30 * boss)
        rZ = (0.0012 + (0.023 - 0.0012) * (1 - a) ** 1.35) * (1 + 0.25 * boss)
        e = 2 + 1.6 * boss
        ring = np.array([c + Nv * rN * sgnpow(math.cos(p), 2 / e) + Zv * rZ * sgnpow(math.sin(p), 2 / e) for p in ph])
        rings.append(ring)
        uvs.append(np.stack([ph / (2 * math.pi), np.full_like(ph, 0.25 + 0.75 * a)], -1))
    return loft(rings, uvs)

# ------------------------------------------------------------------ assemble + export
PIVOT = 0.62
def to_unity(tri):
    def tf(p): return np.array([-p[2], p[1] + PIVOT, p[0]])       # blade (+x) -> +Z, handle stays +Y
    return [(tf(a), tf(b), tf(c), ua, ub, uc) for (a, b, c, ua, ub, uc) in tri]

def build(name, head_tris, handle_tris):
    parts = {"Wood": to_unity(handle_tris), "Steel": to_unity(head_tris)}
    verts, uvs, norms, groups = [], [], [], {}
    cache = {}
    for mat, tri in parts.items():
        Nn = smooth_normals(tri); faces = []
        for ti, t in enumerate(tri):
            if np.linalg.norm(np.cross(t[1] - t[0], t[2] - t[0])) < 1e-12: continue
            idx = []
            for vi in range(3):
                p, uv, n = t[vi], t[3 + vi], Nn[ti, vi]
                k = (tuple(np.round(p, 5)), tuple(np.round(uv, 4)), tuple(np.round(n, 3)), mat)
                if k not in cache:
                    cache[k] = len(verts) + 1; verts.append(p); uvs.append(uv); norms.append(n)
                idx.append(cache[k])
            faces.append(idx)
        groups[mat] = faces
    L = ["# " + name, "mtllib Tools.mtl", "o " + name]
    L += ["v %.5f %.5f %.5f" % tuple(v) for v in verts]
    L += ["vt %.5f %.5f" % tuple(t) for t in uvs]
    L += ["vn %.4f %.4f %.4f" % tuple(n) for n in norms]
    for mat, faces in groups.items():
        L.append("usemtl " + mat)
        L += ["f %d/%d/%d %d/%d/%d %d/%d/%d" % (a, a, a, b, b, b, c, c, c) for a, b, c in faces]
    open(f"{OUT}/{name}.obj", "w").write("\n".join(L) + "\n")
    va = np.array(verts)
    print(name, "verts", len(verts), "tris", sum(len(f) for f in groups.values()),
          "bounds", va.min(0).round(3), va.max(0).round(3))
    return parts

axe_parts = build("FellingAxe", axe_head(), handle(0.055, 1.0, 1.0))
pick_parts = build("Pickaxe", pick_head(), handle(0.046, 1.0, 0.15))
open(f"{OUT}/Tools.mtl", "w").write(
    "newmtl Wood\nKd 1 1 1\nmap_Kd Wood_albedo.png\nmap_Bump Wood_normal.png\n\n"
    "newmtl Steel\nKd 1 1 1\nmap_Kd Steel_albedo.png\nmap_Bump Steel_normal.png\n")

# ------------------------------------------------------------------ preview
def render(parts, W=620, H=900, SS=2, yaw=38, pitch=14, zoom=1.0, yc=0.223, xc=0.0):
    w, h = W * SS, H * SS
    img = np.zeros((h, w, 3), np.float32); zb = np.full((h, w), -1e9, np.float32)
    g = np.linspace(0, 1, h)[:, None, None]
    img[:] = np.array([226, 230, 234]) * (1 - g) + np.array([168, 176, 186]) * g
    ca, sa = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    def cam(p):
        x = p[0] * ca + p[2] * sa; z = -p[0] * sa + p[2] * ca
        return np.array([x, p[1] * cp - z * sp, p[1] * sp + z * cp])
    def camdir(n): return cam(n)
    sc = h * 0.9 / 0.95 * zoom; ox, oy = w / 2 - xc * sc, h / 2 + yc * sc
    Ld = np.array([-0.4, 0.7, 0.6]); Ld /= np.linalg.norm(Ld)
    V = np.array([0, 0, 1.0])
    for mat in ("Wood", "Steel"):
        tex = wood_small if mat == "Wood" else steel_small; th, tw = tex.shape[:2]
        tri = parts[mat]; Nn = smooth_normals(tri)
        for ti, t in enumerate(tri):
            P = [cam(t[k]) for k in range(3)]
            xs = np.array([ox + p[0] * sc for p in P]); ys = np.array([oy - p[1] * sc for p in P])
            ys = ys - 0.0
            x0, x1 = int(max(xs.min(), 0)), int(min(xs.max() + 1, w - 1)); y0, y1 = int(max(ys.min(), 0)), int(min(ys.max() + 1, h - 1))
            if x1 <= x0 or y1 <= y0: continue
            den = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
            if abs(den) < 1e-9: continue
            gx, gy = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
            l0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / den
            l1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / den
            l2 = 1 - l0 - l1
            m = (l0 >= 0) & (l1 >= 0) & (l2 >= 0)
            if not m.any(): continue
            z = l0 * P[0][2] + l1 * P[1][2] + l2 * P[2][2]
            m &= z > zb[y0:y1, x0:x1]
            if not m.any(): continue
            u = l0 * t[3][0] + l1 * t[4][0] + l2 * t[5][0]; v = l0 * t[3][1] + l1 * t[4][1] + l2 * t[5][1]
            tx = ((u % 1) * tw).astype(int) % tw; ty = ((1 - np.clip(v, 0, 0.9999)) * th).astype(int)
            alb = tex[np.clip(ty, 0, th - 1), tx].astype(np.float32)
            n = sum(l[..., None] * camdir(Nn[ti, k]) for k, l in enumerate((l0, l1, l2)))
            n /= np.linalg.norm(n, axis=-1, keepdims=True) + 1e-9
            dif = np.clip(n @ (cam(Ld)), 0, 1)[..., None]
            if mat == "Wood":
                hv = (cam(Ld) + V); hv /= np.linalg.norm(hv)
                spec = np.clip(n @ hv, 0, 1)[..., None] ** 30 * 0.12
                col = alb * (0.32 + 0.78 * dif) + 255 * spec
            else:
                r = 2 * (n @ V)[..., None] * n - V
                up = np.clip((r[..., 1:2] + 0.3) / 1.1, 0, 1)
                env = np.array([75, 68, 62.]) * (1 - up) + np.array([215, 228, 242.]) * up
                hv = (cam(Ld) + V); hv /= np.linalg.norm(hv)
                spec = np.clip(n @ hv, 0, 1)[..., None] ** 60 * 0.9
                col = alb * (0.18 + 0.4 * dif) + env * (alb / 255.0) * 0.62 + 255 * spec * 0.5
            reg = img[y0:y1, x0:x1]; reg[m] = col[m]; zb[y0:y1, x0:x1][m] = z[m]
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)

panels = [render(axe_parts), render(axe_parts, zoom=3.6, yc=0.64, xc=0.0, yaw=62, pitch=18),
          render(pick_parts), render(pick_parts, zoom=2.4, yc=0.62, xc=0.0, yaw=30, pitch=14)]
sheet = Image.new("RGB", (panels[0].width * 4, panels[0].height))
for i, pnl in enumerate(panels): sheet.paste(pnl, (i * panels[0].width, 0))
sheet.save(f"{OUT}/preview.png")
print("preview done")
