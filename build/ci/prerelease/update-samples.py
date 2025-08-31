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

def nuget_latest_version(id_lower: str, prefer_stable=True):
    """
    Query nuget flat container for available versions.
    Returns (version or None).
    """
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
        if stables:
            return stables[-1]
    return versions[-1] if versions else None

def existing_version_in_props(props_path, pkg_id):
    if not os.path.exists(props_path): return None
    tree = ET.parse(props_path); root = tree.getroot()
    ns = ns_of(root.tag)
    for pv in root.findall(f".//{q(ns,'PackageVersion')}"):
        inc = pv.attrib.get('Include')
        upd = pv.attrib.get('Update')
        if (inc == pkg_id) or (upd == pkg_id):
            return pv.attrib.get('Version')
    return None

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids", required=True, help="CSV of CHANGED package IDs (from detect-changed)")
    ap.add_argument("--version", required=True, help="Prerelease semver for changed IDs")
    ap.add_argument("--prefix", default="Coven.", help="Internal package ID prefix to manage in samples")
    ap.add_argument("--props", default=os.path.join("samples","Directory.Packages.props"))
    ap.add_argument("--allow-prerelease-fallback", action="store_true",
                    help="If no stable exists on nuget.org, allow prerelease fallback")
    args = ap.parse_args()

    changed = {s for s in (args.ids or "").split(",") if s}
    prerelease = args.version
    prefix = args.prefix

    # Discover all Coven.* package IDs referenced by samples
    needed = set()
    for p in find_sample_projects():
        needed |= read_coven_refs(p, prefix)

    if not needed and not changed:
        # Nothing to do
        return

    # Build the props tree (create if missing)
    if os.path.exists(args.props):
        tree = ET.parse(args.props); root = tree.getroot()
        ns = ns_of(root.tag)
    else:
        root = ET.Element("Project"); ns = None
        tree = ET.ElementTree(root)

    # Ensure import of parent CPM so external deps still come from root
    has_import = any((e.tag == q(ns,'Import')) and ('GetPathOfFileAbove' in (e.attrib.get('Project',''))) for e in list(root))
    if not has_import:
        imp = ET.Element(q(ns,'Import'))
        imp.set('Project',"$([MSBuild]::GetPathOfFileAbove(Directory.Packages.props, $(MSBuildThisFileDirectory)..))")
        root.insert(0, imp)

    # Find or create ItemGroup
    ig = None
    for e in root.findall(q(ns,'ItemGroup')): ig = e; break
    if ig is None: ig = ET.SubElement(root, q(ns,'ItemGroup'))

    # Upsert PackageVersion entries
    existing = {}
    for pv in ig.findall(q(ns,'PackageVersion')):
        k = pv.attrib.get('Update') or pv.attrib.get('Include')
        if k: existing[k] = pv

    made_change = False
    for pid in sorted(needed):
        desired = None
        if pid in changed:
            desired = prerelease
        else:
            # Keep existing value if already set
            desired = existing_version_in_props(args.props, pid)
            if not desired:
                resolved = nuget_latest_version(pid.lower(), prefer_stable=True)
                if not resolved and args.allow_prerelease_fallback:
                    resolved = nuget_latest_version(pid.lower(), prefer_stable=False)
                desired = resolved

        if not desired:
            # If we still don't have a version, skip this ID; restore will fail and CI will surface it.
            continue

        node = existing.get(pid)
        if node is None:
            node = ET.SubElement(ig, q(ns,'PackageVersion'), {'Update': pid, 'Version': desired})
            made_change = True
        else:
            # Normalize to Update + set Version
            if node.attrib.get('Include'):
                node.attrib.pop('Include', None)
                node.set('Update', pid)
            if node.attrib.get('Version') != desired:
                node.set('Version', desired); made_change = True

    tree.write(args.props, encoding="utf-8", xml_declaration=True)

    # Exit code communicates whether we changed anything (0 either way; CI checks git status)

if __name__ == "__main__":
    main()
