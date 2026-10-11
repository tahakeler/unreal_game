#!/usr/bin/env python3
"""HARVEST PANIC - Tripo3D batch generator.

Parses design/art/asset-specs/tripo-batch-01.md into tools/tripo/batch01.json,
then (unless --dry-run) submits text-to-model tasks, polls with backoff,
downloads GLB + preview render into tools/tripo/out/<id>/, and optionally runs
the rig/animate step. Progress is kept in tools/tripo/state.json (resumable,
never contains the API key).

Security: the API key is read ONLY from env var TRIPO_API_KEY. It is never
printed, logged, or written to disk.

Usage:
  python3 tools/tripo/tripo_gen.py --priority P0 --dry-run
  python3 tools/tripo/tripo_gen.py --priority P0 --max-credits 400
  python3 tools/tripo/tripo_gen.py --only SK_Pig_Hamlet --reroll
"""
import argparse
import json
import shutil
import subprocess
import os
import re
import sys
import time
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SPEC = os.path.join(ROOT, "design", "art", "asset-specs", "tripo-batch-01.md")
MANIFEST = os.path.join(HERE, "batch01.json")
STATE = os.path.join(HERE, "state.json")
OUT = os.path.join(HERE, "out")

API = "https://openapi.tripo3d.ai/v3"
MODEL = "v3.1-20260211"  # API rejects "tripo-v3.1"; allowed list returned by HTTP 400
# Rig / retarget / balance go through the official tripo-cli (0.6.1), whose
# flags are verified via `tripo anim rig --help` etc. It reads TRIPO_API_KEY
# from the environment we pass it. Override binary with TRIPO_CLI.
TRIPO_CLI = os.environ.get("TRIPO_CLI", "tripo")
RIG_TYPES = {"quadruped": "quadruped", "biped": "biped", "avian": "avian"}
# Credit cost ESTIMATES used only by the --max-credits guard. Real cost comes
# from the API if it reports it (stored in state as credits_reported).
COST = {"text_to_model": 30, "rig": 25, "anim": 10}
TERMINAL = {"success", "failed", "cancelled", "banned"}
MAX_ROLLS = 3  # spec section 5: max 3 rolls, then image-to-3D from paintover


# ---------------------------------------------------------------- parsing
def _code(s):
    m = re.search(r"`([^`]+)`", s)
    return m.group(1).strip() if m else None


def _block_after(text, heading):
    i = text.index(heading)
    m = re.search(r"```\n(.*?)\n```", text[i:], re.S)
    return m.group(1).strip()


def _num(s):
    return float(s.replace(",", ""))


def _sockets(line):
    out = []
    for raw in re.findall(r"`([^`]+)`", line):
        m = re.match(r"^(\w+?)_?(\d+)\.\.(\d+)$", raw)
        if m:  # egg_01..06
            base, a, b = m.group(1).rstrip("_"), m.group(2), m.group(3)
            out += ["%s_%0*d" % (base, len(a), n) for n in range(int(a), int(b) + 1)]
        elif "/" in raw and "_" in raw:  # carry_N/E/S/W
            base, rest = raw.split("_", 1)
            out += ["%s_%s" % (base, p) for p in rest.split("/")]
        else:
            out.append(raw)
    return out


def _collision(line):
    l = line.lower()
    for key in ("ucx", "capsule", "convex", "sphere", "box"):
        if key in l:
            return {"ucx": "ucx_custom", "convex": "convex"}.get(key, key)
    if "no collision" in l:
        return "none"
    return "convex"


def _scale(line):
    nums = re.findall(r"(\d+(?:\.\d+)?)\s*(?=cm|×|\s*[WDH]\b|\s*cm)", line)
    vals = [float(n) for n in nums]
    return {"raw": line.strip(), "max_dim_cm": max(vals) if vals else None}


def parse_spec(path=SPEC):
    text = open(path, encoding="utf-8").read()
    prefix = _block_after(text, "Shared Style Prefix")
    shared_neg = _block_after(text, "Shared negative prompt")
    items = []
    # P0 + P1 sections: "### N. ID"
    sections = re.split(r"\n### \d+\. ", text)
    pri_at = [(m.start(), m.group(1)) for m in re.finditer(r"\n## \d+\. (P\d)", text)]
    for sec in sections[1:]:
        start = text.index(sec)
        pri = [p for pos, p in pri_at if pos < start][-1]
        head = sec.split("\n", 1)[0]
        sid = re.match(r"(\w+)", head).group(1)
        lines = {}
        for ln in sec.split("\n"):
            m = re.match(r"- \*\*([^*]+)\*\*:?\s*(.*)", ln)
            if m:
                lines[m.group(1).strip().rstrip(":")] = m.group(2)
        stats = next((l for k, l in lines.items() if k.startswith("Tris")), "")
        stats = "**Tris** " + stats if stats else ""
        stats_line = next((ln for ln in sec.split("\n") if ln.startswith("- **Tris**")), "")
        tris = re.search(r"\*\*Tris\*\*\s*([\d,.]+)", stats_line)
        scale = re.search(r"\*\*Scale\*\*\s*([^·]+)", stats_line)
        pivot = re.search(r"\*\*Pivot\*\*\s*(.+)$", stats_line)
        rig_line = lines.get("Rig", "")
        rig_line_full = next((ln for ln in sec.split("\n") if ln.startswith("- **Rig**")), "")
        rig = None
        if rig_line_full and not re.search(r"\*\*Rig\*\*:\s*none", rig_line_full, re.I):
            rl = rig_line_full.lower()
            head_rl = rl.split("anims")[0]
            rig = "biped" if "biped" in head_rl else "quadruped"
        anims = []
        am = re.search(r"\*\*Anims\*\*:\s*([^.(]+)", rig_line_full)
        if am:
            anims = [a.strip() for a in am.group(1).split(",") if a.strip()]
        ue = next((ln for ln in sec.split("\n") if ln.startswith("- **UE import**")), "")
        mass = re.search(r"mass\s*\**\s*([\d.]+)\s*kg", ue, re.I)
        sockets = _sockets(ue.split("Sockets:", 1)[1]) if "Sockets:" in ue else []
        neg_item = _code(lines.get("Negative", "")) or ""
        neg = neg_item.replace("[SHARED]", shared_neg)
        prompts = {k: v for k, v in lines.items() if k.startswith("Prompt")}
        base = {
            "priority": pri, "rig": rig, "anims": anims if rig else [],
            "face_limit": int(_num(tris.group(1))) if tris else None,
            "scale_cm": _scale(scale.group(1)) if scale else None,
            "pivot": pivot.group(1).strip() if pivot else "",
            "mass_kg": float(mass.group(1)) if mass else None,
            "collision": _collision(ue), "sockets": sockets,
            "negative_prompt": neg, "category": _category(sid, rig),
        }
        if len(prompts) == 1:
            p = _code(list(prompts.values())[0])
            items.append(dict(id=sid, prompt=p.replace("[PREFIX]", prefix), **base))
        else:  # SM_Net (+ SM_Net_Bag)
            for key, val in prompts.items():
                sub = "SM_Net_Bag" if "bag" in key.lower() else sid
                it = dict(id=sub, prompt=_code(val).replace("[PREFIX]", prefix), **base)
                if sub == "SM_Net_Bag":
                    it.update(face_limit=800, collision="sphere", mass_kg=1.0,
                              sockets=["bag_attach"], pivot="knot",
                              scale_cm={"raw": "bag (unspecified; ~50 cm hoop)", "max_dim_cm": 50.0})
                else:
                    it.update(face_limit=3000, collision="none", mass_kg=None,
                              sockets=[s for s in sockets if s != "bag_attach"] + ["bag_attach"])
                items.append(it)
    # P2 cosmetics table
    for m in re.finditer(r"\n\| (\d+) \| (SM_\w+) \| `(\w+)`[^|]*\| `([^`]+)`", text):
        sid = m.group(2)
        items.append(dict(id=sid, priority="P2", rig=None, anims=[], face_limit=1500,
                          scale_cm={"raw": "head ~25 cm reference", "max_dim_cm": None},
                          pivot="socket attach point", mass_kg=None, collision="none",
                          sockets=[], attach_socket=m.group(3), category="Cosmetics",
                          negative_prompt=shared_neg,
                          prompt=prefix + ", " + m.group(4)))
    return {"source": os.path.relpath(path, ROOT), "model": MODEL,
            "style_prefix": prefix, "shared_negative": shared_neg, "items": items}


def _category(sid, rig):
    if sid.startswith("SK_"):
        return "Characters" if sid in ("SK_Landlord", "SK_Scarecrow_MrStitches") else "Critters"
    if sid in ("SM_MarketStall", "SM_MarketPenGate"):
        return "Market"
    return "Props"


# ---------------------------------------------------------------- state
def load_state():
    if os.path.exists(STATE):
        return json.load(open(STATE))
    return {"items": {}, "credits_estimated": 0, "credits_reported": None}


def save_state(st):
    tmp = STATE + ".tmp"
    json.dump(st, open(tmp, "w"), indent=2)
    os.replace(tmp, STATE)  # atomic


# ---------------------------------------------------------------- http
class Api:
    def __init__(self, key):
        self._key = key  # held in memory only

    def _req(self, method, path, body=None):
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(API + path, data=data, method=method, headers={
            "Authorization": "Bearer " + self._key, "Content-Type": "application/json"})
        for attempt in range(5):
            try:
                with urllib.request.urlopen(req, timeout=60) as r:
                    return json.loads(r.read().decode())
            except urllib.error.HTTPError as e:
                msg = e.read().decode(errors="replace")[:300]
                if e.code in (429, 500, 502, 503, 504) and attempt < 4:
                    time.sleep(2 ** attempt * 2)
                    continue
                raise RuntimeError("HTTP %d on %s %s: %s" % (e.code, method, path, msg))
            except urllib.error.URLError as e:
                if attempt < 4:
                    time.sleep(2 ** attempt * 2)
                    continue
                raise RuntimeError("network error on %s: %s" % (path, e.reason))

    def submit(self, path, body):
        r = self._req("POST", path, body)
        d = r.get("data", r)
        tid = d.get("task_id") or d.get("id")
        if not tid:
            raise RuntimeError("no task_id in response: %s" % json.dumps(r)[:300])
        return tid

    def poll(self, tid, timeout=900):
        delay, t0 = 3, time.time()
        while True:
            d = self._req("GET", "/tasks/" + tid).get("data", {})
            st = d.get("status")
            if st in TERMINAL:
                return d
            if time.time() - t0 > timeout:
                return {"status": "timeout", "task_id": tid}
            print("    %s %s %s%%" % (tid[:12], st, d.get("progress", "?")), flush=True)
            time.sleep(delay)
            delay = min(delay * 1.5, 20)


def download(url, dest):
    tmp = dest + ".part"
    with urllib.request.urlopen(url, timeout=120) as r, open(tmp, "wb") as f:
        while True:
            chunk = r.read(1 << 16)
            if not chunk:
                break
            f.write(chunk)
    os.replace(tmp, dest)


# ---------------------------------------------------------------- pipeline
def text_body(it):
    body = {"prompt": it["prompt"], "model": MODEL,
            "negative_prompt": it["negative_prompt"],
            # Spec section 0: HD texture, PBR off (albedo-only), retopo to target.
            "texture": True, "pbr": False, "texture_quality": "detailed"}
    if it.get("face_limit"):
        body["face_limit"] = it["face_limit"]
    return body


def estimate(it, do_rig):
    c = COST["text_to_model"]
    if do_rig and it["rig"]:
        c += COST["rig"] + COST["anim"] * len(it["anims"])
    return c


def run_item(api, it, st, args):
    rec = st["items"].setdefault(it["id"], {"rolls": [], "status": "pending"})
    if rec["status"] in ("generated", "rigged") and not args.reroll:
        print("  skip %s (already %s)" % (it["id"], rec["status"]))
        return
    if len(rec["rolls"]) >= MAX_ROLLS:
        print("  %s hit %d rolls - switch to image-to-3D (spec section 5)" % (it["id"], MAX_ROLLS))
        return
    roll = {"t": time.strftime("%Y-%m-%dT%H:%M:%S"), "type": "text_to_model"}
    rec["rolls"].append(roll)
    pending = rec.get("pending_task")
    if pending:
        roll["task_id"] = pending
    else:
        try:
            roll["task_id"] = api.submit("/generation/text-to-model", text_body(it))
        except RuntimeError:
            rec["rolls"].pop()
            raise
        rec["pending_task"] = roll["task_id"]
        st["credits_estimated"] += COST["text_to_model"]
        save_state(st)
    print("  %s task %s" % (it["id"], roll["task_id"]))
    d = api.poll(roll["task_id"])
    roll["status"] = d.get("status")
    rec.pop("pending_task", None)
    for k in ("consumed_credit", "credits", "cost"):
        if k in d:
            roll["credits_reported"] = d[k]
    if roll["status"] != "success":
        rec["status"] = "failed"
        roll["error"] = d.get("error") or d.get("message")
        save_state(st)
        return
    out = d.get("output", {})
    n = len(rec["rolls"])
    od = os.path.join(OUT, it["id"])
    os.makedirs(od, exist_ok=True)
    if out.get("model_url"):
        download(out["model_url"], os.path.join(od, "%s_r%d.glb" % (it["id"], n)))
        download(out["model_url"], os.path.join(od, it["id"] + ".glb"))
    if out.get("rendered_image_url"):
        ext = ".webp" if ".webp" in out["rendered_image_url"].split("?")[0] else ".png"
        download(out["rendered_image_url"], os.path.join(od, "preview_r%d%s" % (n, ext)))
    rec["status"] = "generated"
    rec["model_task_id"] = roll["task_id"]
    save_state(st)
    if args.rig and it["rig"]:
        rig_item(api, it, rec, st, args)


def cli(args, key):
    """Run tripo-cli with --json; returns parsed JSON (last JSON line)."""
    exe = shutil.which(TRIPO_CLI)
    if not exe:
        raise RuntimeError("tripo-cli not found on PATH (npm i -g tripo-cli)")
    env = dict(os.environ, TRIPO_API_KEY=key)
    r = subprocess.run([exe] + args + ["--json", "--yes", "--quiet", "--no-open"],
                       capture_output=True, text=True, env=env, timeout=1800)
    lines = [l for l in r.stdout.splitlines() if l.strip().startswith("{")]
    if r.returncode != 0:
        raise RuntimeError("tripo %s failed (%d): %s" % (args[0], r.returncode,
                                                         (r.stderr or r.stdout)[-300:]))
    return json.loads(lines[-1]) if lines else {}


def balance(key):
    d = cli(["balance"], key)
    return d.get("balance")


def rig_item(api, it, rec, st, args):
    od = os.path.join(OUT, it["id"])
    rigtype = "avian" if it["id"] in ("SK_Chicken", "SK_Crow") else it["rig"]
    try:
        d = cli(["anim", "rig", rec["model_task_id"], "--rig-type", rigtype,
                 "--out-format", "fbx", "-o", od], api._key)
        st["credits_estimated"] += COST["rig"]
        rec["rig_task_id"] = d.get("task_id") or d.get("id")
        rec["rig_status"] = d.get("status", "success")
        save_state(st)
        if it["anims"] and rec["rig_task_id"]:
            presets = ["preset:" + a for a in it["anims"][:5]]
            cli(["anim", "retarget", rec["rig_task_id"], "--animation"] + presets +
                ["--animate-in-place", "--out-format", "fbx", "-o", od], api._key)
            st["credits_estimated"] += COST["anim"] * len(presets)
            rec["anims"] = it["anims"][:5]
        rec["status"] = "rigged"
    except (RuntimeError, ValueError, subprocess.TimeoutExpired) as e:
        rec["rig_note"] = "rig step failed: %s" % str(e)[:200]
        print("  rig skipped for %s: %s" % (it["id"], rec["rig_note"]))
    save_state(st)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--priority", choices=["P0", "P1", "P2"])
    ap.add_argument("--only", action="append", help="item id (repeatable)")
    ap.add_argument("--dry-run", action="store_true", help="write manifest, print plan, no API calls")
    ap.add_argument("--max-credits", type=int, default=400, help="abort before exceeding this estimate")
    ap.add_argument("--reroll", action="store_true", help="regenerate items already generated")
    ap.add_argument("--rig", action="store_true", default=True)
    ap.add_argument("--no-rig", dest="rig", action="store_false")
    args = ap.parse_args()

    man = parse_spec()
    tmp = MANIFEST + ".tmp"
    json.dump(man, open(tmp, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
    os.replace(tmp, MANIFEST)
    items = [i for i in man["items"]
             if (not args.priority or i["priority"] == args.priority)
             and (not args.only or i["id"] in args.only)]
    if args.only:
        missing = set(args.only) - {i["id"] for i in man["items"]}
        if missing:
            sys.exit("error: unknown id(s): %s" % ", ".join(sorted(missing)))
    total = sum(estimate(i, args.rig) for i in items)
    print("manifest: %s (%d items total)" % (os.path.relpath(MANIFEST, ROOT), len(man["items"])))
    print("%-20s %-3s %-9s %6s %8s %7s %-10s %s" % ("id", "pri", "rig", "faces", "max_cm", "kg", "collision", "sockets"))
    for i in items:
        print("%-20s %-3s %-9s %6s %8s %7s %-10s %s" % (
            i["id"], i["priority"], i["rig"] or "-", i["face_limit"],
            (i["scale_cm"] or {}).get("max_dim_cm"), i["mass_kg"] if i["mass_kg"] is not None else "-",
            i["collision"], ",".join(i["sockets"]) or "-"))
        problems = [k for k in ("prompt", "face_limit") if not i.get(k)]
        if "[PREFIX]" in i["prompt"] or "[SHARED]" in i["negative_prompt"]:
            problems.append("unexpanded placeholder")
        if problems:
            print("   WARN %s: %s" % (i["id"], ", ".join(problems)))
    print("estimated credits: %d (guard %d; estimates, not billed values)" % (total, args.max_credits))
    if args.dry_run:
        print("dry run - no API calls made")
        return
    key = os.environ.get("TRIPO_API_KEY")
    if not key:
        sys.exit("error: TRIPO_API_KEY not set (see docs/asset-pipeline.md). No calls made.")
    st = load_state()
    try:
        bal = balance(key)
    except (RuntimeError, ValueError) as e:
        sys.exit("error: balance pre-flight failed: %s" % e)
    print("balance: %s credits" % bal)
    if bal is None or bal < total:
        sys.exit("error: balance %s < estimated %d credits needed. Top up (tripo topup). No tasks submitted."
                 % (bal, total))
    if st["credits_estimated"] + total > args.max_credits and not args.reroll:
        sys.exit("error: estimate %d + spent %d exceeds --max-credits %d" %
                 (total, st["credits_estimated"], args.max_credits))
    api = Api(key)
    for it in items:
        if st["credits_estimated"] + estimate(it, args.rig) > args.max_credits:
            print("credit guard reached; stopping before %s" % it["id"])
            break
        try:
            run_item(api, it, st, args)
        except RuntimeError as e:
            st["items"].setdefault(it["id"], {})["last_error"] = str(e)[:300]
            save_state(st)
            print("  ERROR %s: %s" % (it["id"], e))
    print("done. estimated credits spent so far: %d" % st["credits_estimated"])


if __name__ == "__main__":
    main()
