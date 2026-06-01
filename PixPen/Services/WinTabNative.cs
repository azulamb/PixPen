using System.Runtime.InteropServices;

namespace PixPen.Services;

/// <summary>
/// Wintab32.dll P/Invoke 宣言群。
/// Wacom ドライバが未インストールの場合は DLL が存在せず例外が発生する。
/// </summary>
internal static class WinTabNative
{
    // WTInfo categories
    internal const uint WTI_INTERFACE  = 1;
    internal const uint WTI_DEFCONTEXT  = 3;
    internal const uint WTI_DEFSYSCTX  = 4;   // システムコンテキスト（カーソル移動を維持）
    internal const uint WTI_CURSORS    = 200;
    internal const uint WTI_DEVICES    = 100;

    // WTInfo indices
    internal const uint IFC_SPECVERSION = 2;
    internal const uint CTX_MSGBASE = 18;

    // Packet field flags
    internal const uint PK_CURSOR = 0x0020;
    internal const uint PK_BUTTONS = 0x0040;
    internal const uint PK_X = 0x0080;
    internal const uint PK_Y = 0x0100;
    internal const uint PK_NORMAL_PRESSURE = 0x0400;

    // LOGCONTEXT options
    internal const uint CXO_SYSTEM = 0x0001;
    internal const uint CXO_PEN = 0x0002;
    internal const uint CXO_MESSAGES = 0x0004;

    // WM_ACTIVATE
    internal const int WM_ACTIVATE = 0x0006;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct LOGCONTEXT
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
        public string lcName;
        public uint lcOptions;
        public uint lcStatus;
        public uint lcLocks;
        public uint lcMsgBase;
        public uint lcDevice;
        public uint lcPktRate;
        public uint lcPktData;
        public uint lcPktMode;
        public uint lcMoveMask;
        public uint lcBtnDnMask;
        public uint lcBtnUpMask;
        public int lcInOrgX, lcInOrgY, lcInOrgZ;
        public int lcInExtX, lcInExtY, lcInExtZ;
        public int lcOutOrgX, lcOutOrgY, lcOutOrgZ;
        public int lcOutExtX, lcOutExtY, lcOutExtZ;
        public int lcSensX, lcSensY, lcSensZ;
        public int lcSysMode;
        public int lcSysOrgX, lcSysOrgY;
        public int lcSysExtX, lcSysExtY;
        public int lcSysSensX, lcSysSensY;
    }

    // リクエストするパケットフィールド順: CURSOR, BUTTONS, X, Y, NORMAL_PRESSURE
    /// <summary>WTInfo の AXIS 系カテゴリで返される軸情報構造体</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct AXIS
    {
        public int  axMin;
        public int  axMax;
        public uint axUnits;
        public int  axResolution; // FIX32
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WinTabPacket
    {
        public uint Cursor;
        public uint Buttons;
        public int X;
        public int Y;
        public uint NormalPressure;
    }

    [DllImport("Wintab32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    internal static extern uint WTInfo(uint wCategory, uint nIndex, IntPtr lpOutput);

    [DllImport("Wintab32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    internal static extern uint WTInfoA(uint wCategory, uint nIndex, ref LOGCONTEXT lpOutput);

    [DllImport("Wintab32.dll", CharSet = CharSet.Ansi)]
    internal static extern IntPtr WTOpen(IntPtr hWnd, ref LOGCONTEXT lpLogCtx, bool fEnable);

    [DllImport("Wintab32.dll")]
    internal static extern bool WTClose(IntPtr hCtx);

    [DllImport("Wintab32.dll")]
    internal static extern bool WTPacket(IntPtr hCtx, uint wSerial, ref WinTabPacket lpPkt);

    [DllImport("Wintab32.dll")]
    internal static extern bool WTEnable(IntPtr hCtx, bool fEnable);

    [DllImport("Wintab32.dll")]
    internal static extern bool WTOverlap(IntPtr hCtx, bool fToTop);

    [DllImport("user32.dll")]
    internal static extern bool SetCursorPos(int X, int Y);
}
