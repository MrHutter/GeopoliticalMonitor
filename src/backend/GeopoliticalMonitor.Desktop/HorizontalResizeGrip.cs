using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace GeopoliticalMonitor.Desktop;

public sealed class HorizontalResizeGrip : Grid
{
    public HorizontalResizeGrip()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }
}
