// ============================================================
//  静音卫士 —— 常驻后台的静音守护（不是屏保）
//
//  触发静音（可勾选多个，任一满足即静音）：
//    A. 键鼠无动作 N 分钟（可自定义）
//    B. 系统屏保启动（时间由屏保设置决定）
//    C. 屏幕关闭（显示器断电）
//    D. 锁屏 Win+L
//
//  恢复音量（默认口径）：
//    必须同时满足 —— 显示器点亮 + 会话已解锁 + 检测到真实键鼠输入，
//    并且持续稳定 1.5 秒以上（避开"屏保退出→锁屏"的时间差）。
//
//  编译：
//    csc.exe /codepage:65001 /target:winexe /platform:anycpu /optimize+
//           /win32icon:静音卫士.ico
//           /r:System.dll /r:System.Core.dll /r:System.Drawing.dll
//           /r:System.Windows.Forms.dll /out:静音卫士.exe 静音卫士.cs
//
//  参数：
//    (无)            启动守护（托盘常驻）
//    /settings       直接打开设置窗口
//    /test <log>     自检：静音 2 秒后恢复
//    /q <log>        把当前音量/静音状态写进日志（测试用）
//    /u              兜底解除静音
//    /install        开启开机自启
//    /uninstall      关闭开机自启
// ============================================================

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("静音卫士")]
[assembly: System.Reflection.AssemblyProduct("静音卫士")]
[assembly: System.Reflection.AssemblyDescription("屏保 / 锁屏 / 关屏 / 空闲时自动静音，回到桌面后恢复原音量")]
[assembly: System.Reflection.AssemblyCompany("")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace MuteGuard
{
    internal static class Program
    {
        internal const string AppName = "静音卫士";

        [STAThread]
        private static void Main(string[] rawArgs)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Audio.Restore();
                Log.Write("FATAL: " + e.ExceptionObject);
            };
            AppDomain.CurrentDomain.ProcessExit += delegate { Audio.Restore(); };

            string mode = "";
            string param = "";
            if (rawArgs.Length > 0)
            {
                string a = rawArgs[0].Trim();
                if (a.Length > 0 && (a[0] == '/' || a[0] == '-')) a = a.Substring(1);
                string lower = a.ToLowerInvariant();
                if (lower == "test") mode = "test";
                else if (lower == "query" || lower == "q") mode = "q";
                else if (lower == "unmute" || lower == "u") mode = "u";
                else if (lower == "install") mode = "install";
                else if (lower == "uninstall") mode = "uninstall";
                else if (lower == "settings") mode = "settings";
                else mode = lower;
                if (rawArgs.Length > 1) param = rawArgs[1].Trim();
            }

            switch (mode)
            {
                case "test":
                    SelfTest(param);
                    return;
                case "q":
                    Query(param);
                    return;
                case "u":
                    Audio.Unmute();
                    return;
                case "install":
                    SetAutoStart(true);
                    return;
                case "uninstall":
                    SetAutoStart(false);
                    return;
                case "settings":
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (SettingsForm f = new SettingsForm(Config.Load()))
                    {
                        f.ShowDialog();
                    }
                    return;
            }

            // ---- 守护模式：单实例 ----
            bool createdNew;
            Mutex mutex = new Mutex(true, "MuteGuard_SingleInstance_v1", out createdNew);
            if (!createdNew)
            {
                Log.Write("another instance is already running, exit");
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Config cfg = Config.Load();
                App app = new App(cfg);
                Application.Run(app);
            }
            finally
            {
                try { mutex.ReleaseMutex(); }
                catch { }
            }
        }

        // ---------- 自检 ----------
        private static void SetAutoStart(bool on)
        {
            Config c = Config.Load();
            c.AutoStart = on;
            c.Save();
            AutoStart.Set(on);
            Log.Write("autostart set " + (on ? "on" : "off") + ", effective=" + AutoStart.IsOn());
        }

        private static void SelfTest(string logPath)
        {
            if (logPath.Length > 0) Log.Override = logPath;
            Log.Write("=== selftest start ===");
            float s1;
            bool m1;
            bool r1 = Audio.TryRead(out s1, out m1);
            Log.Write(string.Format("before: volume={0:P0} muted={1} readable={2}", s1, m1, r1));
            bool ok = Audio.Mute();
            Thread.Sleep(2000);
            float s2;
            bool m2;
            bool r2 = Audio.TryRead(out s2, out m2);
            Log.Write(string.Format("during: volume={0:P0} muted={1} readable={2} muteHr={3}", s2, m2, r2, ok));
            bool ok2 = Audio.Restore();
            Thread.Sleep(500);
            float s3;
            bool m3;
            bool r3 = Audio.TryRead(out s3, out m3);
            Log.Write(string.Format("after : volume={0:P0} muted={1} readable={2} restoreHr={3}", s3, m3, r3, ok2));
            bool pass = r1 && r2 && r3 && ok && ok2 && m2 && (m3 == m1)
                        && Math.Abs(s3 - s1) < 0.005f;
            Log.Write(pass ? "RESULT: PASS" : "RESULT: FAIL");
            Environment.ExitCode = pass ? 0 : 1;
        }

        private static void Query(string logPath)
        {
            if (logPath.Length > 0) Log.Override = logPath;
            float s;
            bool m;
            bool r = Audio.TryRead(out s, out m);
            Log.Write(string.Format("query: volume={0:P0} muted={1} readable={2}", s, m, r));
            Environment.ExitCode = r ? (m ? 2 : 0) : 1;
        }
    }

    // ============================================================
    //  日志
    // ============================================================
    internal static class Log
    {
        public static string Override = null;

        public static string DefaultPath
        {
            get { return Path.Combine(Config.Dir, "log.txt"); }
        }

        public static void Write(string msg)
        {
            try
            {
                string path = Override != null ? Override : DefaultPath;
                if (Override == null) Directory.CreateDirectory(Config.Dir);
                FileInfo fi = new FileInfo(path);
                if (fi.Exists && fi.Length > 1024 * 1024) fi.Delete();
                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n",
                    Encoding.UTF8);
            }
            catch { }
        }
    }

    // ============================================================
    //  配置
    // ============================================================
    internal sealed class Config
    {
        public bool UseIdle = false;
        public int IdleMinutes = 15;
        public bool UseScreensaver = true;
        public bool UseDisplayOff = true;
        public bool UseLock = true;

        // 开机自动启动（默认开启）
        public bool AutoStart = true;

        // 恢复条件
        public bool RestoreRequireUnlock = true;
        public bool RestoreRequireInput = true;
        public int MaxMuteHours = 8;   // 0 = 不限制

        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MuteGuard"); }
        }

        public static string FilePath
        {
            get { return Path.Combine(Dir, "config.ini"); }
        }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (!File.Exists(FilePath)) return c;
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "UseIdle": c.UseIdle = v == "1"; break;
                        case "IdleMinutes": c.IdleMinutes = Clamp(Int(v, 15), 1, 180); break;
                        case "UseScreensaver": c.UseScreensaver = v == "1"; break;
                        case "UseDisplayOff": c.UseDisplayOff = v == "1"; break;
                        case "UseLock": c.UseLock = v == "1"; break;
                        case "AutoStart": c.AutoStart = v == "1"; break;
                        case "RestoreRequireUnlock": c.RestoreRequireUnlock = v == "1"; break;
                        case "RestoreRequireInput": c.RestoreRequireInput = v == "1"; break;
                        case "MaxMuteHours": c.MaxMuteHours = Clamp(Int(v, 8), 0, 48); break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("config load error: " + ex.Message);
            }
            return c;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 静音卫士 配置");
                sb.AppendLine("UseIdle=" + B(UseIdle));
                sb.AppendLine("IdleMinutes=" + IdleMinutes);
                sb.AppendLine("UseScreensaver=" + B(UseScreensaver));
                sb.AppendLine("UseDisplayOff=" + B(UseDisplayOff));
                sb.AppendLine("UseLock=" + B(UseLock));
                sb.AppendLine("AutoStart=" + B(AutoStart));
                sb.AppendLine("RestoreRequireUnlock=" + B(RestoreRequireUnlock));
                sb.AppendLine("RestoreRequireInput=" + B(RestoreRequireInput));
                sb.AppendLine("MaxMuteHours=" + MaxMuteHours);
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.Write("config save error: " + ex.Message);
            }
        }

        private static string B(bool b) { return b ? "1" : "0"; }

        private static int Int(string s, int dflt)
        {
            int v;
            return int.TryParse(s, out v) ? v : dflt;
        }

        private static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }
    }

    // ============================================================
    //  音量（Core Audio 主音量 + 静音）
    // ============================================================
    internal static class Audio
    {
        private const int E_RENDER = 0;
        private const int E_MULTIMEDIA = 1;
        private const int CLSCTX_ALL = 0x17;

        private static readonly object Gate = new object();
        private static bool _captured;
        private static bool _weMuted;
        private static float _origScalar;
        private static bool _origMute;
        private static bool _muteFailed;

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            [PreserveSig] int OpenPropertyStore(int stgmAccess, out IntPtr properties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out int state);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out int count);
            [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
            [PreserveSig] int GetMasterVolumeLevel(out float level);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
            [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
            [PreserveSig] int VolumeStepUp(ref Guid context);
            [PreserveSig] int VolumeStepDown(ref Guid context);
            [PreserveSig] int QueryHardwareSupport(out uint mask);
            [PreserveSig] int GetVolumeRange(out float min, out float max, out float increment);
        }

        private static IAudioEndpointVolume Acquire()
        {
            IMMDeviceEnumerator en = null;
            IMMDevice dev = null;
            try
            {
                en = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
                int hr = en.GetDefaultAudioEndpoint(E_RENDER, E_MULTIMEDIA, out dev);
                if (hr != 0 || dev == null) throw new COMException("GetDefaultAudioEndpoint failed", hr);
                Guid iid = typeof(IAudioEndpointVolume).GUID;
                object obj;
                hr = dev.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out obj);
                if (hr != 0 || obj == null) throw new COMException("Activate failed", hr);
                return (IAudioEndpointVolume)obj;
            }
            finally
            {
                if (dev != null) Marshal.ReleaseComObject(dev);
                if (en != null) Marshal.ReleaseComObject(en);
            }
        }

        private static bool WithEndpoint(Func<IAudioEndpointVolume, bool> action)
        {
            IAudioEndpointVolume ep = null;
            try
            {
                ep = Acquire();
                return action(ep);
            }
            catch (Exception ex)
            {
                Log.Write("audio error: " + ex.Message);
                return false;
            }
            finally
            {
                if (ep != null) Marshal.ReleaseComObject(ep);
            }
        }

        public static bool TryRead(out float scalar, out bool mute)
        {
            float s = 0f;
            bool m = false;
            bool ok = WithEndpoint(delegate(IAudioEndpointVolume ep)
            {
                ep.GetMasterVolumeLevelScalar(out s);
                ep.GetMute(out m);
                return true;
            });
            scalar = s;
            mute = m;
            return ok;
        }

        // 静音并记住原始状态；返回是否成功
        public static bool Mute()
        {
            lock (Gate)
            {
                if (_weMuted) return true;
                bool ok = WithEndpoint(delegate(IAudioEndpointVolume ep)
                {
                    float s;
                    bool m;
                    ep.GetMasterVolumeLevelScalar(out s);
                    ep.GetMute(out m);
                    _origScalar = s;
                    _origMute = m;
                    _captured = true;
                    Guid ctx = Guid.Empty;
                    int hr = ep.SetMute(true, ref ctx);
                    Log.Write(string.Format("mute: saved volume={0:P0} muted={1} -> mute, hr={2}", s, m, hr));
                    return hr >= 0;
                });
                _weMuted = ok;
                if (!ok && !_muteFailed)
                {
                    _muteFailed = true;
                    Log.Write("mute FAILED (no readable default playback device?)");
                }
                if (ok) _muteFailed = false;
                return ok;
            }
        }

        // 恢复进入前的音量与静音状态（若用户期间手动改过，以用户为准）
        public static bool Restore()
        {
            lock (Gate)
            {
                if (!_captured) return true;
                bool ok = WithEndpoint(delegate(IAudioEndpointVolume ep)
                {
                    bool nowMute;
                    ep.GetMute(out nowMute);
                    if (!nowMute)
                    {
                        Log.Write("restore: user changed audio manually, keep current setting");
                        return true;
                    }
                    Guid ctx = Guid.Empty;
                    ep.SetMasterVolumeLevelScalar(_origScalar, ref ctx);
                    int hr = ep.SetMute(_origMute, ref ctx);
                    Log.Write(string.Format("restore: volume->{0:P0} muted->{1}, hr={2}", _origScalar, _origMute, hr));
                    return hr >= 0;
                });
                if (ok)
                {
                    _captured = false;
                    _weMuted = false;
                    _muteFailed = false;
                }
                return ok;
            }
        }

        public static bool Unmute()
        {
            lock (Gate)
            {
                bool ok = WithEndpoint(delegate(IAudioEndpointVolume ep)
                {
                    Guid ctx = Guid.Empty;
                    if (_captured) ep.SetMasterVolumeLevelScalar(_origScalar, ref ctx);
                    int hr = ep.SetMute(false, ref ctx);
                    Log.Write("unmute: hr=" + hr);
                    return hr >= 0;
                });
                if (ok)
                {
                    _captured = false;
                    _weMuted = false;
                }
                return ok;
            }
        }

        public static bool IsMuted { get { return _weMuted; } }

        public static float OriginalVolume { get { return _origScalar; } }
    }

    // ============================================================
    //  系统状态探测
    // ============================================================
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        // 系统最后输入时间（tick，毫秒）
        public static uint LastInputTick()
        {
            LASTINPUTINFO li = new LASTINPUTINFO();
            li.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref li)) return 0;
            return li.dwTime;
        }

        // 空闲秒数
        public static int IdleSeconds()
        {
            uint last = LastInputTick();
            if (last == 0) return 0;
            unchecked
            {
                uint diff = (uint)(Environment.TickCount - (int)last);
                return (int)(diff / 1000u);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, ref bool data, uint winIni);

        // 屏保是否正在运行（SPI_GETSCREENSAVERRUNNING）
        public static bool ScreenSaverRunning()
        {
            bool running = false;
            SystemParametersInfo(0x0072u, 0, ref running, 0);
            return running;
        }

        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int flags);

        [DllImport("wtsapi32.dll")]
        public static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid guid, int flags);

        [DllImport("user32.dll")]
        public static extern bool UnregisterPowerSettingNotification(IntPtr handle);

        public static readonly Guid GuidConsoleDisplayState = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");
        public static readonly Guid GuidSessionDisplayStatus = new Guid("2B84C20E-AD23-4DDF-93DB-05FFBD7EFCA5");
    }

    // ============================================================
    //  守护核心（状态机）
    // ============================================================
    internal sealed class Guard
    {
        private readonly Config _cfg;

        private bool _muted;
        private uint _inputAtMute;
        private DateTime _muteAt;
        private DateTime _eligibleSince = DateTime.MinValue;
        private DateTime _lastFailLog = DateTime.MinValue;

        private bool _locked;
        private bool _displayOn = true;
        private bool _paused;

        public event Action Changed;

        public Guard(Config cfg) { _cfg = cfg; }

        public bool Muted { get { return _muted; } }
        public bool Paused { get { return _paused; } }
        public bool Locked { get { return _locked; } }
        public bool DisplayOn { get { return _displayOn; } }
        public DateTime MuteAt { get { return _muteAt; } }

        // ---------- 条件判定 ----------
        private bool TriggerActive()
        {
            if (_cfg.UseIdle && Native.IdleSeconds() >= _cfg.IdleMinutes * 60) return true;
            if (_cfg.UseScreensaver && Native.ScreenSaverRunning()) return true;
            if (_cfg.UseDisplayOff && !_displayOn) return true;
            if (_cfg.UseLock && _locked) return true;
            return false;
        }

        private bool CanRestore()
        {
            if (!_displayOn) return false;                       // 屏幕没亮，不恢复
            if (Native.ScreenSaverRunning()) return false;        // 屏保还在跑，不恢复
            if (_cfg.RestoreRequireUnlock && _locked) return false; // 锁屏等解锁
            if (_cfg.RestoreRequireInput && Native.LastInputTick() == _inputAtMute) return false; // 还没人动
            return true;
        }

        // ---------- 主循环 ----------
        public void Tick()
        {
            DateTime now = DateTime.Now;

            if (_paused)
            {
                if (_muted) DoRestore("暂停监控");
                Raise();
                return;
            }

            if (!_muted)
            {
                if (TriggerActive()) DoMute();
            }
            else
            {
                if (CanRestore())
                {
                    if (_eligibleSince == DateTime.MinValue) _eligibleSince = now;
                    else if ((now - _eligibleSince).TotalSeconds >= 1.5) DoRestore("条件满足");
                }
                else
                {
                    _eligibleSince = DateTime.MinValue;
                }

                // 最长静音兜底：只在"离开条件都已解除、却仍然恢复不了"时才强制恢复
                // （避免长时间锁屏/关屏期间每个周期放声 1 秒）
                if (_muted && _cfg.MaxMuteHours > 0 && !TriggerActive() &&
                    (now - _muteAt).TotalHours >= _cfg.MaxMuteHours)
                {
                    DoRestore("兜底：无离开条件但恢复失败，超过 " + _cfg.MaxMuteHours + " 小时");
                }
            }

            Raise();
        }

        public void DoMute()
        {
            if (_muted) return;
            if (!Audio.Mute())
            {
                if ((DateTime.Now - _lastFailLog).TotalSeconds > 60)
                {
                    _lastFailLog = DateTime.Now;
                    Log.Write("mute skipped: no default playback device");
                }
                return;
            }
            _muted = true;
            _inputAtMute = Native.LastInputTick();
            _muteAt = DateTime.Now;
            _eligibleSince = DateTime.MinValue;
            Log.Write(string.Format("=== muted (idle={0}s ss={1} lock={2} display={3}) ===",
                Native.IdleSeconds(), Native.ScreenSaverRunning(), _locked, _displayOn));
            Raise();
        }

        private void DoRestore(string why)
        {
            if (!_muted) return;
            if (!Audio.Restore()) return;
            _muted = false;
            _eligibleSince = DateTime.MinValue;
            Log.Write("=== restore (" + why + ") ===");
            Raise();
        }

        public void ManualRestore()
        {
            DoRestore("手动恢复");
        }

        public void ManualMute()
        {
            DoMute();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (paused) DoRestore("进入暂停");
            Log.Write("paused=" + paused);
            Raise();
        }

        // ---------- 事件 ----------
        public void OnSessionChange(int evt)
        {
            switch (evt)
            {
                case 7:  // WTS_SESSION_LOCK
                    _locked = true;
                    Log.Write("session LOCK");
                    break;
                case 8:  // WTS_SESSION_UNLOCK
                    _locked = false;
                    Log.Write("session UNLOCK");
                    break;
                case 5:  // WTS_SESSION_LOGON
                    _locked = false;
                    break;
                case 6:  // WTS_SESSION_LOGOFF
                    if (_muted) DoRestore("注销");
                    break;
            }
            Raise();
        }

        public void OnDisplayState(int state)
        {
            bool on = state != 0;   // 0=off 1=on 2=dimmed；变暗不算关闭
            if (_displayOn != on)
            {
                _displayOn = on;
                Log.Write("display " + (on ? "ON" : "OFF") + " (state=" + state + ")");
            }
            Raise();
        }

        public void OnSystemPower(bool suspend)
        {
            if (suspend)
            {
                _displayOn = false;
                Log.Write("system suspend");
            }
            else
            {
                Log.Write("system resume");
            }
            Raise();
        }

        public void Shutdown()
        {
            DoRestore("程序退出");
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }
    }

    // ============================================================
    //  接收系统消息的隐藏窗口
    // ============================================================
    internal sealed class MsgForm : Form
    {
        public Guard Guard;

        public MsgForm()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            Size = new Size(8, 8);
            Text = "静音卫士";
        }

        protected override void WndProc(ref Message m)
        {
            try
            {
                if (m.Msg == 0x02B1 && Guard != null)          // WM_WTSSESSION_CHANGE
                {
                    Guard.OnSessionChange(m.WParam.ToInt32());
                }
                else if (m.Msg == 0x0218 && Guard != null)     // WM_POWERBROADCAST
                {
                    int wp = m.WParam.ToInt32();
                    if (wp == 0x8013)                          // PBT_POWERSETTINGCHANGE
                    {
                        Guid g = (Guid)Marshal.PtrToStructure(m.LParam, typeof(Guid));
                        uint len = (uint)Marshal.ReadInt32(m.LParam, 16);
                        if (len >= 1 && (g == Native.GuidConsoleDisplayState ||
                                         g == Native.GuidSessionDisplayStatus))
                        {
                            Guard.OnDisplayState(Marshal.ReadByte(m.LParam, 20));
                        }
                    }
                    else if (wp == 0x0004) Guard.OnSystemPower(true);   // PBT_APMSUSPEND
                    else if (wp == 0x0012) Guard.OnSystemPower(false);  // PBT_APMRESUMEAUTOMATIC
                    else if (wp == 0x0007) Guard.OnSystemPower(false);  // PBT_APMRESUME
                }
            }
            catch (Exception ex)
            {
                Log.Write("wndproc error: " + ex.Message);
            }
            base.WndProc(ref m);
        }
    }

    // ============================================================
    //  托盘 / 主程序
    // ============================================================
    internal sealed class App : ApplicationContext
    {
        private readonly Config _cfg;
        private readonly Guard _guard;
        private readonly NotifyIcon _tray;
        private readonly MsgForm _form;
        private readonly System.Windows.Forms.Timer _timer;
        private IntPtr _powerNotify;
        private ToolStripMenuItem _statusItem;
        private ToolStripMenuItem _toggleItem;
        private ToolStripMenuItem _pauseItem;
        private ToolStripMenuItem _autostartItem;

        private static Icon _iconNormal;
        private static Icon _iconMuted;
        private static Icon _iconPaused;

        public App(Config cfg)
        {
            _cfg = cfg;
            _guard = new Guard(cfg);
            _guard.Changed += RefreshUi;

            _form = new MsgForm();
            _form.Guard = _guard;
            _form.Show();
            Native.WTSRegisterSessionNotification(_form.Handle, 0);

            Guid g = Native.GuidConsoleDisplayState;
            _powerNotify = Native.RegisterPowerSettingNotification(_form.Handle, ref g, 0);
            if (_powerNotify == IntPtr.Zero)
                Log.Write("power notification register failed, err=" + Marshal.GetLastWin32Error());

            _iconNormal = MakeIcon(Color.FromArgb(45, 125, 246), false, false);
            _iconMuted = MakeIcon(Color.FromArgb(229, 57, 53), true, false);
            _iconPaused = MakeIcon(Color.FromArgb(120, 120, 120), false, true);

            _tray = new NotifyIcon();
            _tray.Icon = _iconNormal;
            _tray.Text = "静音卫士";
            _tray.Visible = true;
            _tray.ContextMenuStrip = BuildMenu();
            _tray.DoubleClick += delegate { ShowSettings(); };

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 1000;
            _timer.Tick += delegate { _guard.Tick(); };
            _timer.Start();

            // 按配置同步开机自启（默认开启；程序挪了位置也能自动修正路径）
            if (AutoStart.IsOn() != _cfg.AutoStart) AutoStart.Set(_cfg.AutoStart);

            Log.Write("静音卫士 启动; idle=" + _cfg.UseIdle + "/" + _cfg.IdleMinutes + "min ss="
                      + _cfg.UseScreensaver + " displayOff=" + _cfg.UseDisplayOff + " lock=" + _cfg.UseLock
                      + " autostart=" + _cfg.AutoStart);
            RefreshUi();
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            _statusItem = new ToolStripMenuItem("状态：监控中");
            _statusItem.Enabled = false;

            _toggleItem = new ToolStripMenuItem("立即静音");
            _toggleItem.Click += delegate
            {
                if (_guard.Muted) _guard.ManualRestore();
                else _guard.ManualMute();
            };

            _pauseItem = new ToolStripMenuItem("暂停监控");
            _pauseItem.CheckOnClick = true;
            _pauseItem.Click += delegate { _guard.SetPaused(_pauseItem.Checked); };

            ToolStripMenuItem settings = new ToolStripMenuItem("设置…");
            settings.Click += delegate { ShowSettings(); };

            _autostartItem = new ToolStripMenuItem("开机自动启动");
            _autostartItem.CheckOnClick = true;
            _autostartItem.Checked = _cfg.AutoStart;
            _autostartItem.Click += delegate
            {
                _cfg.AutoStart = _autostartItem.Checked;
                _cfg.Save();
                AutoStart.Set(_cfg.AutoStart);
            };

            ToolStripMenuItem logItem = new ToolStripMenuItem("打开日志");
            logItem.Click += delegate { OpenLog(); };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApp(); };

            menu.Items.Add(_statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_toggleItem);
            menu.Items.Add(_pauseItem);
            menu.Items.Add(settings);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_autostartItem);
            menu.Items.Add(logItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);
            return menu;
        }

        private void RefreshUi()
        {
            try
            {
                bool muted = _guard.Muted;
                bool paused = _guard.Paused;

                Icon icon = paused ? _iconPaused : (muted ? _iconMuted : _iconNormal);
                if (_tray.Icon != icon) _tray.Icon = icon;

                float vol;
                bool m;
                string volTxt = "音量 --";
                if (Audio.TryRead(out vol, out m)) volTxt = "音量 " + (int)Math.Round(vol * 100) + "%";

                string state;
                if (paused) state = "已暂停";
                else if (muted)
                {
                    DateTime t = _guard.MuteAt;
                    state = "已静音";
                    if (t != DateTime.MinValue) state += "（" + t.ToString("HH:mm:ss") + " 起）";
                }
                else state = "监控中";

                string tip = "静音卫士 · " + state + " · " + volTxt;
                if (tip.Length > 62) tip = tip.Substring(0, 62);
                _tray.Text = tip;

                _statusItem.Text = "状态：" + state + "   " + volTxt;
                _toggleItem.Text = muted ? "立即恢复音量" : "立即静音";
                if (_pauseItem.Checked != paused) _pauseItem.Checked = paused;
                if (_autostartItem != null) _autostartItem.Checked = _cfg.AutoStart;
            }
            catch (Exception ex)
            {
                Log.Write("refresh ui error: " + ex.Message);
            }
        }

        private void ShowSettings()
        {
            using (SettingsForm f = new SettingsForm(_cfg))
            {
                if (f.ShowDialog() == DialogResult.OK)
                {
                    _cfg.Save();
                    Log.Write("settings saved");
                    RefreshUi();
                }
            }
        }

        private static void OpenLog()
        {
            try
            {
                if (!File.Exists(Log.DefaultPath)) Log.Write("log opened");
                System.Diagnostics.Process.Start("notepad.exe", Log.DefaultPath);
            }
            catch (Exception ex)
            {
                Log.Write("open log error: " + ex.Message);
            }
        }

        private void ExitApp()
        {
            Log.Write("exit requested");
            _guard.Shutdown();
            _timer.Stop();
            Native.WTSUnRegisterSessionNotification(_form.Handle);
            if (_powerNotify != IntPtr.Zero) Native.UnregisterPowerSettingNotification(_powerNotify);
            _tray.Visible = false;
            _tray.Dispose();
            _form.Dispose();
            ExitThread();
        }

        // ---------- 托盘图标（代码绘制） ----------
        private static Icon MakeIcon(Color color, bool slash, bool pauseBars)
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillEllipse(b, 1, 1, 30, 30);
                }
                if (slash)
                {
                    using (Pen p = new Pen(Color.White, 3.0f))
                    {
                        g.DrawLine(p, 9, 9, 23, 23);
                    }
                }
                else if (pauseBars)
                {
                    using (SolidBrush w = new SolidBrush(Color.White))
                    {
                        g.FillRectangle(w, 11, 9, 4, 14);
                        g.FillRectangle(w, 18, 9, 4, 14);
                    }
                }
                else
                {
                    // 小喇叭：矩形 + 三角形
                    using (SolidBrush w = new SolidBrush(Color.White))
                    {
                        g.FillRectangle(w, 9, 13, 5, 7);
                    }
                    Point[] pts = new Point[] {
                        new Point(14, 13), new Point(20, 9), new Point(20, 23), new Point(14, 19)
                    };
                    using (SolidBrush w = new SolidBrush(Color.White))
                    {
                        g.FillPolygon(w, pts);
                    }
                }
            }
            IntPtr h = bmp.GetHicon();
            bmp.Dispose();
            return Icon.FromHandle(h);
        }
    }

    // ============================================================
    //  设置窗口
    // ============================================================
    internal sealed class SettingsForm : Form
    {
        private readonly Config _cfg;
        private CheckBox _cbIdle;
        private NumericUpDown _nudMinutes;
        private CheckBox _cbSs;
        private CheckBox _cbDisp;
        private CheckBox _cbLock;
        private CheckBox _cbReqUnlock;
        private CheckBox _cbReqInput;
        private CheckBox _cbMax;
        private NumericUpDown _nudMaxHours;
        private CheckBox _cbAuto;

        public SettingsForm(Config cfg)
        {
            _cfg = cfg;
            Text = "静音卫士 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(420, 392);
            Font = new Font("Microsoft YaHei UI", 9f);

            Label head = new Label();
            head.Text = "静音生效条件（勾选多个，任一满足即静音）";
            head.SetBounds(14, 12, 390, 20);
            Controls.Add(head);

            _cbIdle = new CheckBox();
            _cbIdle.Text = "键鼠无动作";
            _cbIdle.SetBounds(14, 38, 90, 22);
            _cbIdle.Checked = cfg.UseIdle;
            Controls.Add(_cbIdle);

            _nudMinutes = new NumericUpDown();
            _nudMinutes.Minimum = 1;
            _nudMinutes.Maximum = 180;
            _nudMinutes.Value = cfg.IdleMinutes;
            _nudMinutes.SetBounds(104, 38, 56, 22);
            Controls.Add(_nudMinutes);

            Label l1 = new Label();
            l1.Text = "分钟（1-180）";
            l1.SetBounds(166, 40, 120, 20);
            Controls.Add(l1);

            _cbSs = new CheckBox();
            _cbSs.Text = "屏保启动时（时间由屏保设置决定）";
            _cbSs.SetBounds(14, 64, 300, 22);
            _cbSs.Checked = cfg.UseScreensaver;
            Controls.Add(_cbSs);

            _cbDisp = new CheckBox();
            _cbDisp.Text = "屏幕关闭时（显示器断电）";
            _cbDisp.SetBounds(14, 90, 300, 22);
            _cbDisp.Checked = cfg.UseDisplayOff;
            Controls.Add(_cbDisp);

            _cbLock = new CheckBox();
            _cbLock.Text = "锁屏时（Win+L）";
            _cbLock.SetBounds(14, 116, 300, 22);
            _cbLock.Checked = cfg.UseLock;
            Controls.Add(_cbLock);

            Label head2 = new Label();
            head2.Text = "恢复音量条件";
            head2.SetBounds(14, 152, 300, 20);
            Controls.Add(head2);

            _cbReqUnlock = new CheckBox();
            _cbReqUnlock.Text = "必须先解除锁屏（屏保要求登录时推荐勾选）";
            _cbReqUnlock.SetBounds(14, 178, 390, 22);
            _cbReqUnlock.Checked = cfg.RestoreRequireUnlock;
            Controls.Add(_cbReqUnlock);

            _cbReqInput = new CheckBox();
            _cbReqInput.Text = "必须检测到真实键鼠输入";
            _cbReqInput.SetBounds(14, 204, 390, 22);
            _cbReqInput.Checked = cfg.RestoreRequireInput;
            Controls.Add(_cbReqInput);

            Label note = new Label();
            note.Text = "另外始终要求：显示器已点亮、屏保已退出，且稳定 1.5 秒。";
            note.ForeColor = Color.Gray;
            note.SetBounds(32, 228, 380, 20);
            Controls.Add(note);

            _cbMax = new CheckBox();
            _cbMax.Text = "最长静音";
            _cbMax.SetBounds(14, 258, 80, 22);
            _cbMax.Checked = cfg.MaxMuteHours > 0;
            Controls.Add(_cbMax);

            _nudMaxHours = new NumericUpDown();
            _nudMaxHours.Minimum = 0;
            _nudMaxHours.Maximum = 48;
            _nudMaxHours.Value = cfg.MaxMuteHours > 0 ? cfg.MaxMuteHours : 8;
            _nudMaxHours.SetBounds(96, 258, 56, 22);
            Controls.Add(_nudMaxHours);

            Label l2 = new Label();
            l2.Text = "小时后兜底恢复（仅卡死时生效）";
            l2.SetBounds(158, 260, 250, 20);
            Controls.Add(l2);

            _cbAuto = new CheckBox();
            _cbAuto.Text = "开机自动启动";
            _cbAuto.SetBounds(14, 296, 200, 22);
            _cbAuto.Checked = _cfg.AutoStart;
            Controls.Add(_cbAuto);

            Label l3 = new Label();
            l3.Text = "提示：主音量静音对独占/ASIO 播放器无效。";
            l3.ForeColor = Color.Gray;
            l3.SetBounds(14, 320, 390, 20);
            Controls.Add(l3);

            Button ok = new Button();
            ok.Text = "保存并应用";
            ok.SetBounds(206, 350, 100, 28);
            ok.Click += OkClick;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(314, 350, 90, 28);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void OkClick(object sender, EventArgs e)
        {
            _cfg.UseIdle = _cbIdle.Checked;
            _cfg.IdleMinutes = (int)_nudMinutes.Value;
            _cfg.UseScreensaver = _cbSs.Checked;
            _cfg.UseDisplayOff = _cbDisp.Checked;
            _cfg.UseLock = _cbLock.Checked;
            _cfg.RestoreRequireUnlock = _cbReqUnlock.Checked;
            _cfg.RestoreRequireInput = _cbReqInput.Checked;
            _cfg.MaxMuteHours = _cbMax.Checked ? (int)_nudMaxHours.Value : 0;
            _cfg.AutoStart = _cbAuto.Checked;
            _cfg.Save();
            if (AutoStart.IsOn() != _cfg.AutoStart) AutoStart.Set(_cfg.AutoStart);
            Log.Write(string.Format("settings applied: idle={0}/{1}min ss={2} displayOff={3} lock={4} reqUnlock={5} reqInput={6} max={7}h",
                _cfg.UseIdle, _cfg.IdleMinutes, _cfg.UseScreensaver, _cfg.UseDisplayOff, _cfg.UseLock,
                _cfg.RestoreRequireUnlock, _cfg.RestoreRequireInput, _cfg.MaxMuteHours));
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ============================================================
    //  开机自启（HKCU Run）
    // ============================================================
    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MuteGuard";

        private static string ExePath()
        {
            return Application.ExecutablePath;
        }

        public static bool IsOn()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    string v = (string)k.GetValue(ValueName);
                    return v != null && v.Trim('"').Equals(ExePath(), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        public static void Set(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return;
                    if (on) k.SetValue(ValueName, "\"" + ExePath() + "\"", RegistryValueKind.String);
                    else k.DeleteValue(ValueName, false);
                }
                Log.Write("autostart " + (on ? "on" : "off") + " -> " + ExePath());
            }
            catch (Exception ex)
            {
                Log.Write("autostart error: " + ex.Message);
            }
        }
    }
}
