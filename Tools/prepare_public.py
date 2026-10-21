"""Create an inspectable, history-free local candidate; never stage, commit, push or delete originals."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[1]
PATTERNS = {
    "private_key": r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----",
    "token": r"(?:gh[pousr]_[A-Za-z0-9]{25,}|github_pat_[A-Za-z0-9_]{30,}|AKIA[A-Z0-9]{16}|sk-[A-Za-z0-9_-]{30,})",
    "private_address": r"\b(?:10\.\d+\.\d+\.\d+|192\.168\.\d+\.\d+|172\.(?:1[6-9]|2\d|3[01])\.\d+\.\d+)\b",
    "personal_path": r"(?<![A-Za-z0-9])[A-Za-z]:[\\/](?!/)",
    "credential_assignment": r"(?i)(?:password|api[_-]?key|access[_-]?token)\s*[:=]\s*[\"'][^\"'\r\n]{6,}[\"']",
}

def candidates():
    files = []
    for name in ("Assets", "Packages", "ProjectSettings", "Tools", "docs"):
        files += [p for p in (ROOT / name).rglob("*") if p.is_file()]
    files += [ROOT / p for p in (".gitignore", "README.md", "LICENSE", "PUBLISH_FILES.txt") if (ROOT / p).exists()]
    return sorted(p for p in files if "__pycache__" not in p.parts and p.suffix != ".pyc")

def scan(data):
    # Compressed pixel bytes are not text; PNG metadata is checked by check_assets.py.
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return []
    content = data.decode("utf-8", errors="replace")
    return [name for name, pattern in PATTERNS.items() if re.search(pattern, content)]

def audit_history():
    result = []
    objects = subprocess.check_output(["git", "rev-list", "--objects", "--all"], cwd=ROOT, text=True, encoding="utf-8").splitlines()
    for row in objects:
        parts = row.split(" ", 1)
        if len(parts) != 2:
            continue
        oid, path = parts
        if subprocess.check_output(["git", "cat-file", "-t", oid], cwd=ROOT, text=True, encoding="utf-8").strip() != "blob":
            continue
        kinds = scan(subprocess.check_output(["git", "cat-file", "blob", oid], cwd=ROOT))
        if path.lower().endswith((".dll", ".pfx", ".pem")):
            kinds.append("binary_or_certificate")
        if "/StreamingAssets/" in "/" + path:
            kinds.append("historical_data_provenance")
        if kinds:
            result.append({"object": oid, "path": path, "findings": kinds})
    return result

def meta():
    for p in sorted((ROOT / "Assets").rglob("*")):
        if p.suffix == ".meta" or Path(str(p) + ".meta").exists():
            continue
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "vectorfield-local/" + p.relative_to(ROOT).as_posix()).hex
        if p.is_dir():
            tail = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        else:
            importer = "MonoImporter" if p.suffix == ".cs" else "AssemblyDefinitionImporter" if p.suffix == ".asmdef" else "DefaultImporter"
            tail = importer + ":\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        Path(str(p) + ".meta").write_text("fileFormatVersion: 2\nguid: " + guid + "\n" + tail, encoding="utf-8")

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--audit-history", action="store_true")
    parser.add_argument("--meta", action="store_true")
    args = parser.parse_args()
    if args.meta:
        meta()
    files = candidates()
    names = [p.relative_to(ROOT).as_posix() for p in files if p.name != "PUBLISH_FILES.txt"]
    (ROOT / "PUBLISH_FILES.txt").write_text("\n".join(sorted(names + ["PUBLISH_FILES.txt"])) + "\n", encoding="utf-8")
    files = candidates()
    findings = []
    for p in files:
        # The scanner's pattern definitions are intentional text, not discovered secrets.
        if p.relative_to(ROOT).as_posix() == "Tools/prepare_public.py":
            continue
        kinds = scan(p.read_bytes())
        if p.suffix.lower() in (".dll", ".pfx", ".pem"):
            kinds.append("binary_or_certificate")
        if kinds:
            findings.append({"path": p.relative_to(ROOT).as_posix(), "findings": kinds})
    report = {"candidate_files": len(files), "current_findings": findings}
    if args.audit_history:
        report["history_findings"] = audit_history()
    out = ROOT / "LocalOnly" / "Audit"
    out.mkdir(parents=True, exist_ok=True)
    (out / "publication-audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("Candidate files:", len(files), "Current findings:", len(findings))
    if args.audit_history:
        print("Historical flagged blobs:", len(report["history_findings"]))
    if args.output:
        target = args.output.resolve()
        # Keep export inside this workspace; do not overwrite an existing checkout.
        if not target.is_relative_to(ROOT) or target == ROOT or (target / ".git").exists():
            raise SystemExit("Export must be a non-repository subdirectory of this workspace.")
        for p in files:
            dest = target / p.relative_to(ROOT)
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(p, dest)
        inventory = {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
        (out / "candidate-sha256.json").write_text(json.dumps(inventory, indent=2), encoding="utf-8")
        print("Local candidate exported (no Git history, no Library).")

if __name__ == "__main__":
    main()
