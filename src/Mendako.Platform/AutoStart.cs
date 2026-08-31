using Microsoft.Win32;

namespace Mendako.Platform;

/// <summary>
/// Windows のログオン時に自動起動する設定。HKCU なので管理者権限は要らない。
/// </summary>
public static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string ValueName = "Mendako";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public static void Enable(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Run キーを開けませんでした。");

        // パスに空白が含まれても壊れないよう引用符で囲む。
        key.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key?.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static void Set(bool enabled, string executablePath)
    {
        if (enabled)
        {
            Enable(executablePath);
        }
        else
        {
            Disable();
        }
    }
}
