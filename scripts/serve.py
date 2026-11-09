#!/usr/bin/env python3
"""Serve a published Blazor/PWA directory locally, including SPA navigation fallback."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

class Handler(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, '.wasm': 'application/wasm', '.webmanifest': 'application/manifest+json'}

    def __init__(self, *args, base_path='/', pages=False, **kwargs):
        self.base_path = base_path
        self.pages = pages
        super().__init__(*args, **kwargs)

    def translate_path(self, path):
        path = urlsplit(path).path
        if self.base_path != '/' and path.startswith(self.base_path):
            path = '/' + path[len(self.base_path):]
        return super().translate_path(path)

    def send_head(self):
        request_path = urlsplit(self.path).path
        if self.base_path != '/' and request_path == self.base_path.rstrip('/'):
            self.send_response(301)
            self.send_header('Location', self.base_path)
            self.end_headers()
            return None
        if not request_path.startswith(self.base_path):
            self.send_error(404)
            return None
        path = Path(self.translate_path(urlsplit(self.path).path))
        if not path.exists() and '.' not in path.name:
            if self.pages:
                fallback = Path(self.directory) / '404.html'
                if fallback.is_file():
                    self.send_response(404)
                    self.send_header('Content-Type', 'text/html; charset=utf-8')
                    self.send_header('Content-Length', str(fallback.stat().st_size))
                    self.end_headers()
                    return fallback.open('rb')
            self.path = self.base_path + 'index.html'
        return super().send_head()

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--port', type=int, default=5011)
    parser.add_argument('--base-path', default='/')
    parser.add_argument('--pages', action='store_true', help='serve direct routes as a GitHub Pages 404 shell')
    args = parser.parse_args()
    if not args.base_path.startswith('/') or not args.base_path.endswith('/'):
        parser.error('base path must start and end with /')
    directory = args.directory.resolve()
    if not (directory / 'index.html').is_file():
        parser.error('directory must contain the published index.html')
    server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(Handler, directory=str(directory), base_path=args.base_path, pages=args.pages))
    print(f'Serving {directory} on port {args.port}', flush=True)
    server.serve_forever()
