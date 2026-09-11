using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace WindowsTimeSync;

static unsafe class Program
{
    private const string WindowClassName = "WTSyncInternalClass";
    private static nint hStaticTime;
    private static nint hComboTz;
    private static nint hDatePicker;
    private static nint hBtnSync;
    private static int timerTickCount = 0;

    private const byte XorKey = 0x5A;

    // Chuẩn xác tuyệt đối: "2D04BFE282CD561338F9801D39AE16AD43E743BD" (XOR 0x5A)
    private static readonly byte[] EncryptedThumbprint =
    [
        0x68, 0x1E, 0x6A, 0x6E, 0x18, 0x1C, 0x1F, 0x68, 0x62, 0x68,
        0x19, 0x1E, 0x6F, 0x6C, 0x6B, 0x69, 0x69, 0x62, 0x1C, 0x63,
        0x62, 0x6A, 0x6B, 0x1E, 0x69, 0x63, 0x1B, 0x1F, 0x6B, 0x6C,
        0x1B, 0x1E, 0x6E, 0x69, 0x1F, 0x6D, 0x6E, 0x69, 0x18, 0x1E
    ];

    // Chuỗi lệnh nhạy cảm được mã hóa XOR 0x5A
    // tzutil.exe
    private static readonly byte[] EncCmdTzUtil = [0x2E, 0x20, 0x2F, 0x2E, 0x33, 0x36, 0x74, 0x3F, 0x22, 0x3F];
    // w32tm.exe
    private static readonly byte[] EncCmdW32Tm = [0x2D, 0x69, 0x68, 0x2E, 0x37, 0x74, 0x3F, 0x22, 0x3F];
    // net.exe
    private static readonly byte[] EncCmdNet = [0x34, 0x3F, 0x2E, 0x74, 0x3F, 0x22, 0x3F];
    // sc.exe
    private static readonly byte[] EncCmdSc = [0x29, 0x39, 0x74, 0x3F, 0x22, 0x3F];

    private static readonly (string Label, string TzId)[] SupportedZones =
    [
        ("UTC+07:00 - Vietnam, Bangkok, Jakarta", "SE Asia Standard Time"),
        ("UTC+08:00 - Singapore, Beijing, Taipei", "Singapore Standard Time"),
        ("UTC+09:00 - Tokyo, Seoul", "Tokyo Standard Time"),
        ("UTC+00:00 - London, Dublin, Lisbon", "GMT Standard Time"),
        ("UTC-05:00 - New York, Washington, Miami", "Eastern Standard Time"),
        ("UTC-08:00 - Los Angeles, San Francisco", "Pacific Standard Time")
    ];

    #region Win32 API Imports
    [DllImport("kernel32.dll")]
    private static extern bool IsDebuggerPresent();

    [DllImport("kernel32.dll")]
    private static extern bool CheckRemoteDebuggerPresent(nint hProcess, out bool isDebuggerPresent);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptQueryObject(
        uint dwObjectType,
        [MarshalAs(UnmanagedType.LPWStr)] string pvObject,
        uint dwExpectedContentTypeFlags,
        uint dwExpectedFormatTypeFlags,
        uint dwFlags,
        out uint pdwMsgAndCertEncodingType,
        out uint pdwContentType,
        out uint pdwFormatType,
        out nint phCertStore,
        out nint phMsg,
        out nint ppvContext);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern nint CertEnumCertificatesInStore(nint hCertStore, nint pPrevCertContext);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CertFreeCertificateContext(nint pCertContext);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CertCloseStore(nint hCertStore, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, void* lpParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(in MSG lpMsg);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DispatchMessageW(in MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(in WNDCLASSEX lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowTextW(nint hWnd, string lpString);

    [DllImport("user32.dll")]
    private static extern uint SetTimer(nint hWnd, nint nIDEvent, uint uElapse, void* lpTimerFunc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string lpText, string lpCaption, uint uType);

    [DllImport("user32.dll")]
    private static extern bool EnableWindow(nint hWnd, bool bEnable);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateFontW(int cHeight, int cWidth, int cEscapement, int cOrientation, int cWeight, uint bItalic, uint bUnderline, uint bStrikeOut, uint iCharSet, uint iOutPrecision, uint iClipPrecision, uint iQuality, uint iPitchAndFamily, string pszFaceName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessageW(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessageW(nint hWnd, uint Msg, nint wParam, string lParam);

    [DllImport("comctl32.dll")]
    private static extern void InitCommonControls();
    #endregion

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public nint hwnd; public uint message; public nint wParam; public nint lParam; public uint time; public POINT pt; }

    private delegate nint WndProcDel(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    private static readonly WndProcDel StaticWndProc = CustomWndProc;

    [STAThread]
    static void Main()
    {
        // 1. Chống Debugger
        if (DetectDebugger())
        {
            Environment.Exit(0);
            return;
        }

        // 2. Xác thực tính toàn vẹn chữ ký Authenticode
        string expectedThumb = DecryptString(EncryptedThumbprint, XorKey);
        if (!VerifySelfSignature(expectedThumb))
        {
            MessageBoxW(nint.Zero, "File chua duoc ky so hoac chu ky khong hop le!", "Loi Bao Mat", 0x10);
            Environment.Exit(0);
            return;
        }

        // 3. Đảm bảo quyền Administrator
        if (!IsRunAsAdmin())
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = Environment.ProcessPath ?? "",
                    Verb = "runas"
                });
            }
            catch { }
            return;
        }

        InitCommonControls();

        WNDCLASSEX wc = new()
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(StaticWndProc),
            lpszClassName = WindowClassName,
            hbrBackground = (nint)16 // COLOR_BTNFACE
        };
        RegisterClassExW(in wc);

        nint hWnd = CreateWindowExW(
            0,
            WindowClassName,
            "Đồng Bộ Giờ Chuẩn Windows",
            0x00CA0000 | 0x10000000,
            450, 250, 440, 255,
            0, 0, 0, null);

        nint hNormalFont = CreateFontW(17, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        nint hBoldFont = CreateFontW(22, 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");

        hStaticTime = CreateWindowExW(0, "STATIC", DateTime.Now.ToString("HH:mm:ss - dd/MM/yyyy"), 0x50000001, 15, 15, 395, 30, hWnd, 0, 0, null);
        SendMessageW(hStaticTime, 0x0030, hBoldFont, 1);

        nint hLblTz = CreateWindowExW(0, "STATIC", "Múi giờ:", 0x50000000, 25, 58, 65, 20, hWnd, 0, 0, null);
        SendMessageW(hLblTz, 0x0030, hNormalFont, 1);

        hComboTz = CreateWindowExW(0, "COMBOBOX", "", 0x50000000 | 0x0003 | 0x0200, 95, 54, 300, 200, hWnd, (nint)201, 0, null);
        SendMessageW(hComboTz, 0x0030, hNormalFont, 1);
        for (int i = 0; i < SupportedZones.Length; i++)
        {
            SendMessageW(hComboTz, 0x0143, 0, SupportedZones[i].Label);
        }
        SendMessageW(hComboTz, 0x014E, 0, 0);

        nint hLblDate = CreateWindowExW(0, "STATIC", "Lịch ngày:", 0x50000000, 25, 96, 70, 20, hWnd, 0, 0, null);
        SendMessageW(hLblDate, 0x0030, hNormalFont, 1);

        hDatePicker = CreateWindowExW(0, "SysDateTimePick32", "", 0x50000000 | 0x0004, 95, 92, 240, 26, hWnd, (nint)202, 0, null);
        SendMessageW(hDatePicker, 0x0030, hNormalFont, 1);

        hBtnSync = CreateWindowExW(0, "BUTTON", "Đồng Bộ Ngay", 0x50000000 | 0x0001, 120, 142, 185, 42, hWnd, (nint)101, 0, null);
        SendMessageW(hBtnSync, 0x0030, hNormalFont, 1);

        SetTimer(hWnd, 1, 1000, null);

        ShowWindow(hWnd, 1);
        UpdateWindow(hWnd);

        while (GetMessageW(out MSG msg, 0, 0, 0) > 0)
        {
            TranslateMessage(in msg);
            DispatchMessageW(in msg);
        }
    }

    private static nint CustomWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case 0x0113:
                SetWindowTextW(hStaticTime, DateTime.Now.ToString("HH:mm:ss - dd/MM/yyyy"));
                
                // Kiểm tra chữ ký số định kỳ ngầm
                timerTickCount++;
                if (timerTickCount % 20 == 0)
                {
                    if (!VerifySelfSignature(DecryptString(EncryptedThumbprint, XorKey)))
                    {
                        Environment.Exit(0);
                    }
                }
                break;

            case 0x0111:
                int cmdId = (int)wParam & 0xFFFF;
                if (cmdId == 101) ExecuteSyncRoutine(hWnd);
                break;

            case 0x0010:
            case 0x0002:
                PostQuitMessage(0);
                break;

            default:
                return DefWindowProcW(hWnd, msg, wParam, lParam);
        }
        return 0;
    }

    private static void ExecuteSyncRoutine(nint hWnd)
    {
        if (!VerifySelfSignature(DecryptString(EncryptedThumbprint, XorKey)))
        {
            Environment.Exit(0);
            return;
        }

        int selIndex = (int)SendMessageW(hComboTz, 0x0147, 0, 0);
        if (selIndex < 0 || selIndex >= SupportedZones.Length) selIndex = 0;
        string chosenTzId = SupportedZones[selIndex].TzId;

        EnableWindow(hBtnSync, false);
        SetWindowTextW(hBtnSync, "Đang đồng bộ...");

        new Thread(() =>
        {
            string tzutil = DecryptString(EncCmdTzUtil, XorKey);
            string w32tm = DecryptString(EncCmdW32Tm, XorKey);
            string net = DecryptString(EncCmdNet, XorKey);
            string sc = DecryptString(EncCmdSc, XorKey);

            DispatchCmd(tzutil, $"/s \"{chosenTzId}\"");

            DispatchCmd(net, "stop w32time");
            DispatchCmd(w32tm, "/unregister");
            DispatchCmd(w32tm, "/register");
            DispatchCmd(sc, "config w32time start= auto");
            DispatchCmd(net, "start w32time");

            DispatchCmd(w32tm, "/config /manualpeerlist:\"time.google.com,0x8 time.windows.com,0x8 pool.ntp.org,0x8\" /syncfromflags:manual /reliable:yes /update");
            Thread.Sleep(1200);

            int code = DispatchCmd(w32tm, "/resync /force");
            if (code != 0)
            {
                Thread.Sleep(1000);
                code = DispatchCmd(w32tm, "/resync /rediscover");
            }

            EnableWindow(hBtnSync, true);
            SetWindowTextW(hBtnSync, "Đồng Bộ Ngay");

            if (code == 0)
            {
                MessageBoxW(hWnd, "Đồng bộ giờ và múi giờ thành công!", "Thành công", 0x40);
            }
            else
            {
                MessageBoxW(hWnd, "Không thể kết nối máy chủ NTP. Vui lòng kiểm tra lại mạng hoặc cổng UDP 123.", "Lỗi", 0x30);
            }
        })
        { IsBackground = true }.Start();
    }

    private static int DispatchCmd(string app, string param)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo(app, param)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            proc?.WaitForExit(8000);
            return proc?.ExitCode ?? -1;
        }
        catch { return -1; }
    }

    private static bool IsRunAsAdmin()
    {
        try
        {
            WindowsPrincipal principal = new(WindowsIdentity.GetCurrent());
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private static bool DetectDebugger()
    {
        try
        {
            if (IsDebuggerPresent()) return true;
            CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, out bool remote);
            return remote;
        }
        catch { return false; }
    }

    // Đọc chữ ký số 2 tầng: kết hợp cả .NET Managed và Win32 Crypt32 API
    private static bool VerifySelfSignature(string expectedThumbprint)
    {
        try
        {
            string? currentPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentPath) || !File.Exists(currentPath))
                return false;

            string foundThumbprint = string.Empty;

            // Tầng 1: Managed Authenticode Parser
            try
            {
                using X509Certificate rawCert = X509Certificate.CreateFromSignedFile(currentPath);
                using X509Certificate2 cert2 = new(rawCert);
                foundThumbprint = cert2.Thumbprint ?? string.Empty;
            }
            catch { }

            // Tầng 2: Native Win32 Crypt32 Parser (Dự phòng cho Single-File)
            if (string.IsNullOrEmpty(foundThumbprint))
            {
                nint hCertStore = nint.Zero;
                nint pCertContext = nint.Zero;
                try
                {
                    if (CryptQueryObject(1, currentPath, 0x00003FFE, 0x0000000E, 0, out _, out _, out _, out hCertStore, out _, out _))
                    {
                        if (hCertStore != nint.Zero)
                        {
                            pCertContext = CertEnumCertificatesInStore(hCertStore, nint.Zero);
                            if (pCertContext != nint.Zero)
                            {
                                using X509Certificate2 cert = new(pCertContext);
                                foundThumbprint = cert.Thumbprint ?? string.Empty;
                            }
                        }
                    }
                }
                finally
                {
                    if (pCertContext != nint.Zero) CertFreeCertificateContext(pCertContext);
                    if (hCertStore != nint.Zero) CertCloseStore(hCertStore, 0);
                }
            }

            return string.Equals(foundThumbprint.Trim(), expectedThumbprint.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string DecryptString(byte[] data, byte key)
    {
        char[] result = new char[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (char)(data[i] ^ key);
        }
        return new string(result);
    }
}