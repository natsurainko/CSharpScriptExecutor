using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using static Natsurainko.Wpf.UI.Helpers.MicaHelper.ParameterTypes;

namespace Natsurainko.Wpf.UI.Helpers;

public static class MicaHelper
{
    public class ParameterTypes
    {
        [Flags]
        public enum DWMWINDOWATTRIBUTE
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            DWMWA_SYSTEMBACKDROP_TYPE = 38
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        };
    }

    [DllImport("DwmApi.dll")]
    private static extern int DwmExtendFrameIntoClientArea( IntPtr hwnd, ref ParameterTypes.MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, ParameterTypes.DWMWINDOWATTRIBUTE dwAttribute, ref int pvAttribute, int cbAttribute);

    private static int ExtendFrame(IntPtr hwnd, ParameterTypes.MARGINS margins)
        => DwmExtendFrameIntoClientArea(hwnd, ref margins);

    private static int SetWindowAttribute(IntPtr hwnd, ParameterTypes.DWMWINDOWATTRIBUTE attribute, int parameter)
        => DwmSetWindowAttribute(hwnd, attribute, ref parameter, Marshal.SizeOf<int>());

    public static void SetMicaTheme(this Window window, bool enableDarkTheme = false)
    {
        SetWindowAttribute(
            new WindowInteropHelper(window).Handle,
            DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
            enableDarkTheme ? 1 : 0);

        window.EnableMicaEffect();
    }

    public static void EnableMicaEffect(this Window window)
        => SetWindowAttribute(
            new WindowInteropHelper(window).Handle,
            DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE,
            2);

    public static void ExtendFrameIntoClientArea(this Window window)
    {
        var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        hwndSource.CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

        ExtendFrame(hwndSource.Handle, new MARGINS
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1
        });
    }
}
