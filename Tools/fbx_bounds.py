#!/usr/bin/env python3
"""
fbx_bounds.py — mede os modelos KayKit (FBX binário) sem precisar do Unity.

Uso:  python3 Tools/fbx_bounds.py            (gera Tools/model_sizes.json)

Lê o formato binário Kaydara:
  cabeçalho de 27 bytes ("Kaydara FBX Binary  \\0" + 0x1A 0x00 + versão uint32)
  registros de nó: endOffset, numProps, propListLen (uint32; uint64 se versão >= 7500),
  nameLen (uint8), nome, propriedades tipadas, filhos, registro nulo.
  Arrays 'f','d','i','l','b' podem vir comprimidos com zlib (encoding = 1).

Para cada arquivo:
  - pega GlobalSettings: UnitScaleFactor, UpAxis/FrontAxis/CoordAxis (+ sinais);
  - pega cada Geometry (prop "Vertices") e cada Model (Lcl Translation/Rotation/Scaling,
    PreRotation, RotationOrder ignorada = XYZ);
  - usa Connections (OO) para descobrir a hierarquia Model->Model e Geometry->Model;
  - transforma os vértices para o espaço do arquivo e mede o bounding box.

SUPOSIÇÃO DE ESCALA (importante):
  O Unity, com "Convert Units" ligado (padrão), aplica fator = UnitScaleFactor / 100
  (UnitScaleFactor está em cm por unidade do arquivo). Ex.: UnitScaleFactor=100 (metros)
  -> fator 1; UnitScaleFactor=1 (cm) -> 0,01. O tamanho reportado = bbox * fator,
  com o eixo "up" do arquivo virando Y no Unity. "Scale Factor" do importer = 1.
  Se o Animador 3D mudar globalScale no AssetPostprocessor, multiplique por ele.
"""
import json, math, os, struct, sys, zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
KAYKIT = os.path.join(ROOT, "Assets", "KayKit")
FOLDERS = ["Forest", "Medieval", "Tools", "Blocks", "Resources", "Characters", "Weapons"]


class Node:
    __slots__ = ("name", "props", "children")

    def __init__(self, name, props, children):
        self.name, self.props, self.children = name, props, children

    def find(self, name):
        for c in self.children:
            if c.name == name:
                return c
        return None

    def findall(self, name):
        return [c for c in self.children if c.name == name]


def read_props(data, pos, n):
    props = []
    for _ in range(n):
        t = chr(data[pos]); pos += 1
        if t == 'Y': props.append(struct.unpack_from('<h', data, pos)[0]); pos += 2
        elif t == 'C': props.append(bool(data[pos])); pos += 1
        elif t == 'I': props.append(struct.unpack_from('<i', data, pos)[0]); pos += 4
        elif t == 'F': props.append(struct.unpack_from('<f', data, pos)[0]); pos += 4
        elif t == 'D': props.append(struct.unpack_from('<d', data, pos)[0]); pos += 8
        elif t == 'L': props.append(struct.unpack_from('<q', data, pos)[0]); pos += 8
        elif t in 'fdilb':
            alen, enc, clen = struct.unpack_from('<III', data, pos); pos += 12
            raw = data[pos:pos + clen]; pos += clen
            if enc == 1:
                raw = zlib.decompress(raw)
            fmt = {'f': 'f', 'd': 'd', 'i': 'i', 'l': 'q', 'b': 'B'}[t]
            props.append(list(struct.unpack('<%d%s' % (alen, fmt), raw[:alen * struct.calcsize(fmt)])))
        elif t in 'SR':
            ln = struct.unpack_from('<I', data, pos)[0]; pos += 4
            raw = data[pos:pos + ln]; pos += ln
            props.append(raw.decode('utf-8', 'replace') if t == 'S' else raw)
        else:
            raise ValueError("tipo de propriedade desconhecido %r em %d" % (t, pos - 1))
    return props, pos


def read_node(data, pos, big):
    if big:
        end, nprops, plen = struct.unpack_from('<QQQ', data, pos); pos += 24
    else:
        end, nprops, plen = struct.unpack_from('<III', data, pos); pos += 12
    nlen = data[pos]; pos += 1
    if end == 0:
        return None, pos
    name = data[pos:pos + nlen].decode('ascii', 'replace'); pos += nlen
    props, pos2 = read_props(data, pos, nprops)
    pos = pos + plen
    children = []
    null_len = 25 if big else 13
    while pos < end:
        if end - pos == null_len and data[pos:end] == b'\0' * null_len:
            pos = end
            break
        ch, pos = read_node(data, pos, big)
        if ch is None:
            break
        children.append(ch)
    return Node(name, props, children), end


def parse(path):
    data = open(path, 'rb').read()
    if not data.startswith(b'Kaydara FBX Binary'):
        raise ValueError("não é FBX binário")
    ver = struct.unpack_from('<I', data, 23)[0]
    big = ver >= 7500
    pos = 27
    top = []
    while pos < len(data):
        n, pos = read_node(data, pos, big)
        if n is None:
            break
        top.append(n)
    return Node("root", [], top), ver


def p70(node):
    """Lê Properties70 -> dict nome -> lista de valores."""
    out = {}
    if node is None:
        return out
    p = node.find("Properties70")
    if p is None:
        return out
    for P in p.findall("P"):
        if P.props:
            out[P.props[0]] = P.props[4:]
    return out


def rot_matrix(rx, ry, rz):
    """Euler XYZ (FBX eEulerXYZ): R = Rz * Ry * Rx (aplica X primeiro)."""
    rx, ry, rz = map(math.radians, (rx, ry, rz))
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    Rx = [[1, 0, 0], [0, cx, -sx], [0, sx, cx]]
    Ry = [[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]
    Rz = [[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]]
    return mm(Rz, mm(Ry, Rx))


def mm(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def local_affine(props):
    t = props.get("Lcl Translation", [0, 0, 0])
    r = props.get("Lcl Rotation", [0, 0, 0])
    s = props.get("Lcl Scaling", [1, 1, 1])
    pre = props.get("PreRotation", [0, 0, 0])
    R = mm(rot_matrix(*pre), rot_matrix(*r))
    M = [[R[i][j] * s[j] for j in range(3)] for i in range(3)]
    return M, list(t)


def compose(parent, child):
    PM, Pt = parent
    CM, Ct = child
    M = mm(PM, CM)
    t = [sum(PM[i][k] * Ct[k] for k in range(3)) + Pt[i] for i in range(3)]
    return M, t


IDENT = ([[1, 0, 0], [0, 1, 0], [0, 0, 1]], [0, 0, 0])


def measure(path):
    root, ver = parse(path)
    gs = root.find("GlobalSettings")
    g = p70(gs)
    unit = float(g.get("UnitScaleFactor", [1.0])[0])
    up = int(g.get("UpAxis", [1])[0]); up_s = int(g.get("UpAxisSign", [1])[0])
    objs = root.find("Objects")
    geoms, models = {}, {}
    for o in (objs.children if objs else []):
        if o.name == "Geometry":
            v = o.find("Vertices")
            if v is not None and v.props:
                geoms[o.props[0]] = v.props[0]
        elif o.name == "Model":
            models[o.props[0]] = local_affine(p70(o))
    parent_of, geo_owner = {}, {}
    conns = root.find("Connections")
    for c in (conns.children if conns else []):
        if c.props and c.props[0] == "OO":
            a, b = c.props[1], c.props[2]
            if a in models and b in models:
                parent_of[a] = b
            elif a in geoms and b in models:
                geo_owner.setdefault(a, []).append(b)

    cache = {}

    def world(mid, depth=0):
        if mid in cache:
            return cache[mid]
        loc = models[mid]
        p = parent_of.get(mid)
        w = compose(world(p, depth + 1), loc) if (p is not None and depth < 64) else loc
        cache[mid] = w
        return w

    mn = [1e30] * 3; mx = [-1e30] * 3
    count = 0
    for gid, verts in geoms.items():
        owners = geo_owner.get(gid) or [None]
        for mid in owners:
            M, t = world(mid) if mid is not None else IDENT
            for i in range(0, len(verts) - 2, 3):
                x, y, z = verts[i], verts[i + 1], verts[i + 2]
                p = [M[0][0] * x + M[0][1] * y + M[0][2] * z + t[0],
                     M[1][0] * x + M[1][1] * y + M[1][2] * z + t[1],
                     M[2][0] * x + M[2][1] * y + M[2][2] * z + t[2]]
                for k in range(3):
                    if p[k] < mn[k]: mn[k] = p[k]
                    if p[k] > mx[k]: mx[k] = p[k]
                count += 1
    if count == 0:
        return None
    f = unit / 100.0
    # converte para eixos Unity (Y para cima). Z-up -> (x, z, y).
    if up == 2:
        mn = [mn[0], mn[2] * up_s, mn[1]]; mx = [mx[0], mx[2] * up_s, mx[1]]
        mn, mx = [min(a, b) for a, b in zip(mn, mx)], [max(a, b) for a, b in zip(mn, mx)]
    size = [round((mx[k] - mn[k]) * f, 4) for k in range(3)]
    center = [round((mx[k] + mn[k]) * 0.5 * f, 4) for k in range(3)]
    return {"size": size, "center": center, "minY": round(mn[1] * f, 4),
            "unitScale": unit, "upAxis": up, "version": ver, "verts": count}


def main():
    out = {}
    for folder in FOLDERS:
        d = os.path.join(KAYKIT, folder)
        if not os.path.isdir(d):
            continue
        for fn in sorted(os.listdir(d)):
            if not fn.lower().endswith(".fbx"):
                continue
            mid = os.path.splitext(fn)[0]
            try:
                r = measure(os.path.join(d, fn))
            except Exception as e:  # noqa
                print("ERRO", fn, e, file=sys.stderr)
                continue
            if r:
                r["folder"] = folder
                out[mid] = r
    dst = os.path.join(ROOT, "Tools", "model_sizes.json")
    with open(dst, "w") as fp:
        json.dump(out, fp, indent=1, sort_keys=True)
    for k, v in sorted(out.items()):
        print("%-34s %-10s size=%s unit=%s up=%s" % (k, v["folder"], v["size"], v["unitScale"], v["upAxis"]))
    print("->", dst, len(out), "modelos")


if __name__ == "__main__":
    main()
