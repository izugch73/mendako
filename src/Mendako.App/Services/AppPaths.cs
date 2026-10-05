using System;
using System.IO;

namespace Mendako.App.Services;

/// <summary>保存先のパス。ローミングさせたくないので LocalApplicationData に置く。</summary>
public static class AppPaths
{
    /// <summary>保存先を差し替える環境変数。開発中に、飼っているメンダコを巻き込まずに試すためのもの。</summary>
    public const string DataDirectoryVariable = "MENDAKO_DATA_DIR";

    public static string DataDirectory { get; } =
        Environment.GetEnvironmentVariable(DataDirectoryVariable) is { Length: > 0 } overridden
            ? Path.GetFullPath(overridden)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Mendako");

    public static string StateFile => Path.Combine(DataDirectory, "state.json");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    /// <summary>
    /// 自動起動の登録に使う exe のパス。
    /// 単一ファイルで発行すると <c>Assembly.Location</c> は常に空になるので、
    /// フォールバックには使わない (使うと IL3000 も出る)。
    /// </summary>
    public static string ExecutablePath => Environment.ProcessPath ?? string.Empty;

    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);
}
