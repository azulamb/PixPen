using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using static PixPen.Services.WinTabNative;

namespace PixPen.Services;

/// <summary>WinTab WT_PACKET イベント引数</summary>
public record WinTabInputArgs(int X, int Y, double Pressure, bool IsDown, bool IsEraser);

/// <summary>
/// Wacom WinTab API によるタブレットサービス。
/// wintab32.dll が存在しない場合は IsAvailable = false となり安全にフォールバックする。
/// </summary>
public class WinTabTabletService : ITabletService
{
    private IntPtr _hCtx = IntPtr.Zero;
    private uint _wtPacketMessage;
    private uint _maxPressure = 1023;
    private bool _lcOutExtYNeg;
    private int  _lcOutOrgY;
    private int  _lcOutExtY;

    public bool IsAvailable => _isDllPresent && _hCtx != IntPtr.Zero;
    public string DisplayName => "Wacom WinTab";
    private bool _isDllPresent;

    public double CurrentPressure { get; private set; } = 1.0;
    public bool   CurrentIsEraser { get; private set; } = false;

    // DrawingCanvas がマルチモニタ対応の比率マッピングに使用する出力範囲
    public int OutOrgX { get; private set; }
    public int OutOrgY { get; private set; }
    public int OutExtX { get; private set; } = 1;
    public int OutExtY { get; private set; } = 1;

    public event Action<WinTabInputArgs>? PacketReceived;

    // ─── WH_GETMESSAGE フック ─────────────────────────────────────────────
    // HwndSource.AddHook は EnablePointerSupport=true 時に機能しない場合があるため、
    // メッセージキューレベルで WT_PACKET を捕捉する。

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    private HookProc? _getMsgDelegate;   // GC 防止
    private HookProc? _callWndDelegate;  // GC 防止
    private IntPtr _getMsgHook;
    private IntPtr _callWndHook;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr hmod, uint tid);
    [DllImport("user32.dll")] private static extern bool   UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr h, int c, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    // WH_GETMESSAGE (3) が使う MSG 構造体
    [StructLayout(LayoutKind.Sequential)]
    private struct HOOKMSG
    {
        public IntPtr hwnd; public uint message;
        public IntPtr wParam; public IntPtr lParam;
        public uint time; public int ptX, ptY;
    }

    // WH_CALLWNDPROC (4) が使う CWPSTRUCT 構造体
    [StructLayout(LayoutKind.Sequential)]
    private struct CWPSTRUCT
    {
        public IntPtr lParam;   // 元の lParam
        public IntPtr wParam;   // 元の wParam（WT_PACKET ではシリアル番号）
        public uint   message;
        // 4 バイト padding（64bit で HWND を 8 バイト境界に揃えるため）
        public IntPtr hwnd;
    }


    public void Initialize(Window window)
    {
        try
        {
            var ver = WTInfo(WTI_INTERFACE, IFC_SPECVERSION, IntPtr.Zero);
            _isDllPresent = ver > 0;
            if (!_isDllPresent) return;

            var lc = new LOGCONTEXT();
            if (WTInfoA(WTI_DEFSYSCTX, 0, ref lc) == 0) return;

            _wtPacketMessage = lc.lcMsgBase;
            _lcOutOrgY  = lc.lcOutOrgY;
            _lcOutExtY  = lc.lcOutExtY;
            _lcOutExtYNeg = lc.lcOutExtY < 0;
            // 出力範囲を公開（DrawingCanvas の比率マッピング用）
            OutOrgX = lc.lcOutOrgX;
            OutOrgY = lc.lcOutOrgY;
            OutExtX = lc.lcOutExtX;
            OutExtY = lc.lcOutExtY;

            lc.lcPktData  = PK_CURSOR | PK_BUTTONS | PK_X | PK_Y | PK_NORMAL_PRESSURE;
            lc.lcPktMode  = 0;
            lc.lcMoveMask = PK_X | PK_Y | PK_NORMAL_PRESSURE;
            lc.lcOptions |= CXO_MESSAGES;

            // lcDevice フィールドを使って正しいデバイスの圧力レンジを取得する
            _maxPressure = ReadMaxPressure(lc.lcDevice);

            var hwnd = new WindowInteropHelper(window).Handle;
            _hCtx = WTOpen(hwnd, ref lc, true);
            if (_hCtx == IntPtr.Zero) return;

            // HwndSource の代わりに WH_GETMESSAGE フックで WT_PACKET を捕捉する。
            // WH_GETMESSAGE は PostMessage された全メッセージをキューレベルで傍受する。
            uint tid = GetCurrentThreadId();
            _getMsgDelegate  = OnGetMessage;
            _callWndDelegate = OnCallWndProc;
            // WH_GETMESSAGE (3): PostMessage されたパケットを捕捉
            _getMsgHook  = SetWindowsHookEx(3, _getMsgDelegate,  IntPtr.Zero, tid);
            // WH_CALLWNDPROC (4): SendMessage されたパケットも捕捉
            _callWndHook = SetWindowsHookEx(4, _callWndDelegate, IntPtr.Zero, tid);
        }
        catch { _isDllPresent = false; }
    }

    private IntPtr OnGetMessage(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && lParam != IntPtr.Zero && _hCtx != IntPtr.Zero)
        {
            try
            {
                var m = Marshal.PtrToStructure<HOOKMSG>(lParam);
                if (m.message == _wtPacketMessage)
                    ProcessPacket((uint)m.wParam, "GetMsg");
            }
            catch { }
        }
        return CallNextHookEx(_getMsgHook, code, wParam, lParam);
    }

    private IntPtr OnCallWndProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && lParam != IntPtr.Zero && _hCtx != IntPtr.Zero)
        {
            try
            {
                var m = Marshal.PtrToStructure<CWPSTRUCT>(lParam);
                if (m.message == _wtPacketMessage)
                    ProcessPacket((uint)m.wParam, "CallWnd");
            }
            catch { }
        }
        return CallNextHookEx(_callWndHook, code, wParam, lParam);
    }

    private void ProcessPacket(uint serial, string hookType)
    {
        var pkt = new WinTabPacket();
        if (!WTPacket(_hCtx, serial, ref pkt)) return;

        CurrentPressure = _maxPressure > 0
            ? Math.Clamp(pkt.NormalPressure / (double)_maxPressure, 0.0, 1.0)
            : 1.0;
        // カーソル番号の奇数/偶数による消しゴム判定は Cintiq では不正確
        // （ペン先カーソルが1=奇数になりペンが消しゴムと誤検出される）
        // 安全のため常に false とし、WPF の StylusDevice.Inverted に任せる
        CurrentIsEraser = false;

        // Y 軸補正: Cintiq は物理 Y=0 がタブレット下端にあるため反転している
        // screen_Y = 2*lcOutOrgY + lcOutExtY - pkt.Y
        int sy = 2 * _lcOutOrgY + _lcOutExtY - pkt.Y;
        SetCursorPos(pkt.X, sy);

        bool isDown = (pkt.Buttons & 1) != 0 || pkt.NormalPressure > 0;
        PacketReceived?.Invoke(new WinTabInputArgs(pkt.X, sy, CurrentPressure, isDown, CurrentIsEraser));
    }

    private static uint ReadMaxPressure(uint deviceIndex)
    {
        const uint DVC_NPRESSURE = 6;
        const uint CSR_NPRESSURE = 22;
        const uint axisSize = 16; // sizeof(AXIS) = 4+4+4+4

        unsafe
        {
            var axis = new WinTabNative.AXIS();

            // 1. コンテキストに対応するデバイス（lcDevice）を優先して照会
            for (uint d = deviceIndex; d < deviceIndex + 4; d++)
            {
                uint sz = WTInfo(WTI_DEVICES + d, DVC_NPRESSURE, (IntPtr)(&axis));
                if (sz >= axisSize && axis.axMax > 0)
                    return (uint)axis.axMax;
            }

            // 2. すべてのデバイス（0〜15）を試す
            for (uint d = 0; d < 16; d++)
            {
                uint sz = WTInfo(WTI_DEVICES + d, DVC_NPRESSURE, (IntPtr)(&axis));
                if (sz >= axisSize && axis.axMax > 0)
                    return (uint)axis.axMax;
            }

            // 3. すべてのカーソル（0〜31）を試す
            for (uint c = 0; c < 32; c++)
            {
                uint sz = WTInfo(WTI_CURSORS + c, CSR_NPRESSURE, (IntPtr)(&axis));
                if (sz >= axisSize && axis.axMax > 0)
                    return (uint)axis.axMax;
            }
        }

        // フォールバック: 1023 → 8191 に変更（現代の Wacom は 8192 段階が標準）
        // raw:7780 が観測されたため 1023 は小さすぎる
        return 8191;
    }

    public void Dispose()
    {
        if (_getMsgHook  != IntPtr.Zero) { UnhookWindowsHookEx(_getMsgHook);  _getMsgHook  = IntPtr.Zero; }
        if (_callWndHook != IntPtr.Zero) { UnhookWindowsHookEx(_callWndHook); _callWndHook = IntPtr.Zero; }
        if (_hCtx != IntPtr.Zero) { WTClose(_hCtx); _hCtx = IntPtr.Zero; }
    }
}
