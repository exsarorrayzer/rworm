import sys
import os
import argparse
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from core.server import RWORMServer

def main():
    parser = argparse.ArgumentParser(description="RWORM Server")
    parser.add_argument("--host", default="0.0.0.0", help="Bind host (default: 0.0.0.0)")
    parser.add_argument("--port", type=int, default=3333, help="Bind port (default: 3333)")
    args = parser.parse_args()

    print("=" * 50)
    print("  RWORM Server v1.0")
    print("  Remote Access Administration Tool")
    print("=" * 50)
    print(f"[*] Host: {args.host}  Port: {args.port}")
    print()

    server = RWORMServer(host=args.host, port=args.port)

    try:
        server.start()
    except KeyboardInterrupt:
        print("\n[*] Shutting down...")
        server.stop()

if __name__ == "__main__":
    main()