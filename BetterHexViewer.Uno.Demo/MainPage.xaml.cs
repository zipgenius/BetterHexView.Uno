using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BetterHexViewer.Uno.Demo;

public sealed partial class MainPage : Page
{
    private bool _ready;

    public MainPage()
    {
        InitializeComponent();
        _ready = true;
        Loaded += async (_, _) =>
        {
            if (Environment.GetCommandLineArgs().Contains("--self-test"))
            {
                try { await ControlTests.RunAsync(Viewer); Environment.Exit(0); }
                catch (Exception ex) { Console.Error.WriteLine(ex); Environment.Exit(1); }
            }
            else LoadSample();
        };
    }

    private void LoadSample()
    {
        var data = new byte[8192];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)i;
        Encoding.ASCII.GetBytes("BetterHexViewer.Uno - hello Uno!").CopyTo(data, 0);
        Viewer.LoadBytes(data);
        Status.Text = $"{Viewer.FileSize:N0} byte caricati";
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { Status.Text = "Operazione annullata."; }
        catch (Exception ex) { Status.Text = ex.Message; }
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async () => { await Viewer.OpenFileAsync(FilePath.Text); Status.Text = $"{Viewer.FileSize:N0} byte caricati"; });
    private void Sample_Click(object sender, RoutedEventArgs e) => LoadSample();
    private async void Search_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async () => ShowResult(await Viewer.SearchAsync(Query.Text)));
    private async void Next_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async () => ShowResult(await Viewer.SearchNextAsync()));
    private async void Previous_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async () => ShowResult(await Viewer.SearchPreviousAsync()));
    private async void All_Click(object sender, RoutedEventArgs e)
        => await RunAsync(async () => Status.Text = $"{(await Viewer.FindAllAsync(Query.Text)).Count:N0} occorrenze");
    private void ShowResult(HexSearchResult result)
        => Status.Text = result.Found ? $"Trovato a 0x{result.Offset:X} ({result.Length} byte)" : "Nessuna occorrenza";
    private void CopyHex_Click(object sender, RoutedEventArgs e) => Viewer.CopySelectionAsHex();
    private void CopyText_Click(object sender, RoutedEventArgs e) => Viewer.CopySelectionAsAscii();
    private void Clear_Click(object sender, RoutedEventArgs e) { Viewer.Clear(); Status.Text = "Nessun dato caricato"; }
    private void Viewer_SelectionChanged(object? sender, HexSelectionChangedEventArgs e)
    {
        if (SelectionStatus != null) SelectionStatus.Text = e.Length > 0 ? $"Selezione: 0x{e.StartOffset:X}, {e.Length:N0} byte" : "Nessuna selezione";
    }
    private void Viewer_HoverOffsetChanged(object? sender, HexHoverOffsetChangedEventArgs e)
        => HoverStatus.Text = e.Offset < 0 ? "" : $"Puntatore: 0x{e.Offset:X} · {(e.IsAsciiColumn ? "ASCII" : "HEX")}";
    private void Options_Changed(object sender, SelectionChangedEventArgs e) => ApplyOptions();
    private void Options_Click(object sender, RoutedEventArgs e) => ApplyOptions();
    private void ApplyOptions()
    {
        if (!_ready) return;
        Viewer.OffsetFormat = (OffsetFormat)OffsetMode.SelectedIndex;
        Viewer.ColumnGroupSize = (ColumnGroupSize)(1 << Grouping.SelectedIndex);
        Viewer.AsciiEncoding = Encoding.GetEncoding(EncodingMode.SelectedIndex switch { 1 => 437, 2 => 1252, 3 => 65001, _ => 28591 });
        RequestedTheme = (ElementTheme)ThemeMode.SelectedIndex;
        Viewer.FullWidth = Fit.IsChecked == true;
        Viewer.ShowAsciiPanel = Ascii.IsChecked == true;
    }
}
