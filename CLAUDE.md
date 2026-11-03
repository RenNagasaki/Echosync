# Echosync

Inherits all conventions from the parent `../CLAUDE.md`.

## Build

- Solution: `Echosync.sln`
- **Always build the full solution** (client + server together). Never build individual projects.

## Project Structure

- `Echosync/` — Main Dalamud plugin project.
- `Echosync-Data/` — Data/assets submodule or folder.
- `Echosync-Server/` — Server-side component.
- `Data/` — Shared data.

## Git-Remotes
- `origin` → `https://gitlab.echotools.cloud/echotools/echosync.git` (Hauptremote, hierhin pushen).
- `github` → `https://github.com/RenNagasaki/Echosync.git`: Nur-Lese-Spiegel, den der CI-Job `mirror-to-github` (`.gitlab-ci.yml`) bei jedem Push auf den Default-Branch oder einem Tag additiv befüllt (nie `--mirror`, sonst gehen GitHub-Release-Tags verloren). Nicht direkt nach GitHub pushen: der Job pusht mit `--force` und überschreibt das.
