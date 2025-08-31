#!/usr/bin/env python3
# build/ci/prerelease/update-samples.py
import argparse, os, re, json, glob, xml.etree.ElementTree as ET
from urllib.request import urlopen, Request
from urllib.error import URLError, HTTPError

def ns_of(tag):
    m = re.match(r'^\{([^}]+)\}', tag or '')
    return m.group(1) if m else None

def q(ns, name): return f'{{{ns}}}{name}' if ns else name

def find_sample_projects():
    return glob.glob(os.path.join('samples', '**', '*.csproj'), recursive=True)

def read_coven_refs(csproj_path, prefix):
    try:
        tree = ET.parse(csproj_path); root = tree.getroot()
    except ET.ParseError:
        return set()
    ns = ns_of(root.tag)
    ids = set()
    for pr in root.findall(f".//{q(ns,'PackageReference')}"):
        inc = pr.attrib.get('Include') or pr.attrib.get('Update')
        if inc and inc.startswith(prefix):
            ids.add(inc)
    return ids

def read_defined_ids(props_path):
    """Return set of IDs that have a PackageVersion entry in props_path."""
    if not os.path.exists(props_path): return set()
    tree = ET.parse(props_path); root = tree.getroot()
    ns = ns_of(root.tag)
    ids = set()
    for pv in root.findall(f".//{q(ns,'PackageVersion')}"):
        inc = pv.attrib.get('Include')
        upd = pv.attrib.get('Update')
        if inc: ids.add(inc)
        if upd: ids.add(upd)
    return ids

def nuget_latest_version(id_lower: str, prefer_stable=True):
    url = f"https://api.nuget.org/v3-flatcontainer/{id_lower}/index.json"
    try:
        req = Request(url, headers={"User-Agent": "coven-prerelease-bot/1.0"})
        with urlopen(req, timeout=10) as r:
            data = json.loads(r.read().decode('utf-8'))
    except (HTTPError, URLError, TimeoutError):
        return None
    versions = data.get('versions') or []
    if prefer_stable:
        stables = [v for v in versions if '-' not in v]
        if stables: return stables[-1]
    return versions[-1] if versions else None

def ensure_parent_import_first(root):
    ns = ns_of(root.tag)
    # Look for an Import with GetPathOfFileAbove
    has = any(
        (e.tag == q(ns,'Import')) and ('GetPathOfFileAbove' in (e.attrib.get('Project','')))
        for e in list(root)
    )
    if not has:
        imp = ET.Element(q(ns,'Import'))
        # Fully quoted args for GetPathOfFileAbove
        imp.set('Project', "$([MSBuild]::GetPathOfFileAbove('Directory.Packages.props','$(MSBuildThisFileDirectory)..'))")
        root.insert(0, imp)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids", required=True, help="CSV of CHANGED package IDs (from detect-changed)")
    ap.add_argument("--version", required=True, help="Prerelease semver for changed IDs")
    ap.add_argument("--prefix", default="Coven.", help="Internal package ID prefix to manage in samples")
    ap.add_argument("--props", default=os.path.join("samples","Directory.Packages.props"))
    ap.add_argument("--allow-prerelease-fallback", action="store_true",
                    help="If no stable exists on nuget.org, allow prerelease fallback")
    ap.add_argument("--root-props", default="Directory.Packages.props",
                    help="Path to parent CPM file to inspect (default: repository root)")
    args = ap.parse_args()

    changed = {s for s in (args.ids or "").split(",") if s}
    prerelease = args.version
    prefix = args.prefix

    # Discover all Coven.* package IDs referenced by samples
    needed = set()
    for p in find_sample_projects():
        needed |= read_coven_refs(p, prefix)

    # If nothing to do, exit cleanly
    if not needed and not changed:
        return

    # Build the props tree (create if missing)
    if os.path.exists(args.props):
        tree = ET.parse(args.props); root = tree.getroot()
    else:
        root = ET.Element("Project")
        tree = ET.ElementTree(root)

    # Import parent first so its items exist before we emit Update
    ensure_parent_import_first(root)

    # Find or create ItemGroup
    ns = ns_of(root.tag)
    ig = None
    for e in root.findall(q(ns,'ItemGroup')): ig = e; break
    if ig is None: ig = ET.SubElement(root, q(ns,'ItemGroup'))

    # What IDs are already defined upstream / locally?
    parent_defined = read_defined_ids(args.root_props)
    overlay_defined = read_defined_ids(args.props)

    # Existing nodes in overlay by id (either Include or Update)
    existing = {}
    for pv in ig.findall(q(ns,'PackageVersion')):
        k = pv.attrib.get('Update') or pv.attrib.get('Include')
        if k: existing[k] = pv

    def desired_version(pid):
        if pid in changed:
            return prerelease
        # Keep overlay version if present
        node = existing.get(pid)
        if node is not None and node.attrib.get('Version'):
            return node.attrib['Version']
        # Otherwise, try nuget.org (prefer stable)
        v = nuget_latest_version(pid.lower(), prefer_stable=True)
        if not v and args.allow_prerelease_fallback:
            v = nuget_latest_version(pid.lower(), prefer_stable=False)
        return v

    # Upsert entries with Include-or-Update semantics
    for pid in sorted(needed):
        ver = desired_version(pid)
        if not ver:
            # Leave unresolved; restore will surface it (rare: brand new package id with no published version)
            continue

        node = existing.get(pid)
        if node is None:
            # Choose Update if parent/overlay already define; otherwise Include
            use_update = (pid in parent_defined) or (pid in overlay_defined)
            if use_update:
                node = ET.SubElement(ig, q(ns,'PackageVersion'), {'Update': pid, 'Version': ver})
            else:
                node = ET.SubElement(ig, q(ns,'PackageVersion'), {'Include': pid, 'Version': ver})
            existing[pid] = node
        else:
            # Normalize and set Version
            if 'Include' in node.attrib and pid in parent_defined:
                # Prefer Update when parent already has it (override instead of duplicate)
                node.attrib.pop('Include', None)
                node.set('Update', pid)
            node.set('Version', ver)

    tree.write(args.props, encoding="utf-8", xml_declaration=True)

if __name__ == "__main__":
    main()
