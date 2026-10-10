# BetterHexViewer.Uno changelog

This changelog describes the **Uno Platform edition**, independently of the WinUI 3 package.

## Unreleased

- Replace the Italian package README with English documentation adapted from the WinUI 3 edition.
- Document Uno Skia host requirements, platform validation, file access, search and selection limits.
- Add the Uno demo screenshot and English demo labels.
- Add a dedicated demo README and explain manual NuGet publication.

## 1.2.1 — Uno Platform port

- Port BetterHexViewer.WinUI3 1.2.1 to Uno Platform with Skia rendering.
- Replace Win2D rendering with separate main and hover `SKCanvasElement` surfaces.
- Preserve hex/ASCII display, selection, keyboard navigation, search, grouping and themes.
- Support memory-mapped files and a cached stream fallback where mapping is unavailable.
- Provide a desktop demo and integration checks, including a 40 GiB sparse file.

The upstream WinUI history is available in the
[original changelog](https://github.com/zipgenius/BetterHexViewer_WinUI3/blob/main/CHANGELOG.md).
Its Win2D-specific implementation and benchmarks do not apply to this edition.
