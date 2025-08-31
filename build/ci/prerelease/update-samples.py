#!/usr/bin/env python3
# build/ci/prerelease/update-samples.py
# Writes samples/Directory.Packages.props so that every Coven.* PackageReference
# in /samples has a matching <PackageVersion Include="...">.
# - Changed IDs get the current prerelease (passed in).
# - Unchanged IDs get the stable base read from Build/VERSION (strip any -pre tag).

import argparse, os, re, glob, xml.etree.ElementTree as ET

def ns_of(tag):
    m = re.match(r'^\{([^}]+)\}', tag or '')
    return m.group(1) if m else None
def q(ns, name): return f'{{{ns}}}{name}' if ns else name

def sample_projects():
    return glob.glob(os.path.join('samples','**','*.csproj'), recursive=True)

def coven_refs(csproj, prefix):
    try:
        root = ET.parse(csproj).getroot()
    except ET.ParseError:
        return set()
    ns = ns_of(root.tag)
    out = set()
    for pr in root.findall(f".//{q(ns,'PackageReference')}") or []:
        inc = pr.attrib.get('Include') or pr.attrib.get('Update')
        if inc and inc.startswith(prefix): out.add(inc)
    return out

def read_stable_base():
    candidates = ['Build/VERSION', 'build/VERSION']
    for p in candidates:
        if os.path.exists(p):
            with open(p, 'r', encoding='utf-8') as f:
                base = f.read().strip()
                return base.split('-')[0]  # strip any prerelease
    raise SystemExit("VERSION file not found under Build/ or build/")

def ensure_overlay():
    path = os.path.join('samples', 'Directory.Packages.props')
    if os.path.exists(path):
        root = ET.parse(path).getroot()
    else:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        root = ET.Element("Project")
    ns = ns_of(root.tag)

    # Ensure CPM is on
    pg = next((n for n in root.findall(q(ns,'PropertyGroup')) or []), None)
    if pg is None: pg = ET.SubElement(root, q(ns,'PropertyGroup'))
    mpvc = pg.find(q(ns,'ManagePackageVersionsCentrally'))
    if mpvc is None: mpvc = ET.SubElement(pg, q(ns,'ManagePackageVersionsCentrally'))
    mpvc.text = "true"

    # Ensure import of parent (so non-Coven versions still flow down)
    has_import = any((e.tag == q(ns,'Import')) and ('GetPathOfFileAbove' in (e.attrib.get('Project',''))) for e in list(root))
    if not has_import:
        imp = ET.Element(q(ns,'Import'))
        imp.set('Project', "$([MSBuild]::GetPathOfFileAbove(Directory.Packages.props, $(MSBuildThisFileDirectory)..))")
        root.insert(0, imp)

    tree = ET.ElementTree(root)
    return path, tree, root, ns

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids", required=True, help="CSV of CHANGED package IDs (from detect-changed)")
    ap.add_argument("--version", required=True, help="Prerelease version for CHANGED IDs")
    ap.add_argument("--prefix", default="Coven.", help="Package ID prefix to manage")
    args = ap.parse_args()

    changed = {s for s in (args.ids or "").split(",") if s}
    prerelease = args.version
    stable_base = read_stable_base()

    # Gather every Coven.* referenced under /samples
    needed = set()
    for proj in sample_projects():
        needed |= coven_refs(proj, args.prefix)

    # Nothing to do? still ensure the overlay skeleton exists
    path, tree, root, ns = ensure_overlay()

    # Find/create the ItemGroup for PackageVersion entries
    ig = next((n for n in root.findall(q(ns,'ItemGroup')) or []), None)
    if ig is None: ig = ET.SubElement(root, q(ns,'ItemGroup'))

    # Drop any existing PackageVersion for the managed prefix (clean slate)
    for pv in list(ig.findall(q(ns,'PackageVersion')) or []):
        pid = pv.attrib.get('Include') or pv.attrib.get('Update')
        if pid and pid.startswith(args.prefix):
            ig.remove(pv)

    # Emit Include entries for all needed IDs (changed -> prerelease; unchanged -> stable)
    for pid in sorted(needed):
        ver = prerelease if pid in changed else stable_base
        ET.SubElement(ig, q(ns,'PackageVersion'), {'Include': pid, 'Version': ver})

    tree.write(path, encoding='utf-8', xml_declaration=True)
    # Optional: print a summary mapping for logs
    print(f"Updated {path} with {len(needed)} PackageVersion Include entries.")
    for pid in sorted(needed):
        v = prerelease if pid in changed else stable_base
        print(f"  - {pid} -> {v}")

if __name__ == "__main__":
    main()
