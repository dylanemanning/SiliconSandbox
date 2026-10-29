#!/usr/bin/env python3
"""Generate an SPDX 2.3 project-dependency SBOM from Unity's package lockfile.
Run from any directory after Unity has resolved packages.
"""
import argparse
import datetime as dt
import hashlib
import json
import re
import uuid
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[2]


def license_info(cache_dir, package_name, extracted):
    """Return SPDX license expression or a LicenseRef backed by cached license text."""
    folders = sorted(cache_dir.glob(package_name + '@*')) if cache_dir.is_dir() else []
    if not folders:
        return 'NOASSERTION', 'No cached package directory available'
    folder = folders[0]
    metadata = folder / 'package.json'
    meta = {}
    if metadata.is_file():
        try:
            meta = json.loads(metadata.read_text(encoding='utf-8-sig'))
        except (ValueError, UnicodeError):
            pass
    lic = meta.get('license')
    # Accept only an SPDX-like identifier or expression, not prose or a URL.
    if isinstance(lic, str) and re.fullmatch(r'[A-Za-z0-9.+-]+', lic) and not lic.lower().startswith('http'):
        return lic, f'License identifier from {metadata.as_posix()}'
    candidates = sorted(p for p in folder.iterdir() if p.is_file() and re.match(r'^(LICENSE|LICENCE|COPYING|NOTICE)(\.|$)', p.name, re.I))
    for file in candidates:
        try:
            content = file.read_text(encoding='utf-8-sig')
        except (UnicodeError, OSError):
            continue
        if not content.strip():
            continue
        digest = hashlib.sha256(content.encode('utf-8')).hexdigest()[:16]
        ref = f'LicenseRef-Unity-{digest}'
        extracted.setdefault(ref, {'licenseId': ref, 'extractedText': content,
                                   'name': f'License text supplied with {package_name}'})
        return ref, f'License text from {file.as_posix()} (not classified as an SPDX standard license)'
    url = meta.get('licensesUrl') or meta.get('licenseUrl')
    if url:
        return 'NOASSERTION', f'License URL in package metadata: {url} (not verified)'
    return 'NOASSERTION', 'No license identifier or license text found in cached package'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        '--root',
        type=Path,
        default=PROJECT_ROOT,
        help='Unity project root (relative paths are resolved from the project containing this script).',
    )
    parser.add_argument('--output', type=Path, default=Path('doc/SBOM.json'))
    args = parser.parse_args()
    root = args.root if args.root.is_absolute() else PROJECT_ROOT / args.root
    root = root.resolve()
    lock = json.loads((root / 'Packages/packages-lock.json').read_text(encoding='utf-8-sig'))['dependencies']
    direct = json.loads((root / 'Packages/manifest.json').read_text(encoding='utf-8-sig'))['dependencies']
    extracted = {}
    packages = []
    relationships = []
    doc_id = 'SPDXRef-DOCUMENT'
    project_id = 'SPDXRef-SiliconSandbox'
    relationships.append({'spdxElementId': doc_id, 'relatedSpdxElement': project_id, 'relationshipType': 'DESCRIBES'})
    for name, entry in sorted(lock.items()):
        pkg_id = 'SPDXRef-Package-' + re.sub(r'[^A-Za-z0-9.-]', '-', name)
        lic, evidence = license_info(root / 'Library/PackageCache', name, extracted)
        source = entry.get('source', 'unknown')
        pkg = {
            'SPDXID': pkg_id,
            'name': name,
            'versionInfo': str(entry.get('version', 'NOASSERTION')),
            'downloadLocation': entry.get('url', 'NOASSERTION') if source == 'registry' else 'NOASSERTION',
            'filesAnalyzed': False,
            'licenseConcluded': 'NOASSERTION',
            'licenseDeclared': lic,
            'copyrightText': 'NOASSERTION',
            'supplier': 'Organization: Unity Technologies' if name.startswith('com.unity.') else 'NOASSERTION',
            'comment': f'Unity source={source}; depth={entry.get("depth", "unknown")}; {evidence}',
        }
        packages.append(pkg)
        relationships.append({'spdxElementId': project_id, 'relatedSpdxElement': pkg_id, 'relationshipType': 'DEPENDS_ON'} if name in direct else {'spdxElementId': doc_id, 'relatedSpdxElement': pkg_id, 'relationshipType': 'DESCRIBES'})
        for dep in entry.get('dependencies', {}):
            if dep in lock:
                relationships.append({'spdxElementId': pkg_id,
                                      'relatedSpdxElement': 'SPDXRef-Package-' + re.sub(r'[^A-Za-z0-9.-]', '-', dep),
                                      'relationshipType': 'DEPENDS_ON'})
    packages.insert(0, {'SPDXID': project_id, 'name': 'Silicon Sandbox (Unity project dependency inventory)',
                        'downloadLocation': 'NOASSERTION', 'filesAnalyzed': False,
                        'licenseConcluded': 'NOASSERTION', 'licenseDeclared': 'NOASSERTION',
                        'copyrightText': 'NOASSERTION',
                        'comment': 'Project dependency SBOM, not an inventory of files included in the shipped Windows player.'})
    sbom = {
        'spdxVersion': 'SPDX-2.3', 'dataLicense': 'CC0-1.0', 'SPDXID': doc_id,
        'name': 'Silicon Sandbox - Unity Package Dependency SBOM',
        'documentNamespace': 'https://example.invalid/spdxdocs/silicon-sandbox-' + str(uuid.uuid4()),
        'creationInfo': {'created': dt.datetime.now(dt.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),
                         'creators': ['Tool: generate_unity_sbom.py']},
        'packages': packages, 'relationships': relationships,
    }
    if extracted:
        sbom['hasExtractedLicensingInfos'] = list(extracted.values())
    output = args.output if args.output.is_absolute() else root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(sbom, indent=2) + '\n', encoding='utf-8')
    print(f'Wrote {output}: {len(lock)} Unity packages, {sum(p["licenseDeclared"] != "NOASSERTION" for p in packages[1:])} with license evidence')

if __name__ == '__main__':
    main()
