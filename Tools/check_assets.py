"""Check local GUID references, asset metadata, and the two runtime PNGs without importing Unity."""
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[1]

def main():
    assets = ROOT / "Assets"
    known = {}
    errors = []
    for p in assets.rglob("*.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})$", p.read_text(encoding="utf-8"), re.M)
        if not match:
            errors.append("Missing GUID: " + str(p.relative_to(ROOT)))
            continue
        guid = match.group(1)
        if guid in known:
            errors.append("Duplicate GUID: " + str(p.relative_to(ROOT)))
        known[guid] = p
    for p in assets.rglob("*"):
        if p.suffix != ".meta" and not Path(str(p) + ".meta").exists():
            errors.append("Missing meta: " + str(p.relative_to(ROOT)))
        if p.is_file() and p.suffix in (".unity", ".mat", ".prefab", ".asset"):
            for guid in re.findall(r"guid: ([0-9a-f]{32})", p.read_text(encoding="utf-8")):
                if not guid.startswith("0000000000000000") and guid not in known:
                    errors.append("Unresolved GUID in " + str(p.relative_to(ROOT)) + ": " + guid)
    for name in ("long-mode.png", "short-mode.png"):
        p = ROOT / "docs" / "images" / name
        if not p.exists():
            errors.append("Missing screenshot: " + name)
            continue
        data = p.read_bytes()
        if data[:8] != b"\x89PNG\r\n\x1a\n":
            errors.append("Not PNG: " + name)
            continue
        offset = 8
        chunks = []
        while offset < len(data):
            size = struct.unpack_from(">I", data, offset)[0]
            kind = data[offset + 4:offset + 8].decode("ascii")
            chunks.append(kind)
            if kind in ("tEXt", "iTXt", "zTXt", "eXIf"):
                errors.append("Review embedded image metadata: " + name)
            offset += size + 12
        width, height = struct.unpack_from(">II", data, 16)
        print(name, str(width) + "x" + str(height), "chunks=" + ",".join(sorted(set(chunks))))
    for error in errors:
        print(error)
    print("Asset GUIDs:", len(known), "Errors:", len(errors))
    if errors:
        raise SystemExit(1)

if __name__ == "__main__":
    main()
