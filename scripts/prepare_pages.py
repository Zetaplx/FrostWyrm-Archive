#!/usr/bin/env python3
"""Prepare published Blazor files for GitHub Pages without disabling PWA integrity checks."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import re


def prepare(directory: Path, base_path: str) -> None:
    if not re.fullmatch(r'/(?:[A-Za-z0-9._~-]+/)*', base_path):
        raise ValueError('base path must start/end with / and contain only URL-safe path segments')
    if any(part in ('.', '..') for part in base_path.split('/')):
        raise ValueError('base path must not contain dot segments')
    manifest_path = directory / 'service-worker-assets.js'
    raw = manifest_path.read_text(encoding='utf-8-sig').strip()
    prefix = 'self.assetsManifest = '
    if not raw.startswith(prefix) or not raw.endswith(';'):
        raise ValueError('expected a published .NET service-worker-assets.js manifest')
    manifest = json.loads(raw[len(prefix):-1])
    index = directory / 'index.html'
    html, changed = re.subn(r'<base\s+href="[^"]*"\s*/?>', f'<base href="{base_path}" />', index.read_text(encoding='utf-8-sig'))
    if changed != 1:
        raise ValueError('expected exactly one base href in the published index.html')
    index.write_text(html, encoding='utf-8')
    (directory / '404.html').write_text(html, encoding='utf-8')
    (directory / '.nojekyll').touch()

    def digest(path: Path) -> str:
        return 'sha256-' + base64.b64encode(hashlib.sha256(path.read_bytes()).digest()).decode('ascii')

    assets = {entry['url']: entry for entry in manifest['assets']}
    for name in ('index.html', '404.html'):
        assets[name] = {'url': name, 'hash': digest(directory / name)}
    manifest['assets'] = [assets[name] for name in sorted(assets)]
    manifest['version'] = hashlib.sha256(json.dumps(manifest['assets'], sort_keys=True).encode()).hexdigest()[:16]
    manifest_path.write_text(prefix + json.dumps(manifest, indent=2) + ';\n', encoding='utf-8')
    # Do not leave compressed copies containing the pre-rewrite base URL or hashes.
    for name in ('index.html', '404.html', 'service-worker-assets.js'):
        for suffix in ('.br', '.gz'):
            (directory / (name + suffix)).unlink(missing_ok=True)
    # Verify actual artifact bytes, not merely the manifest's syntax.
    for entry in manifest['assets']:
        if digest(directory / entry['url']) != entry['hash']:
            raise ValueError(f"PWA integrity mismatch: {entry['url']}")
    print(f'GitHub Pages base {base_path}: {len(assets)} asset hashes verified; direct-route fallback ready.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path, help='published wwwroot, not the source directory')
    parser.add_argument('--base-path', required=True, help='for example /FrostWyrm-Archive/')
    args = parser.parse_args()
    prepare(args.directory.resolve(), args.base_path)
