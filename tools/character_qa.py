#!/usr/bin/env python3
"""character_qa.py — one-shot QA gate for Orlo character GLBs.

Consolidates the manual rig/model audit into a single command so per-model
cost is "drop file, get green check" instead of a debugging marathon. Checks a
character .glb against the LOCKED Orlo character standard (settled 2026-07-04):

  1. Geometry sanity      — loads, has faces/verts, no degenerate spam
  2. Bone-set conformance — matches v2.3.0 canon (52 bones), no missing/extra
  3. Weights              — every bone weighted; no orphan verts / neutral_bone
  4. Origin at feet       — min(POSITION.y) ~= 0   (else it hovers in-client)
  5. Y-up                 — Y is the tallest axis  (else it lies down/rotates)
  6. Facing +Z            — bind pose faces forward (else upside-down/backwards)
  7. Landmarks            — orlo_lm_* reference points present
  8. Skin texture         — a material with a baseColorTexture is present (else
                            it renders as a flat/white mannequin in-client)

The client trusts a skinned mesh's bind pose (auto-orient is skipped), so
checks 4/5/6 are what actually keep a character from hovering or flipping
once it's loaded. See memory: project_orla_rig_convention.

Usage:
  character_qa.py MODEL.glb [--canon-from BONE_GEN.py] [--json] [-v]

  --canon-from  extract the canonical bone list live from a bone_generator
                __init__.py (e.g. a git-show of randy/blender-addon-iter) so
                the gate never drifts from the real canon. Default: the
                hardcoded v2.3.0 set below (UPDATE when the canon changes).

Exit code: 0 = PASS, 1 = FAIL, 2 = usage/parse error. CI-friendly.
"""
import sys, json, struct, argparse, re
import numpy as np

# --- v2.3.0 canonical skeleton (branch randy/blender-addon-iter, 52 bones) ---
# UPDATE this when the bone_generator canon changes, or pass --canon-from.
CANON_BONES = [
    "root", "spine1", "spine2", "spine3", "neck", "head", "lower_jaw",
    "upper_lip", "nose", "l_lid", "r_lid", "l_eyebrow", "r_eyebrow",
    "l_smile", "r_smile", "lower_lip", "l_bottom_lip", "r_bottom_lip",
    "l_clav", "l_shoulder", "l_upperarm", "l_forearm", "l_wrist", "l_hand",
    "l_thumb1", "l_thumb2", "l_index1", "l_index2", "l_fingers1", "l_fingers2",
    "r_clav", "r_shoulder", "r_upperarm", "r_forearm", "r_wrist", "r_hand",
    "r_thumb1", "r_thumb2", "r_index1", "r_index2", "r_fingers1", "r_fingers2",
    "l_thigh", "l_lowerleg", "l_ankle", "l_toes", "l_bigtoe",
    "r_thigh", "r_lowerleg", "r_ankle", "r_toes", "r_bigtoe",
]

# Bones that are commonly unweighted without it being a defect (tip bones that
# ride their parent). An unweighted bone NOT in this set is a hard FAIL — e.g.
# an unbound lower_jaw means the mouth can't move.
OPTIONAL_UNWEIGHTED = {"l_bigtoe", "r_bigtoe"}

_COMP = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2), 5123: ("H", 2),
         5125: ("I", 4), 5126: ("f", 4)}
_NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def load_glb(path):
    with open(path, "rb") as f:
        data = f.read()
    if data[:4] != b"glTF":
        raise ValueError("not a binary glTF (.glb)")
    off, js, bn = 12, None, None
    while off < len(data):
        clen = struct.unpack("<I", data[off:off + 4])[0]
        ctype = data[off + 4:off + 8]
        chunk = data[off + 8:off + 8 + clen]
        if ctype == b"JSON":
            js = json.loads(chunk)
        elif ctype == b"BIN\x00":
            bn = chunk
        off += 8 + clen
    if js is None:
        raise ValueError("no JSON chunk")
    return js, bn


def accessor(js, bn, idx):
    a = js["accessors"][idx]
    bv = js["bufferViews"][a["bufferView"]]
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    comp, _ = _COMP[a["componentType"]]
    nc = _NCOMP[a["type"]]
    arr = np.frombuffer(bn, dtype=np.dtype("<" + comp),
                        count=a["count"] * nc, offset=start)
    out = arr.reshape(a["count"], nc).astype(np.float64)
    if a.get("normalized") and comp in ("B", "H", "b", "h"):
        maxv = {"B": 255.0, "H": 65535.0, "b": 127.0, "h": 32767.0}[comp]
        out = out / maxv
    return out


def canon_from_file(path):
    """Extract SKELETON bone names from a bone_generator __init__.py."""
    with open(path) as f:
        text = f.read()
    names = re.findall(r'^\s{4}"([a-z_0-9]+)":\s*\{', text, re.M)
    # de-dup preserving order
    seen, out = set(), []
    for n in names:
        if n not in seen:
            seen.add(n); out.append(n)
    return out


class Result:
    def __init__(self):
        self.checks = []  # (name, status, detail)  status in PASS/FAIL/WARN

    def add(self, name, status, detail=""):
        self.checks.append((name, status, detail))

    @property
    def passed(self):
        return not any(s == "FAIL" for _, s, _ in self.checks)


def qa(path, canon):
    r = Result()
    js, bn = load_glb(path)
    names = [n.get("name", "") for n in js.get("nodes", [])]

    # ---- gather mesh data ----
    POS, JNT, WGT, IDX = [], [], [], []
    vbase = 0
    for m in js.get("meshes", []):
        for pr in m["primitives"]:
            at = pr["attributes"]
            if "POSITION" not in at:
                continue
            P = accessor(js, bn, at["POSITION"]); POS.append(P)
            if "JOINTS_0" in at and "WEIGHTS_0" in at:
                JNT.append(accessor(js, bn, at["JOINTS_0"]))
                WGT.append(accessor(js, bn, at["WEIGHTS_0"]))
            if "indices" in pr:
                IDX.append(accessor(js, bn, pr["indices"]).astype(int).ravel() + vbase)
            vbase += len(P)
    if not POS:
        r.add("geometry", "FAIL", "no mesh POSITION data")
        return r, js
    P = np.vstack(POS)

    # ---- 1. geometry sanity ----
    nfaces = sum(len(i) for i in IDX) // 3 if IDX else 0
    degdet = ""
    if IDX and nfaces:
        tris = np.concatenate(IDX).reshape(-1, 3)
        v = P[tris]
        area = 0.5 * np.linalg.norm(np.cross(v[:, 1] - v[:, 0], v[:, 2] - v[:, 0]), axis=1)
        ndeg = int((area < 1e-9).sum())
        degdet = f", {ndeg} degenerate" if ndeg else ""
    if len(P) == 0 or nfaces == 0:
        r.add("geometry", "FAIL", f"{len(P)} verts / {nfaces} faces")
    else:
        r.add("geometry", "PASS", f"{len(P)} verts / {nfaces} faces{degdet}")

    ext = P.max(0) - P.min(0)
    height = ext[1]

    # ---- skin / joints ----
    joints = js["skins"][0]["joints"] if js.get("skins") else []
    jn = [names[j] for j in joints]

    # weighted-vert count per joint (skin-joint index)
    cnt = np.zeros(max(len(joints), 1), int)
    if JNT and joints:
        J = np.vstack(JNT); W = np.vstack(WGT)
        for row_j, row_w in zip(J, W):
            for ji, wv in zip(row_j, row_w):
                if wv > 1e-5:
                    cnt[int(ji)] += 1

    # ---- 2. bone-set conformance ----
    jset, cset = set(jn), set(canon)
    missing = sorted(cset - jset)
    extra = sorted(jset - cset - {"neutral_bone"})
    nbones = len(jset - {"neutral_bone"})  # canon-relevant count (exclude artifact)
    if not joints:
        r.add("bones", "FAIL", "no skin/joints in GLB")
    elif missing or extra:
        d = []
        if missing: d.append(f"missing {missing}")
        if extra: d.append(f"extra {extra}")
        r.add("bones", "FAIL", f"{nbones} bones; " + "; ".join(d))
    else:
        r.add("bones", "PASS", f"{nbones} bones, exact canon match")

    # ---- 3. weights / neutral_bone / orphans ----
    if "neutral_bone" in jn:
        nbverts = cnt[jn.index("neutral_bone")]
        r.add("neutral_bone", "FAIL",
              f"present with {nbverts} verts — unweighted geometry (Blender "
              f"export artifact; reweight orphans + re-export)")
    else:
        r.add("neutral_bone", "PASS", "absent")

    if joints:
        unw = [jn[i] for i in range(len(joints))
               if cnt[i] == 0 and jn[i] != "neutral_bone"]
        core_unw = [b for b in unw if b not in OPTIONAL_UNWEIGHTED]
        if core_unw:
            r.add("weights", "FAIL",
                  f"{len(core_unw)} deform bone(s) carry 0 verts: {core_unw} "
                  f"(vgroup-name mismatch? won't deform — reweight + re-export)")
        elif unw:
            r.add("weights", "WARN", f"tip bone(s) unweighted: {unw} (usually fine)")
        else:
            r.add("weights", "PASS", "every bone weighted")

    # ---- 4. origin at feet ----
    feet_y = float(P[:, 1].min())
    tol = max(0.02 * height, 0.01)
    if abs(feet_y) <= tol:
        r.add("origin@feet", "PASS", f"min Y={feet_y:.3f}")
    else:
        r.add("origin@feet", "FAIL",
              f"origin sits {-feet_y:.3f} {'above' if feet_y < 0 else 'below'} "
              f"feet (min Y={feet_y:.3f}) — will hover/sink in-client; "
              f"re-origin to feet + re-export")

    # ---- 5. Y-up (upright) ----
    tallest = "XYZ"[int(np.argmax(ext))]
    if tallest == "Y":
        r.add("Y-up", "PASS", f"extents X={ext[0]:.2f} Y={ext[1]:.2f} Z={ext[2]:.2f}")
    else:
        r.add("Y-up", "FAIL",
              f"tallest axis is {tallest}, not Y "
              f"(X={ext[0]:.2f} Y={ext[1]:.2f} Z={ext[2]:.2f}) — not upright")

    # ---- 6. facing +Z ----
    ymax = P[:, 1].max()
    band = P[(P[:, 1] > ymax * 0.90) & (P[:, 1] < ymax * 0.965)]
    if len(band):
        fwd = band[np.argmax(np.abs(band[:, 2]))]
        if fwd[2] > 0:
            r.add("facing+Z", "PASS", f"face at Z=+{fwd[2]:.3f}")
        else:
            r.add("facing+Z", "FAIL",
                  f"face at Z={fwd[2]:.3f} (faces -Z) — bind pose must face +Z; "
                  f"rotate 180° about Y + apply before export")
    else:
        r.add("facing+Z", "WARN", "no head-band verts to judge facing")

    # ---- 7. landmarks ----
    lms = [n for n in names if n.startswith("orlo_lm_")]
    if len(lms) >= 40:
        r.add("landmarks", "PASS", f"{len(lms)} orlo_lm_ present")
    elif lms:
        r.add("landmarks", "WARN", f"only {len(lms)} orlo_lm_ present (expected ~49+)")
    else:
        r.add("landmarks", "WARN", "no orlo_lm_ reference points")

    # ---- 8. skin texture present ----
    # Two failure modes both seen in the wild (qravey playtest 2026-07-08):
    #   (a) 0 materials         → male base, renders default-shader white
    #   (b) materials but no    → female base "orla_flat", flat baseColorFactor,
    #       baseColorTexture       no albedo map
    # Both hard-FAIL: a base without a skin albedo ships as a mannequin.
    mats = js.get("materials", [])
    if not mats:
        r.add("skin-texture", "FAIL",
              "0 materials in GLB — renders as an untextured default-shader "
              "mannequin; export with a skin material + baked albedo")
    else:
        n_albedo = sum(
            1 for m in mats
            if "baseColorTexture" in m.get("pbrMetallicRoughness", {})
        )
        if n_albedo == 0:
            r.add("skin-texture", "FAIL",
                  f"{len(mats)} material(s) but 0 baseColorTexture — flat-colour "
                  f"placeholder, no skin albedo; bake + embed the skin textures")
        else:
            r.add("skin-texture", "PASS",
                  f"{n_albedo}/{len(mats)} material(s) carry a baseColorTexture")

    return r, js


ICON = {"PASS": "\033[32m✓\033[0m", "FAIL": "\033[31m✗\033[0m",
        "WARN": "\033[33m⚠\033[0m"}


def main():
    ap = argparse.ArgumentParser(description="Orlo character GLB QA gate")
    ap.add_argument("glb")
    ap.add_argument("--canon-from", metavar="BONE_GEN.py",
                    help="extract canon bone list from a bone_generator __init__.py")
    ap.add_argument("--json", action="store_true", help="machine-readable output")
    ap.add_argument("-v", "--verbose", action="store_true")
    args = ap.parse_args()

    canon = CANON_BONES
    canon_src = "hardcoded v2.3.0"
    if args.canon_from:
        try:
            canon = canon_from_file(args.canon_from)
            canon_src = args.canon_from + f" ({len(canon)} bones)"
        except Exception as e:
            print(f"error reading --canon-from: {e}", file=sys.stderr)
            return 2

    try:
        r, _ = qa(args.glb, canon)
    except Exception as e:
        if args.json:
            print(json.dumps({"glb": args.glb, "verdict": "ERROR", "error": str(e)}))
        else:
            print(f"\033[31mERROR\033[0m parsing {args.glb}: {e}")
        return 2

    verdict = "PASS" if r.passed else "FAIL"
    if args.json:
        print(json.dumps({
            "glb": args.glb, "verdict": verdict, "canon": canon_src,
            "checks": [{"name": n, "status": s, "detail": d} for n, s, d in r.checks],
        }, indent=2))
    else:
        print(f"\n  Orlo character QA — {args.glb}")
        print(f"  canon: {canon_src}\n")
        for n, s, d in r.checks:
            print(f"    {ICON[s]} {n:<14} {d}")
        v = "\033[32mPASS\033[0m" if r.passed else "\033[31mFAIL\033[0m"
        nwarn = sum(1 for _, s, _ in r.checks if s == "WARN")
        print(f"\n  Verdict: {v}" + (f"  ({nwarn} warning(s))" if nwarn else "") + "\n")
    return 0 if r.passed else 1


if __name__ == "__main__":
    sys.exit(main())
