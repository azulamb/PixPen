using System.Runtime.InteropServices;

namespace PixPen.Services;

/// <summary>
/// Windows WM_POINTER API の P/Invoke 宣言。
/// WPF の WISP (StylusDown) が発火しない環境（Wacom の WM_POINTER パス等）で
/// ペン筆圧・消しゴムフラグを取得するために使用する。
/// </summary>
internal static class PointerNative
{
    internal const int WM_POINTERDOWN   = 0x0246;
    internal const int WM_POINTERUPDATE = 0x0245;
    internal const int WM_POINTERUP     = 0x0247;

    internal const uint PT_PEN = 3;

    /// <summary>wParam の下位 16bit がポインター ID</summary>
    internal static uint GetPointerId(IntPtr wParam) => (uint)wParam & 0xFFFF;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINTER_INFO
    {
        public uint   pointerType;
        public uint   pointerId;
        public uint   frameId;
        public uint   pointerFlags;
        public IntPtr sourceDevice;
        public IntPtr hwndTarget;
        public POINT  ptPixelLocation;
        public POINT  ptHimetricLocation;
        public POINT  ptPixelLocationRaw;
        public POINT  ptHimetricLocationRaw;
        public uint   dwTime;
        public uint   historyCount;
        public int    inputData;
        public uint   dwKeyStates;
        public ulong  PerformanceCount;
        public int    ButtonChangeType;
    }

    [Flags]
    internal enum PenFlags : uint
    {
        None     = 0x00,
        Barrel   = 0x01,
        Inverted = 0x02,  // ペンを裏返し（消しゴム側）
        Eraser   = 0x04   // 消しゴムボタン押下
    }

    [Flags]
    internal enum PenMask : uint
    {
        None     = 0x00,
        Pressure = 0x01,  // pressure フィールドが有効
        Rotation = 0x02,
        TiltX    = 0x04,
        TiltY    = 0x08
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINTER_PEN_INFO
    {
        public POINTER_INFO pointerInfo;
        public PenFlags     penFlags;
        public PenMask      penMask;
        public uint         pressure;   // 0 – 1024
        public uint         rotation;
        public int          tiltX;
        public int          tiltY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetPointerType(uint pointerId, out uint pointerType);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetPointerPenInfo(uint pointerId, out POINTER_PEN_INFO penInfo);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetMessageExtraInfo();

    // POINTER_FLAG 定数
    internal const uint POINTER_FLAG_INCONTACT    = 0x00000004;
    internal const uint POINTER_FLAG_FIRSTBUTTON  = 0x00000010;

    /// <summary>
    /// マウスイベントが生成された時点でアクティブなペンポインターを検索し、
    /// 筆圧と消しゴム状態を返す。
    /// WM_POINTER メッセージを受信せずとも GetPointerPenInfo でポインター状態を
    /// 直接クエリできる。pointer ID は小さい値から順にスキャンする。
    /// </summary>
    internal static bool TryGetActivePenPressure(
        ref uint lastId, out double pressure, out bool isEraser)
    {
        pressure = 1.0;
        isEraser = false;

        // 前回の ID を先に試す（高速パス）
        if (lastId > 0 && TryQueryPen(lastId, out pressure, out isEraser))
            return true;

        // ID 1 〜 256 をスキャン（Wacom は通常 1〜数個の小さい ID）
        for (uint pid = 1; pid <= 256; pid++)
        {
            if (pid == lastId) continue;
            if (TryQueryPen(pid, out pressure, out isEraser))
            {
                lastId = pid;
                return true;
            }
        }
        return false;
    }

    private static bool TryQueryPen(uint pid, out double pressure, out bool isEraser)
    {
        pressure = 1.0;
        isEraser = false;
        if (!GetPointerPenInfo(pid, out var info)) return false;
        if (info.pointerInfo.pointerType != PT_PEN) return false;

        // 接触中かどうか (IN_CONTACT または筆圧 > 0)
        bool inContact = (info.pointerInfo.pointerFlags & POINTER_FLAG_INCONTACT) != 0
                      || info.pressure > 0;
        if (!inContact) return false;

        bool hasPressure = info.penMask.HasFlag(PenMask.Pressure);
        pressure = hasPressure ? Math.Clamp(info.pressure / 1024.0, 0.0, 1.0) : 1.0;
        isEraser = info.penFlags.HasFlag(PenFlags.Inverted) ||
                   info.penFlags.HasFlag(PenFlags.Eraser);
        return true;
    }
}
