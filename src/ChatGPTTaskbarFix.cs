using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class ChatGPTTaskbarFix {
    const string StopName = @"Local\OpenAI_Codex_TaskbarCompat_v1_Stop";
    const string MutexName = @"Local\OpenAI_Codex_TaskbarCompat_v1_Mutex";
    const string PropertyName = "NonRudeHWND";
    static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatGPTTaskbarFix");
    static readonly Regex AllowedPath = new Regex(@"^C:\\Program Files\\WindowsApps\\OpenAI\.Codex_[0-9.]+_(?:x64|arm64)__2p2nqsd0c76g0\\app\\ChatGPT\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Dictionary<IntPtr, TaggedWindow> Tagged = new Dictionary<IntPtr, TaggedWindow>();
    static WinEventProc Callback = OnWindowEvent;
    static IntPtr ForegroundHook, ShowHook;
    static EventWaitHandle StopSignal;
    static int ApplyCount;

    class TaggedWindow { public uint ProcessId; public IntPtr Previous; }
    delegate bool EnumProc(IntPtr hwnd, IntPtr parameter);
    delegate void WinEventProc(IntPtr hook, uint eventId, IntPtr hwnd, int objectId, int childId, uint threadId, uint time);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int length);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetProp(IntPtr hwnd, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetProp(IntPtr hwnd, string name, IntPtr value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr RemoveProp(IntPtr hwnd, string name);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWinEventHook(uint minimum, uint maximum, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int length);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [ComImport, Guid("602D4995-B13A-429B-A66E-1935E44F4317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList2 {
        [PreserveSig] int HrInit();
        [PreserveSig] int AddTab(IntPtr hwnd);
        [PreserveSig] int DeleteTab(IntPtr hwnd);
        [PreserveSig] int ActivateTab(IntPtr hwnd);
        [PreserveSig] int SetActiveAlt(IntPtr hwnd);
        [PreserveSig] int MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
    }

    static string Executable(uint processId) {
        IntPtr process = OpenProcess(0x1000, false, processId);
        if (process == IntPtr.Zero) return null;
        try {
            var value = new StringBuilder(32768); int length = value.Capacity;
            return QueryFullProcessImageName(process, 0, value, ref length) ? value.ToString() : null;
        } finally { CloseHandle(process); }
    }
    static int RefreshShell(IntPtr hwnd) {
        object instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("56FDF344-FD6D-11D0-958A-006097C9A090")));
        try {
            var taskbar = (ITaskbarList2)instance;
            int result = taskbar.HrInit();
            if (result < 0) return result;
            return taskbar.MarkFullscreenWindow(hwnd, false);
        } finally { Marshal.FinalReleaseComObject(instance); }
    }
    static void Log(string text) {
        try {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "events.log");
            if (File.Exists(path) && new FileInfo(path).Length > 131072) File.WriteAllText(path, "");
            File.AppendAllText(path, DateTimeOffset.Now.ToString("o") + " " + text + Environment.NewLine, Encoding.UTF8);
        } catch { }
    }
    static void SaveStatus(object status) {
        try {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "status.json"), new JavaScriptSerializer().Serialize(status), Encoding.UTF8);
        } catch { }
    }
    static void Apply(IntPtr hwnd, string trigger) {
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || GetAncestor(hwnd, 2) != hwnd) return;
        var className = new StringBuilder(256); GetClassName(hwnd, className, className.Capacity);
        if (className.ToString() != "Chrome_WidgetWin_1" || (GetWindowLong(hwnd, -16) & 0x10000) == 0) return;
        uint processId; GetWindowThreadProcessId(hwnd, out processId);
        TaggedWindow prior;
        if (Tagged.TryGetValue(hwnd, out prior) && prior.ProcessId == processId && GetProp(hwnd, PropertyName) == new IntPtr(1)) return;
        string path = Executable(processId);
        if (path == null || !AllowedPath.IsMatch(path)) return;
        IntPtr previous = GetProp(hwnd, PropertyName);
        if (!SetProp(hwnd, PropertyName, new IntPtr(1))) {
            Log("Apply failed; pid=" + processId + "; win32=" + Marshal.GetLastWin32Error()); return;
        }
        Tagged[hwnd] = new TaggedWindow { ProcessId = processId, Previous = previous };
        int result = RefreshShell(hwnd);
        ApplyCount++;
        Log("Applied; pid=" + processId + "; hwnd=" + hwnd.ToInt64() + "; trigger=" + trigger + "; shell=" + result);
        SaveStatus(new { state = "running", helperPid = Process.GetCurrentProcess().Id, appliedAt = DateTimeOffset.Now.ToString("o"), targetPid = processId, windowHandle = hwnd.ToInt64(), targetPath = path, propertyValue = GetProp(hwnd, PropertyName).ToInt64(), shellHResult = result, applyCount = ApplyCount, trigger = trigger });
    }
    static void OnWindowEvent(IntPtr hook, uint eventId, IntPtr hwnd, int objectId, int childId, uint threadId, uint time) {
        if (eventId == 0x8002 && (objectId != 0 || childId != 0)) return;
        try { Apply(hwnd, eventId == 3 ? "foreground" : "window-show"); }
        catch (Exception error) { Log("Window callback error: " + error.GetType().Name + ": " + error.Message); }
    }
    static void Restore() {
        foreach (var entry in Tagged) {
            IntPtr hwnd = entry.Key; var saved = entry.Value;
            if (!IsWindow(hwnd)) continue;
            uint processId; GetWindowThreadProcessId(hwnd, out processId);
            if (processId != saved.ProcessId || GetProp(hwnd, PropertyName) != new IntPtr(1)) continue;
            if (saved.Previous == IntPtr.Zero) RemoveProp(hwnd, PropertyName);
            else SetProp(hwnd, PropertyName, saved.Previous);
            try { RefreshShell(hwnd); } catch { }
        }
        Tagged.Clear();
    }
    [STAThread]
    static int Main(string[] args) {
        if (args.Length == 1 && args[0] == "--stop") {
            try { using (var signal = EventWaitHandle.OpenExisting(StopName)) signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            return 0;
        }
        bool created;
        using (var mutex = new Mutex(true, MutexName, out created)) {
            if (!created) return 0;
            try {
                StopSignal = new EventWaitHandle(false, EventResetMode.ManualReset, StopName);
                StopSignal.Reset();
                ForegroundHook = SetWinEventHook(3, 3, IntPtr.Zero, Callback, 0, 0, 2);
                ShowHook = SetWinEventHook(0x8002, 0x8002, IntPtr.Zero, Callback, 0, 0, 2);
                if (ForegroundHook == IntPtr.Zero || ShowHook == IntPtr.Zero) throw new InvalidOperationException("Window notification registration failed.");
                Log("Started; helperPid=" + Process.GetCurrentProcess().Id);
                SaveStatus(new { state = "running", helperPid = Process.GetCurrentProcess().Id, startedAt = DateTimeOffset.Now.ToString("o"), applyCount = 0 });
                EnumWindows(delegate(IntPtr hwnd, IntPtr unused) { Apply(hwnd, "initial-scan"); return true; }, IntPtr.Zero);
                using (var timer = new System.Windows.Forms.Timer()) {
                    timer.Interval = 1000;
                    timer.Tick += delegate {
                        if (StopSignal.WaitOne(0)) Application.ExitThread();
                        var stale = new List<IntPtr>();
                        foreach (var entry in Tagged) if (!IsWindow(entry.Key)) stale.Add(entry.Key);
                        foreach (var hwnd in stale) Tagged.Remove(hwnd);
                    };
                    timer.Start();
                    Application.Run();
                }
                return 0;
            } catch (Exception error) {
                Log("Fatal: " + error.GetType().Name + ": " + error.Message); return 1;
            } finally {
                if (ForegroundHook != IntPtr.Zero) UnhookWinEvent(ForegroundHook);
                if (ShowHook != IntPtr.Zero) UnhookWinEvent(ShowHook);
                Restore();
                SaveStatus(new { state = "stopped", helperPid = Process.GetCurrentProcess().Id, stoppedAt = DateTimeOffset.Now.ToString("o"), applyCount = ApplyCount });
                Log("Stopped; window annotations restored.");
                if (StopSignal != null) StopSignal.Dispose();
                mutex.ReleaseMutex();
                GC.KeepAlive(Callback);
            }
        }
    }
}
