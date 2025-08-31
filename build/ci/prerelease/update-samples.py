#!/usr/bin/env python3
# build/ci/prerelease/update-samples.py
import argparse, os, re, xml.etree.ElementTree as ET

def ns_of(tag):
    m = re.match(r'^\{([^}]+)\}', tag or '')
    return m.group(1) if m else None

def q(ns, name): return f'{{{ns}}}{name}' if ns else name

ap = argparse.ArgumentParser()
ap.add_argument("--ids", required=True, help="CSV of package IDs")
ap.add_argument("--version", required=True)
ap.add_argument("--samples-props", default=os.path.join("samples","Directory.Packages.props"))
args = ap.parse_args()

ids = [s for s in args.ids.split(",") if s]
ver = args.version
path = args.samples_props
os.makedirs(os.path.dirname(path), exist_ok=True)

if os.path.exists(path):
    tree = ET.parse(path); root = tree.getroot()
    ns = ns_of(root.tag)
else:
    root = ET.Element("Project"); ns = None
    tree = ET.ElementTree(root)

# Ensure Import of parent CPM at the top
has_import = any((e.tag == q(ns,'Import')) and ('GetPathOfFileAbove' in (e.attrib.get('Project',''))) for e in list(root))
if not has_import:
    imp = ET.Element(q(ns,'Import'))
    imp.set('Project',"$([MSBuild]::GetPathOfFileAbove(Directory.Packages.props, $(MSBuildThisFileDirectory)..))")
    root.insert(0, imp)

# Find or create ItemGroup
ig = None
for e in root.findall(q(ns,'ItemGroup')): ig = e; break
if ig is None: ig = ET.SubElement(root, q(ns,'ItemGroup'))

# Upsert PackageVersion Update="<id>" Version="<ver>"
by_id = {}
for pv in ig.findall(q(ns,'PackageVersion')):
    k = pv.attrib.get('Update') or pv.attrib.get('Include')
    if k: by_id[k] = pv

for pid in ids:
    pv = by_id.get(pid)
    if pv is None:
        pv = ET.SubElement(ig, q(ns,'PackageVersion'), {'Update': pid, 'Version': ver})
    else:
        pv.attrib.pop('Include', None)
        pv.set('Update', pid)
        pv.set('Version', ver)

tree.write(path, encoding="utf-8", xml_declaration=True)
