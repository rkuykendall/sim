#!/usr/bin/env python3
"""Serve the web build locally with the COOP/COEP headers Godot requires."""

import http.server
import os
import sys

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8080
WEB_DIR = os.path.join(os.path.dirname(__file__), "builds", "paint-town-web")


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=WEB_DIR, **kwargs)

    def end_headers(self):
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        super().end_headers()

    def log_message(self, format, *args):
        pass  # suppress per-request noise


print(f"Serving {WEB_DIR}")
print(f"Open http://localhost:{PORT}")
print("Ctrl+C to stop")
http.server.HTTPServer(("", PORT), Handler).serve_forever()
