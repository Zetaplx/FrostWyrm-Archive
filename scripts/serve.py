#!/usr/bin/env python3
"""Serve a published Blazor/PWA directory locally, including SPA navigation fallback."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

class Handler(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, '.wasm': 'application/wasm', '.webmanifest': 'application/manifest+json'}

    def send_head(self):
        path = Path(self.translate_path(urlsplit(self.path).path))
        if not path.exists() and '.' not in path.name:
            self.path = '/index.html'
        return super().send_head()

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--port', type=int, default=5011)
    args = parser.parse_args()
    directory = args.directory.resolve()
    if not (directory / 'index.html').is_file():
        parser.error('directory must contain the published index.html')
    server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(Handler, directory=str(directory)))
    print(f'Serving {directory} on port {args.port}', flush=True)
    server.serve_forever()
