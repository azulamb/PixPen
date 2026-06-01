using PixPen.Models;

namespace PixPen.Services;

public static class TabletServiceFactory
{
    public static ITabletService Create(AppSettings settings)
    {
        if (settings.UseWinTab)
        {
            try { return new WinTabTabletService(); }
            catch { }
        }
        return new WindowsInkTabletService();
    }
}
