#!/usr/bin/env python3
# build/ci/prerelease/cpm-report.py
# Prints a markdown report comparing:
# - Coven.* PackageReferences found in /samples
# - PackageVersion entries present in samples/Directory.Packages.props (overlay)
# - PackageVersion entries present in the root Directory.Packages.props (parent)
# Exits 0 (informational), but highlights any missing central versions that will trigger NU1010.

import argparse, os, re, glob, sys, xml.etree.ElementTree as ET
from datetime import datetime

def ns_of(tag):
    m = re.match(r'^\{([^}]+)\}', tag or '')
    return m.group(1) if m else None
def q(ns, name): return f'{{{ns}}}{name}' if ns else name

def load_props(path):
    if not os.path.exists(path): return (None, None, {}, False)
    tree = ET.parse(path); root = tree.getroot()
    ns = ns_of(root.tag)
    versions = {}
    cpm_on = False
    for pg in root.findall(q(ns,'PropertyGroup')) or []:
        val = pg.find(q(ns,'ManagePackageVersionsCentrally'))
        if val is not None and (val.text or '').strip().lower() == 'true':
            cpm_on = True
    for pv in root.findall(f".//{q(ns,'PackageVersion')}") or []:
        k = pv.attrib.get('Include') or pv.attrib.get('Update')
        v = pv.attrib.get('Version')
        if k and v: versions[k] = v
    return (tree, ns, versions, cpm_on)

def find_sample_projects():
    return sorted(glob.glob(os.path.join('samples','**','*.csproj'), recursive=True))

def read_refs(csproj):
    try:
        root = ET.parse(csproj).getroot()
    except ET.ParseError:
        return set()
    ns = ns_of(root.tag)
    ids = set()
    for pr in root.findall(f".//{q(ns,'PackageReference')}") or []:
        inc = pr.attrib.get('Include') or pr.attrib.get('Update')
        if inc: ids.add(inc)
    return ids

def print_hdr(s): 
    print(f"\n### {s}\n")

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--overlay", default=os.path.join("samples","Directory.Packages.props"))
    ap.add_argument("--parent",  default="Directory.Packages.props")
    ap.add_argument("--prefix",  default="Coven.")
    ap.add_argument("--out",     default="artifacts/cpm/cpm-report.md")
    args = ap.parse_args()

    os.makedirs(os.path.dirname(args.out), exist_ok=True)

    # Active files
    overlay_path = args.overlay
    parent_path  = args.parent

    o_tree, o_ns, o_versions, o_cpm = load_props(overlay_path)
    p_tree, p_ns, p_versions, p_cpm = load_props(parent_path)

    # Discover refs under samples
    projects = find_sample_projects()
    coven_refs = {}
    for p in projects:
        ids = {i for i in read_refs(p) if i.startswith(args.prefix)}
        if ids: coven_refs[p] = sorted(ids)

    missing = {}  # project -> [missing ids]

    # Build markdown
    out = []
    out.append("# Central Package Management Report (samples)\n")
    out.append(f"_Generated: {datetime.utcnow().isoformat()}Z_\n")

    out.append("#### Active CPM files")
    out.append(f"- Overlay (closest): `{overlay_path}` — **exists**: {os.path.exists(overlay_path)}; **CPM on**: {o_cpm}")
    out.append(f"- Parent (imported): `{parent_path}` — **exists**: {os.path.exists(parent_path)}; **CPM on**: {p_cpm}\n")

    # Summaries
    out.append("#### Known central versions")
    out.append(f"- Overlay defines: {len(o_versions)} IDs")
    out.append(f"- Parent defines:  {len(p_versions)} IDs\n")

    out.append("#### Per-project references (Coven.*)")
    if not coven_refs:
        out.append("_No Coven.* references found under /samples._\n")
    else:
        for proj, ids in coven_refs.items():
            out.append(f"- `{proj}`:\n")
            out.append("  | Package ID | In overlay? | In parent? | Version (overlay » parent) |\n"
                       "  |---|:--:|:--:|---|\n")
            miss = []
            for pid in ids:
                in_o = pid in o_versions
                in_p = pid in p_versions
                ver  = (o_versions.get(pid) or p_versions.get(pid) or "")
                out.append(f"  | `{pid}` | {'✓' if in_o else ''} | {'✓' if in_p else ''} | `{ver}` |")
                if not in_o and not in_p:
                    miss.append(pid)
            if miss:
                missing[proj] = miss
                out.append(f"  > **Missing central versions** → {', '.join('`'+m+'`' for m in miss)}\n")
            else:
                out.append("  > All referenced packages have central versions.\n")
            out.append("")

    if missing:
        out.append("\n### ❌ Missing central versions detected (will cause NU1010)\n")
        for proj, ids in missing.items():
            out.append(f"- `{proj}` → {', '.join('`'+m+'`' for m in ids)}")
    else:
        out.append("\n### ✅ No NU1010 risk detected (all refs have central versions)\n")

    report = "\n".join(out)
    print(report)
    with open(args.out, "w", encoding="utf-8") as f: f.write(report)

if __name__ == "__main__":
    main()
