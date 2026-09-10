using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;

namespace Aye;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string KeyboardKey = @"Control Panel\Keyboard";

    public static void EnsureConfigured()
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executable))
            {
                using var run = Registry.CurrentUser.CreateSubKey(RunKey);
                run?.SetValue("Aye", $"\"{executable}\"", RegistryValueKind.String);
            }

            // Windows 10/11 otherwise reserves Print Screen for Snipping Tool.
            using var keyboard = Registry.CurrentUser.CreateSubKey(KeyboardKey);
            keyboard?.SetValue("PrintScreenKeyForSnippingEnabled", 0, RegistryValueKind.DWord);
        }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
    }
}

internal sealed class PrintScreenHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkSnapshot = 0x2C;

    private readonly Action _capture;
    private readonly HookProc _callback;
    private nint _hook;
    private bool _keyIsDown;

    public bool IsInstalled => _hook != 0;

    public PrintScreenHook(Action capture)
    {
        _capture = capture;
        _callback = OnKeyboardEvent;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = module is null ? 0 : GetModuleHandle(module.ModuleName);
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, moduleHandle, 0);
    }

    private nint OnKeyboardEvent(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var key = Marshal.ReadInt32(data);
            if (key == VkSnapshot)
            {
                var messageId = (int)message;
                if (messageId is WmKeyDown or WmSysKeyDown)
                {
                    if (!_keyIsDown)
                    {
                        _keyIsDown = true;
                        _capture();
                    }
                    return 1;
                }
                if (messageId is WmKeyUp or WmSysKeyUp)
                {
                    _keyIsDown = false;
                    return 1;
                }
            }
        }
        return CallNextHookEx(_hook, code, message, data);
    }

    public void Dispose()
    {
        if (_hook == 0)
            return;
        UnhookWindowsHookEx(_hook);
        _hook = 0;
        GC.SuppressFinalize(this);
    }

    ~PrintScreenHook() => Dispose();

    private delegate nint HookProc(int code, nint message, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc callback, nint module, uint threadId);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);
}
