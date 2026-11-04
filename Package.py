"""Create the readable source listing and distribution archives using Python's standard library."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import hashlib

root = Path(__file__).resolve().parent
dist = root / 'dist'
source = []
for p in root.rglob('*'):
    if not p.is_file():
        continue
    rel = p.relative_to(root)
    if any(part in {'.tools', '.test-data', '.git', 'bin', 'obj', 'dist', '__pycache__'} for part in rel.parts):
        continue
    if rel.as_posix() == 'docs/Full-Source.md':
        continue
    source.append(p)
source.sort(key=lambda p: p.relative_to(root).as_posix())
tree = '\n'.join(p.relative_to(root).as_posix() for p in source)
parts = ['# TwistTidy: complete source\n',
         'Architecture and beginner instructions are in README.md. The binary .ico and screenshots are included in the source archive. The icon is reproducible with the application --export-icon <output.ico> diagnostic.\n',
         '## File tree\n\n```text\n' + tree + '\n```\n']
for p in source:
    if p.suffix.lower() not in {'.cs', '.csproj', '.xaml', '.ps1', '.py', '.sln', '.config', '.md'} and p.name != '.gitignore':
        continue
    rel = p.relative_to(root).as_posix()
    language = {'.cs': 'csharp', '.xaml': 'xml', '.csproj': 'xml', '.ps1': 'powershell', '.py': 'python'}.get(p.suffix.lower(), 'text')
    parts += [f'\n## {rel}\n\n````{language}\n', p.read_text(encoding='utf-8-sig'), '\n````\n']
listing = root / 'docs' / 'Full-Source.md'
listing.write_text(''.join(parts), encoding='utf-8')
source.append(listing)
with ZipFile(dist / 'TwistTidy-Source.zip', 'w', ZIP_DEFLATED) as archive:
    for p in source:
        archive.write(p, 'TwistTidy-Source/' + p.relative_to(root).as_posix())
with ZipFile(dist / 'TwistTidy-Portable-win-x64.zip', 'w', ZIP_DEFLATED) as archive:
    for p in (dist / 'TwistTidy').iterdir():
        if p.is_file():
            archive.write(p, 'TwistTidy/' + p.name)
    archive.write(root / 'README.md', 'TwistTidy/README.md')
    archive.write(root / 'docs' / 'VERIFICATION.md', 'TwistTidy/VERIFICATION.md')
hashes = []
for p in sorted(dist.iterdir()):
    if p.is_file() and p.suffix in {'.zip', '.exe'}:
        hashes.append(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.name)
exe = dist / 'TwistTidy' / 'TwistTidy.exe'
hashes.append(hashlib.sha256(exe.read_bytes()).hexdigest() + '  TwistTidy/TwistTidy.exe')
(dist / 'SHA256SUMS.txt').write_text('\n'.join(hashes) + '\n', encoding='utf-8')
print(f'Packaged {len(source)} source files. Distribution hashes saved.')
