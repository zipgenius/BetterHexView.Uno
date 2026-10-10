# BetterHexViewer.Uno demo

This is the **Uno Platform desktop demo**, using the Skia renderer and `net10.0-desktop`.
It references the Uno control project in this repository; it is independent of the WinUI 3 demo.

## Run

From the repository root with .NET 10 installed:

```shell
dotnet run --project BetterHexViewer.Uno.Demo -c Release
```

The app opens with 8 KiB of generated sample data, beginning with
`BetterHexViewer.Uno - hello Uno!`. No user file is needed for the sample.

- Enter a path accessible to the process and choose **Open file**, or choose **Sample data**.
- Adjust offset format, grouping, encoding, theme, width fitting and the ASCII panel.
- Search text, move to the next/previous result, or count all matches.
- Select bytes in HEX or ASCII and copy as hexadecimal or text.
- **Clear** unloads the content.

The supplied desktop project supports Uno's Windows, Linux and macOS hosts;
runtime checks and the screenshot were produced on Windows. Other hosts require
validation. The demo accepts a filesystem path directly rather than opening a
platform-specific file picker.

![Uno desktop demo](https://raw.githubusercontent.com/zipgenius/BetterHexView.Uno/master/docs/images/uno-demo.png)

## Integration checks

```shell
dotnet run --project BetterHexViewer.Uno.Demo -c Release -- --self-test
```

This opens a real Uno window, runs the checks and exits with code 0 on success.
Checks include rendering, template loading, focus, search, clipboard, selection,
empty files, block boundaries, cancellation and a 40 GiB sparse file.

See the [main README](https://github.com/zipgenius/BetterHexView.Uno/blob/master/README.md)
for the library API, platform requirements and NuGet publication.
