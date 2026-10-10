using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32.SafeHandles;
using SkiaSharp;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace BetterHexViewer.Uno.Demo;

// Runs inside an actual Uno window: dependency properties, dispatcher, template,
// clipboard and Skia rendering are exercised, as well as the file/search paths.
internal static class ControlTests
{
    private static int _checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine($"PASS {++_checks}: {name}");
    }

    public static async Task RunAsync(BetterHexViewer viewer)
    {
        var temp = Path.Combine(Path.GetTempPath(), "betterhex-uno-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            Check(viewer.ActualWidth > 0 && viewer.ActualHeight > 0, "control is laid out in Uno");
            var canvas = typeof(BetterHexViewer).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewer);
            Check(canvas != null, "Generic.xaml resolves both rendering surfaces");
            Check(viewer.Focus(FocusState.Programmatic), "custom control accepts keyboard focus");
            Check(viewer.FocusState != FocusState.Unfocused, "keyboard focus stays on control");
            int events = 0;
            HexSelectionChangedEventArgs? selection = null;
            viewer.SelectionChanged += (_, e) => { events++; selection = e; };
            viewer.LoadBytes(Encoding.ASCII.GetBytes("AbC abc xyz ABC"));
            Check(viewer.FileSize == 15 && !viewer.Selection.HasSelection, "LoadBytes resets size and selection");
            viewer.SelectFromTo(5, 2);
            Check(viewer.Selection.StartOffset == 2 && viewer.Selection.Length == 4 && events > 0, "reversed inclusive selection and event");
            viewer.SelectRange(100, 100);
            Check(viewer.Selection.StartOffset == 14 && viewer.Selection.Length == 1, "selection clamps to file");
            viewer.LoadBytes(Encoding.ASCII.GetBytes("AbC abc xyz ABC"));
            HexSearchResult? searchEvent = null;
            viewer.SearchResultFound += (_, e) => searchEvent = e;
            var result = await viewer.SearchAsync("abc", ignoreCase: true);
            Check(result.Found && result.Offset == 0 && !result.Wrapped && searchEvent == result, "case-insensitive text search and event before completion");
            Check(viewer.Selection.StartOffset == 0 && viewer.Selection.Length == 3, "search selects match before completion");
            result = await viewer.SearchNextAsync();
            Check(result.Offset == 4, "search next");
            result = await viewer.SearchPreviousAsync();
            Check(result.Offset == 0, "search previous");
            result = await viewer.SearchPreviousAsync();
            Check(result.Offset == 12 && result.Wrapped, "backward wrap reports Wrapped");
            result = await viewer.SearchNextAsync();
            Check(result.Offset == 0 && result.Wrapped, "forward wrap reports Wrapped");
            var offsets = await viewer.FindAllAsync("abc", ignoreCase: true);
            Check(offsets.SequenceEqual(new long[] { 0, 4, 12 }), "FindAll text");
            offsets = await viewer.FindAllAsync(Encoding.ASCII.GetBytes("ABC"));
            Check(offsets.SequenceEqual(new long[] { 12 }), "FindAll byte pattern resets case mode");
            result = await viewer.SearchNextAsync();
            Check(result.Offset == 12, "repeated byte search stays case-sensitive");
            result = await viewer.SearchAsync("missing");
            Check(!result.Found && result.Offset == -1, "missing search");
            Check(!(await viewer.SearchAsync(Array.Empty<byte>())).Found, "empty pattern");
            viewer.ClearSearch();
            Check(!(await viewer.SearchNextAsync()).Found, "ClearSearch clears repeat state");
            viewer.SelectRange(4, 3);
            viewer.CopySelectionAsHex();
            Check(await Clipboard.GetContent().GetTextAsync() == "61 62 63", "hex clipboard copy");
            viewer.CopySelectionAsAscii();
            Check(await Clipboard.GetContent().GetTextAsync() == "abc", "text clipboard copy");

            // Array and mapped-file searches must agree at both 4 MiB boundaries.
            const int chunk = 4 * 1024 * 1024;
            var bytes = new byte[2 * chunk + 100];
            var pattern = Encoding.ASCII.GetBytes("boundary!");
            long[] expected = [chunk - 4, chunk + 30, 2 * chunk - 3];
            foreach (long offset in expected) pattern.CopyTo(bytes, (int)offset);
            viewer.LoadBytes(bytes);
            Check((await viewer.FindAllAsync(pattern)).SequenceEqual(expected), "array boundary search");
            var path = Path.Combine(temp, "chunks.bin");
            await File.WriteAllBytesAsync(path, bytes);
            await viewer.OpenFileAsync(path);
            Check((await viewer.FindAllAsync(pattern)).SequenceEqual(expected), "mapped parallel search across chunk boundaries");
            result = await viewer.SearchAsync(pattern);
            Check(result.Offset == expected[0], "mapped forward search across boundary");
            result = await viewer.SearchNextAsync();
            Check(result.Offset == expected[1], "mapped next search");
            result = await viewer.SearchPreviousAsync();
            Check(result.Offset == expected[0], "mapped backward search across boundary");
            viewer.SelectRange(0, bytes.Length);
            Check(selection!.Data.Length == 5 * 1024 * 1024 && selection.Length == bytes.Length, "selection event payload capped at 5 MiB");
            Check(viewer.Selection.Data.Length == bytes.Length, "Selection property returns full data");
            var pendingSearch = viewer.FindAllAsync(new byte[] { 0, 0, 0, 1 });
            viewer.Clear();
            try { await pendingSearch; } catch (OperationCanceledException) { }
            Check(viewer.FileSize == 0 && selection!.Length == 0, "Clear safely cancels searches and notifies selection");
            File.Delete(path);
            Check(!File.Exists(path), "Clear releases mapped file");
            var empty = Path.Combine(temp, "empty.bin");
            await File.WriteAllBytesAsync(empty, []);
            await viewer.OpenFileAsync(empty);
            Check(viewer.FileSize == 0 && !viewer.Selection.HasSelection, "empty mapped file");
            var small = Path.Combine(temp, "small.bin");
            await File.WriteAllBytesAsync(small, pattern);
            var pendingOpen = viewer.OpenFileAsync(small);
            viewer.LoadBytes(new byte[] { 42 });
            await pendingOpen;
            viewer.SelectRange(0, 1);
            Check(viewer.FileSize == 1 && viewer.Selection.Data[0] == 42, "later LoadBytes takes precedence over pending OpenFileAsync");

            // Sparse file tests offsets beyond 2 GiB and row indices beyond Int32.
            var large = Path.Combine(temp, "large.bin");
            const long marker = 40L * 1024 * 1024 * 1024;
            using (var stream = new FileStream(large, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            {
                if (OperatingSystem.IsWindows())
                    Check(DeviceIoControl(stream.SafeFileHandle, 0x900C4, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero), "create sparse test file");
                stream.SetLength(marker + 256);
                stream.Position = marker;
                stream.Write(pattern);
            }
            await viewer.OpenFileAsync(large);
            Check(viewer.FileSize == marker + 256, "40 GiB file size uses Int64");
            viewer.SelectRange(marker - 1, 1);
            result = await viewer.SearchAsync(pattern);
            Check(result.Offset == marker && viewer.Selection.Data.SequenceEqual(pattern), "search and selection beyond 2 GiB");
            viewer.ScrollToOffset(marker);
            long topLine = (long)typeof(BetterHexViewer).GetField("_topLine", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewer)!;
            Check(topLine > int.MaxValue, "scroll row index beyond Int32");
            typeof(BetterHexViewer).GetField("_hoverByte", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(viewer, marker);
            Check(DrawSurface(viewer, "_hoverCanvas", 1280, 800) > 0, "hover rendering beyond Int32 row index");
            viewer.Clear();
            CheckStreamFallback(small, pattern, 0);
            CheckStreamFallback(large, pattern, marker);

            viewer.LoadBytes(Enumerable.Range(0, 256).Select(i => (byte)i).ToArray());
            viewer.FullWidth = true;
            int withAscii = viewer.BytesPerLine;
            viewer.ShowAsciiPanel = false;
            Check(viewer.BytesPerLine > withAscii, "auto-fit uses space freed by hiding ASCII panel");
            foreach (ColumnGroupSize group in Enum.GetValues<ColumnGroupSize>())
            {
                viewer.ColumnGroupSize = group;
                Check(viewer.BytesPerLine >= (int)group && viewer.BytesPerLine % (int)group == 0, $"auto-fit grouping {group}");
            }
            viewer.FontFamily = new FontFamily("DejaVu Sans Mono");
            viewer.FontSize = 17;
            viewer.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            viewer.ExtraLineGap = 8;
            viewer.BytesSpacing = 12;
            viewer.FullWidth = false;
            viewer.ShowAsciiPanel = true;
            viewer.ColumnGroupSize = ColumnGroupSize.One;
            Check(viewer.BytesPerLine == 16, "fixed layout retains 16 bytes per line");
            foreach (OffsetFormat format in Enum.GetValues<OffsetFormat>())
            {
                viewer.OffsetFormat = format;
                Check(DrawSurface(viewer, "_canvas", 1280, 800) > 10000, $"Skia renders {format} ruler, bytes and ASCII");
                CheckTextPixels(viewer);
            }
            viewer.AsciiEncoding = Encoding.GetEncoding(437);
            viewer.AsciiEncoding = Encoding.UTF8;
            Check(viewer.AsciiEncoding == Encoding.UTF8, "encoding switching");
            viewer.RequestedTheme = ElementTheme.Dark;
            viewer.Background = new SolidColorBrush(Color.FromArgb(255, 14, 20, 32));
            Check(DrawSurface(viewer, "_canvas", 1280, 800) > 10000, "dark theme and custom brushes render");
            viewer.RequestedTheme = ElementTheme.Light;
            viewer.ApplyTemplate();
            viewer.LoadBytes([1, 2, 3]);
            viewer.SelectRange(0, 2);
            Check(viewer.Selection.Data.SequenceEqual(new byte[] { 1, 2 }), "control works after template reapplication");
        }
        finally
        {
            viewer.Clear();
            Directory.Delete(temp, true);
        }
        Console.WriteLine($"ALL {_checks} CHECKS PASSED");
    }

    private static int DrawSurface(BetterHexViewer viewer, string field, int width, int height)
    {
        var surface = typeof(BetterHexViewer).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewer)!;
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        surface.GetType().GetMethod("RenderOverride", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(surface, [canvas, new Windows.Foundation.Size(width, height)]);
        int count = 0;
        foreach (var pixel in bitmap.Pixels) if (pixel.Alpha > 0) count++;
        return count;
    }

    private static void CheckTextPixels(BetterHexViewer viewer)
    {
        object Field(string name) => typeof(BetterHexViewer).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewer)!;
        double charWidth = (double)Field("_charWidth");
        double dividerY = (double)Field("_cachedDividerY");
        double rowHeight = (double)Field("_lineHeight") + viewer.ExtraLineGap;
        var surface = Field("_canvas");
        using var bitmap = new SKBitmap(1280, 800);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        surface.GetType().GetMethod("RenderOverride", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(surface, [canvas, new Windows.Foundation.Size(1280, 800)]);
        int ColorsIn(double x, double y, double w, double h)
        {
            var colors = new HashSet<SKColor>();
            for (int cy = (int)y + 2; cy < y + h - 2; cy++)
                for (int cx = (int)x + 2; cx < x + w - 2; cx++) colors.Add(bitmap.GetPixel(cx, cy));
            return colors.Count;
        }
        Check(ColorsIn((double)Field("_cachedHexStartX"), dividerY, 2 * charWidth, rowHeight) > 1,
            $"HEX glyph ink is present for {viewer.OffsetFormat}");
        Check(ColorsIn((double)Field("_cachedAsciiStartX"), dividerY, 1.5 * charWidth, rowHeight) > 1,
            $"ASCII glyph ink is present for {viewer.OffsetFormat}");
        Check(ColorsIn(0, dividerY, 15 * charWidth, rowHeight) > 1,
            $"offset glyph ink is present for {viewer.OffsetFormat}");
        var previewPath = Environment.GetEnvironmentVariable("BETTERHEX_RENDER_PREVIEW");
        if (!string.IsNullOrEmpty(previewPath))
        {
            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var output = File.Create(previewPath);
            png.SaveTo(output);
        }
    }

    private static void CheckStreamFallback(string path, byte[] expected, long offset)
    {
        var type = typeof(BetterHexViewer).Assembly.GetType("BetterHexViewer.Uno.HexFileAccessor")!;
        using var source = (IDisposable)type.GetMethod("Open", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [path, false])!;
        var buffer = new byte[expected.Length];
        type.GetMethod("ReadArray", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(source, [offset, buffer, 0, buffer.Length]);
        Check(buffer.SequenceEqual(expected), $"stream fallback bulk read at {offset}");
        var readByte = type.GetMethod("ReadByte", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Check(Enumerable.Range(0, expected.Length).All(i => (byte)readByte.Invoke(source, [offset + i])! == expected[i]), $"stream fallback cached byte reads at {offset}");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code,
        IntPtr input, uint inputSize, IntPtr output, uint outputSize, out uint bytesReturned, IntPtr overlapped);
}
