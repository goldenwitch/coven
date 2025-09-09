#!/usr/bin/env python3
# Regenerate top "Code" section of INDEX.md from the solution file.
# Keeps the Architecture Guide section and below unchanged.

import argparse, os, re

ARCH_ANCHOR = "# Architecture Guide"

def parse_sln_projects(sln_path):
    projects = []
    with open(sln_path, 'r', encoding='utf-8', errors='ignore') as f:
        for line in f:
            m = re.match(r'^Project\([^)]*\) = "([^"]+)", "([^"]+)",', line)
            if not m:
                continue
            name, relpath = m.group(1), m.group(2)
            projects.append((name, relpath.replace('\\', '/')))
    return projects

def link_for(relpath):
    # Convert solution relative path to repo absolute markdown link target directory
    if relpath.startswith('../samples/'):
        parts = relpath.split('/')
        return f"/samples/{parts[2]}/"
    if relpath.startswith('../Toys/'):
        parts = relpath.split('/')
        return f"/Toys/{parts[2]}/"
    # default: under src
    parts = relpath.split('/')
    return f"/src/{parts[0]}/"

def categorize(projects):
    cats = {
        'core': [],
        'spell': [],
        'spell_impl': [],
        'spell_impl_tests': [],
        'chat': [],
        'analyzers': [],
        'durables': [],
        'sophia': [],
        'samples': [],
        'toys': [],
    }
    for (name, rel) in projects:
        link = link_for(rel)
        if rel.startswith('../samples/'):
            cats['samples'].append((name, link))
        elif rel.startswith('../Toys/'):
            cats['toys'].append((name, link))
        elif name.startswith('Coven.Core'):
            cats['core'].append((name, link))
        elif name.startswith('Coven.Spellcasting.Agents.Codex.McpShim'):
            cats['spell_impl'].append((name, link))
        elif name.startswith('Coven.Spellcasting.Agents.Codex.Tests'):
            cats['spell_impl_tests'].append((name, link))
        elif name.startswith('Coven.Spellcasting.Agents.Codex'):
            cats['spell_impl'].append((name, link))
        elif name.startswith('Coven.Spellcasting'):
            cats['spell'].append((name, link))
        elif name.startswith('Coven.Chat'):
            cats['chat'].append((name, link))
        elif name.startswith('Coven.Analyzers'):
            cats['analyzers'].append((name, link))
        elif name.startswith('Coven.Durables'):
            cats['durables'].append((name, link))
        elif name.startswith('Coven.Sophia'):
            cats['sophia'].append((name, link))
    # de-dup by link
    for k,v in cats.items():
        seen = set(); dedup = []
        for n,l in sorted(v, key=lambda t: t[0].lower()):
            if l in seen: continue
            seen.add(l); dedup.append((n,l))
        cats[k] = dedup
    return cats

def render_code_section(cats):
    lines = []
    lines.append('# Code')
    lines.append('')
    lines.append('Project overview: see [README](/README.md).')
    lines.append('')
    # Core
    if cats['core']:
        lines.append('## Coven Engine (Coven.Core)')
        for _, link in cats['core']:
            lines.append(f"- [{os.path.basename(link.strip('/'))}]({link})")
        lines.append('')
    # Spellcasting
    if cats['spell']:
        lines.append('## Coven with Agents (Coven.Spellcasting)')
        for _, link in cats['spell']:
            lines.append(f"- [{os.path.basename(link.strip('/'))}]({link})")
        lines.append('')
        if cats['spell_impl']:
            lines.append('### Agent implementations')
            for _, link in cats['spell_impl']:
                lines.append(f"- [{os.path.basename(link.strip('/'))}]({link})")
            lines.append('')
        if cats['spell_impl_tests']:
            lines.append('### Agent implementation tests')
            for _, link in cats['spell_impl_tests']:
                lines.append(f"- [{os.path.basename(link.strip('/'))}]({link})")
            lines.append('')
    # Chat
    if cats['chat']:
        lines.append('## Coven Infrastructure for Chat')
        # filter to only existing repos (sln assures existence)
        for _, link in cats['chat']:
            lines.append(f"- [{os.path.basename(link.strip('/'))}]({link})")
        lines.append('')
    # Analyzers
    if cats['analyzers']:
        lines.append('## Analyzers')
        for _, link in cats['analyzers']:
            lines.append(f"- [{os.path.basename(link.strip('/'))}]({link})")
        lines.append('')
    # Durables
    if cats['durables']:
        lines.append('## Durables')
        for _, link in cats['durables']:
            lines.append(f"- [{os.path.basename(link.strip('/'))}]({link})")
        lines.append('')
    # Sophia
    if cats['sophia']:
        lines.append('## Sophia')
        for _, link in cats['sophia']:
            lines.append(f"- [{os.path.basename(link.strip('/'))}]({link})")
        lines.append('')
    # Samples
    if cats['samples']:
        lines.append('## Samples')
        for _, link in cats['samples']:
            lines.append(f"- [{os.path.basename(link.strip('/'))}]({link})")
        lines.append('')
    # Toys
    if cats['toys']:
        lines.append('## Toys')
        for _, link in cats['toys']:
            lines.append(f"- [{os.path.basename(link.strip('/'))}]({link})")
        lines.append('')
    return '\n'.join(lines) + '\n'

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--sln', default='src/Coven.sln')
    ap.add_argument('--index', default='INDEX.md')
    args = ap.parse_args()

    projs = parse_sln_projects(args.sln)
    cats = categorize(projs)
    code = render_code_section(cats)

    # splice with existing Architecture Guide and below
    if os.path.exists(args.index):
        with open(args.index, 'r', encoding='utf-8') as f:
            content = f.read()
        split = content.split(ARCH_ANCHOR, 1)
        tail = (ARCH_ANCHOR + split[1]) if len(split) == 2 else ''
    else:
        tail = ''

    new_content = code + tail
    with open(args.index, 'w', encoding='utf-8') as f:
        f.write(new_content)
    print(f"Regenerated {args.index} from {args.sln}")

if __name__ == '__main__':
    main()

