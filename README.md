# Scumm2Batocera – ScummVM to Batocera Exporter

Scumm2Batocera is a small Windows tool that scans a folder of ScummVM games and exports them in a Batocera-ready format.

It uses a local JSON database extracted from the newest ScummVM release (`--list-games`), so it can match against the latest supported games.

## Features

- Scan a folder with multiple ScummVM game subfolders.
- Fuzzy-match folder names to real ScummVM games:
  - Exact title matches
  - Short ID matches
  - Keyword overlap
  - Acronyms / common abbreviations (e.g. MI2, DOTT, INDY3).
- Show:
  - Original folder name
  - Identified game title
  - ScummVM short ID
  - Confidence score
  - A short “reason” for the match.
- Export to a Batocera-ready structure:
  - Copies the game folders.
  - Creates `.scummvm` files (containing the short ID) next to each game.
- Writes `identification-log.txt` and `export-log.txt` with details.

## Requirements

- Windows
- .NET 8.0 Desktop Runtime (or Visual Studio 2022+ with .NET 8 support)

## Usage

1. Put `scummvm-games.json` in the same folder as the executable.
2. Start the app.
3. Select your **source folder** (one subfolder per game).
4. Click **Scan Folder** and review the matches.
5. Select an **output folder**.
6. Click **Export Batocera Ready** to copy games and create `.scummvm` files.

> This tool is basically feature-complete and not under constant development. I may update it occasionally if needed.

## Disclaimer

This project is not affiliated with or endorsed by ScummVM or Batocera.  
Game data and names belong to their respective owners.
