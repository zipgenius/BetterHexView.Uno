using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace BetterHexViewer.Uno;

public sealed partial class BetterHexViewer
{
    private long SearchWorker(byte[] pattern, long start, bool forward, CancellationToken token, bool ignoreCase)
    {
        lock (_dataGate)
        {
            if (token.IsCancellationRequested) return -1;
            return forward
                ? BmhForward(pattern, start, token, ignoreCase)
                : BmhBackward(pattern, start, token, ignoreCase);
        }
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs args)
    {
        UpdateBytesPerLine();
        UpdateScrollBar();
        ScheduleRender();
    }

    private void DetachRoot()
    {
        if (_rootContent == null) return;
        _rootContent.RemoveHandler(PointerPressedEvent, (PointerEventHandler)OnRootPointerPressed);
        _rootContent = null;
        _hasFocus = false;
    }

    private void DetachTemplate()
    {
        if (_hoverCanvas != null) _hoverCanvas.Draw -= OnHoverCanvasDraw;
        if (_canvas == null) return;
        _canvas.Draw -= OnCanvasDraw;
        _canvas.SizeChanged -= OnCanvasSizeChanged;
        _canvas.PointerPressed -= OnPointerPressed;
        _canvas.PointerMoved -= OnPointerMoved;
        _canvas.PointerMoved -= OnPointerHover;
        _canvas.PointerReleased -= OnPointerReleased;
        _canvas.RightTapped -= OnRightTapped;
        _canvas.PointerExited -= OnPointerExited;
        _canvas.PointerPressed -= OnSbPointerPressed;
        _canvas.PointerMoved -= OnSbPointerMoved;
        _canvas.PointerReleased -= OnSbPointerReleased;
        _canvas.ReleasePointerCaptures();
    }
}
