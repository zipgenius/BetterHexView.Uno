# BetterHexViewer.Uno

Porting del controllo **BetterHexViewer.WinUI3 1.2.1** per Uno Platform con renderer Skia.
Namespace e assembly: `BetterHexViewer.Uno`; classe XAML: `BetterHexViewer`.
La libreria è indipendente da ZGHex e dal checkout WinUI3.
Questo repository contiene la libreria, la demo con le verifiche di integrazione
e i file per compilarla e pubblicarla; il nome della cartella/repository è `BetterHexView.Uno`, mentre
assembly, namespace e pacchetto NuGet restano **`BetterHexViewer.Uno`**.

## Requisiti e piattaforme

- .NET 10 e Uno.Sdk 6.7.30 (versione fissata nel `global.json` della soluzione).
- Applicazione Uno con renderer Skia. La demo di verifica usa `net10.0-desktop` e host Windows,
  Linux X11/framebuffer e macOS. La libreria usa `net10.0`, come raccomandato da Uno
  per le librerie destinate al renderer Skia.
- `Uno.WinUI.Graphics2DSK` e SkiaSharp sono dipendenze transitive gestite da Uno.Sdk.
- Il renderer usa `SKCanvasElement`: sfrutta l'accelerazione hardware quando il
  relativo host Uno la abilita. Non richiede Win2D o Windows App SDK.
- Android, iOS e WebAssembly richiedono un host Uno configurato con `SkiaRenderer`;
  i renderer nativi e WinUI/Windows App SDK non sono target di questa libreria.
  Compilazione ed esecuzione sono state verificate su Windows desktop. Linux,
  macOS, mobile e browser richiedono verifica sul rispettivo host.
- `OpenFileAsync` accetta un percorso accessibile al processo. Su mobile e browser
  il chiamante deve importare il file nel filesystem della propria sandbox oppure
  usare `LoadBytes`. I percorsi del filesystem del computer non sono disponibili
  automaticamente nel browser.

## Uso in un'app Uno

```xml
<!-- Nel progetto dell'app -->
<ItemGroup>
  <ProjectReference Include="../BetterHexView.Uno/BetterHexViewer.Uno.csproj" />
</ItemGroup>
```

```xml
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:hex="using:BetterHexViewer.Uno">
  <hex:BetterHexViewer x:Name="HexViewer"
                       ColumnGroupSize="One" OffsetFormat="Hexadecimal"
                       FullWidth="False" ShowAsciiPanel="True"
                       SelectionChanged="HexViewer_SelectionChanged"/>
</Page>
```

Il tema predefinito è in `Themes/Generic.xaml` e viene caricato automaticamente.

```csharp
await HexViewer.OpenFileAsync(filePath);
// Oppure:
HexViewer.LoadBytes(myByteArray);

HexViewer.SelectRange(0x100, 16);
HexViewer.SelectFromTo(0x200, 0x2FF); // estremi inclusivi, in qualsiasi ordine
HexViewer.ScrollToOffset(0x400);

var match = await HexViewer.SearchAsync("MZ");
var binaryMatch = await HexViewer.SearchAsync(new byte[] { 0x4D, 0x5A });
await HexViewer.SearchNextAsync();
await HexViewer.SearchPreviousAsync();
IReadOnlyList<long> all = await HexViewer.FindAllAsync("hello", ignoreCase: true);

HexViewer.AsciiEncoding = Encoding.GetEncoding(437);
HexViewer.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("DejaVu Sans Mono");
HexViewer.CopySelectionAsHex();
HexViewer.CopySelectionAsAscii();
HexViewer.Clear();
```

Usare proprietà e metodi del controllo sul thread UI, come per gli altri controlli
XAML. I metodi di ricerca eseguono la scansione su thread di lavoro; `SearchAsync`
completa dopo aver aggiornato selezione ed evento sul thread UI.

## Parità con WinUI3

| Funzione | Implementazione Uno |
| --- | --- |
| Rendering immediato e righello | Due superfici Skia, dati e hover separati; vengono disegnate solo le righe visibili |
| File grandi | Mappatura in memoria in sola lettura; fallback con FileStream e cache da 64 KiB nei runtime senza MMF |
| Offset e scorrimento | `long`, anche oltre 2 GiB e oltre `Int32` righe |
| Selezione mouse | Clic e trascinamento nelle colonne HEX/ASCII, scorrimento durante il trascinamento |
| Tastiera | Frecce, Home/End, PageUp/PageDown, modificatori Ctrl/Shift, Ctrl+A e accesso con Tab |
| Ricerca | Testo/byte, avanti/indietro, ripetizione e ritorno all'inizio/fine |
| FindAllAsync | Scansione BMH; file divisi in blocchi da 4 MiB con sovrapposizione ai confini |
| Copia | Menu contestuale e metodi per HEX o testo |
| Hover | Evidenziazione simultanea HEX/ASCII, evento dell'offset, tooltip dopo 500 ms |
| Formati e gruppi | HEX/DEC/OCT; gruppi di 1/2/4/8/16 byte |
| Layout | 16 byte per riga oppure adattamento alla larghezza, ASCII opzionale e spaziature |
| Aspetto | Tema chiaro/scuro, font e pennelli configurabili durante l'esecuzione |
| Codifiche | Latin-1 predefinita, IBM437, CP1252, UTF-8 e qualsiasi Encoding .NET disponibile |

La rappresentazione ASCII decodifica un byte per cella, come l'originale: le sequenze
UTF-8 multibyte non diventano un singolo glifo. I caratteri di controllo e di
sostituzione sono visualizzati come `·`. `ignoreCase` normalizza solo A-Z/a-z.
`FindAllAsync` mantiene il comportamento originale dei risultati non sovrapposti
all'interno di ciascun blocco di scansione; non è una ricerca di tutte le
combinazioni sovrapposte.

### API pubblica mantenuta

- Enum: `OffsetFormat`, `ColumnGroupSize`.
- Risultati/eventi: `HexSearchResult`, `HexSelection`,
  `HexSelectionChangedEventArgs`, `HexHoverOffsetChangedEventArgs`.
- Proprietà: `FontFamily`, `FontSize`, `FontWeight`, `ColumnGroupSize`,
  `OffsetFormat`, `BytesSpacing`, `ExtraLineGap`, `FullWidth`, `SelectionBackground`,
  `SelectionForeground`, `DividerBrush`, `OffsetBackground`, `AsciiBackground`,
  `OffsetForeground`, `RulerForeground`, `TooltipOffsetLabel`, `NoDataLoadedText`,
  `ShowAsciiPanel`, `AsciiEncoding`, `BytesPerLine`, `FileSize`, `Selection`.
  `Background` e `Foreground` sono le proprietà ereditate di Control.
- Metodi: `OpenFileAsync`, `LoadBytes`, `Clear`, `CopySelectionAsHex`,
  `CopySelectionAsAscii`, `ScrollToOffset`, `SelectRange`, `SelectFromTo`,
  `SearchAsync` (byte[]/string), `SearchNextAsync`, `SearchPreviousAsync`,
  `ClearSearch`, `FindAllAsync` (byte[]/string), `Dispose`.
- Eventi: `SelectionChanged`, `SearchResultFound`, `HoverOffsetChanged`.

`SelectionChanged.Data` contiene al massimo 5 MiB, mentre `Length` riporta l'intera
selezione. `Selection.Data` restituisce una copia completa entro i limiti degli
array .NET e della memoria disponibile; selezioni oltre Int32 provocano una
`OverflowException` esplicita. Leggere il file a blocchi per selezioni di tali dimensioni.

Una nuova ricerca annulla la precedente. `ClearSearch`, `Clear`, `LoadBytes`,
`OpenFileAsync`, `Dispose` e la rimozione dal visual tree annullano le scansioni
attive e rilasciano i file. La cancellazione può produrre `OperationCanceledException`
oppure un risultato vuoto/non trovato. Dopo un nuovo caricamento non restano pattern,
selezioni o hover riferiti al file precedente. Come nell'originale, alla rimozione
dal visual tree il contenuto viene rilasciato: ricaricarlo se il controllo viene riutilizzato.

## Compilazione, verifiche e pacchetto

Dalla radice di questo repository:

```sh
dotnet build BetterHexViewer.Uno.csproj -c Release
dotnet pack BetterHexViewer.Uno.csproj -c Release -o artifacts/betterhexviewer
```

La demo e le verifiche di integrazione sono nella cartella
`BetterHexViewer.Uno.Demo` di questo repository. Dalla radice:

```sh
dotnet run --project BetterHexViewer.Uno.Demo -c Release
dotnet run --project BetterHexViewer.Uno.Demo -c Release -- --self-test
```

Le verifiche di integrazione sono ospitate in una vera finestra Uno: template XAML, dependency
properties, focus, ricerca, clipboard, selezione, rendering Skia fuori schermo,
file vuoti, confini di blocchi, cancellazione e file sparso da 40 GiB.
La demo espone apertura file, dati di esempio, ricerca, copia, temi e layout.

Sorgente originale: `zipgenius/BetterHexViewer_WinUI3`, commit
`e0be0795a4342cb9a59c5a6681af208ed2b986ec`, versione progetto 1.2.1.
Licenza MIT, copyright 2026 zipgenius.it; vedere `LICENSE`.
La migrazione da un'app WinUI3 richiede il nuovo riferimento e il namespace
`BetterHexViewer.Uno`; eventuali template personalizzati devono usare `HexCanvas`
al posto delle vecchie parti Win2D.

Riferimenti: [librerie di controlli Uno](https://platform.uno/docs/articles/guides/how-to-create-control-libraries.html),
[SKCanvasElement](https://platform.uno/docs/articles/controls/SKCanvasElement.html).

## Pubblicazione su nuget.org da GitHub

Il workflow `.github/workflows/betterhexviewer-nuget.yml` compila libreria e demo,
esegue le verifiche di integrazione e crea il pacchetto della sola libreria.
Push di codice, pull request e
avvio manuale del workflow producono un artefatto scaricabile; la pubblicazione
su nuget.org avviene soltanto al push di un tag `betterhexviewer-v*`.

1. Caricare i sorgenti e il workflow nel repository GitHub.
2. Creare su nuget.org una API key con permessi di pubblicazione per
   `BetterHexViewer.Uno` (inclusa la creazione del pacchetto per la prima release).
3. In GitHub, Settings → Secrets and variables → Actions, aggiungere il repository
   secret `NUGET_API_KEY` con quella chiave. Non inserirla nei file del repository.
4. Verificare che `<Version>` nel progetto corrisponda alla versione da pubblicare.
5. Creare e inviare il tag, ad esempio per la versione attuale:

```sh
git tag betterhexviewer-v1.2.1
git push origin betterhexviewer-v1.2.1
```

Il tag deve puntare al commit contenente il workflow e i sorgenti della release.
Il workflow rifiuta tag la cui versione non coincide con quella del progetto.
Per le release successive cambiare `<Version>`, fare commit e usare il nuovo tag.
NuGet non consente di sostituire una versione pubblicata; `--skip-duplicate`
consente di rieseguire il workflow senza tentare di sovrascriverla.

Vedere la [documentazione NuGet per la pubblicazione](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package).
