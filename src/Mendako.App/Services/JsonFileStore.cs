using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Mendako.App.Services;

/// <summary>
/// JSON ファイルの読み書き。書き込みは一時ファイル経由の置き換えにしてあり、
/// 保存中に電源が落ちてもファイルが壊れない。
/// </summary>
public sealed class JsonFileStore<T>
    where T : class
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // 日本語の名前がエスケープされて読めなくなるのを防ぐ
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private readonly string _path;

    public JsonFileStore(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>読み込む。ファイルがない・壊れている場合は null を返す。</summary>
    public T? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = File.ReadAllText(_path);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 壊れていたら握りつぶして初期状態から始める。
            // ここで落とすと「起動しないアプリ」になってしまう。
            return null;
        }
    }

    /// <summary>アトミックに保存する。</summary>
    public void Save(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));

        if (File.Exists(_path))
        {
            // File.Replace は書き込み先が存在する場合のみ使える
            File.Replace(temporary, _path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporary, _path);
        }
    }
}
