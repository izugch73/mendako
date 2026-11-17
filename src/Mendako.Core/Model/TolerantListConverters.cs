using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mendako.Core.Model;

/// <summary>
/// 保存データの一覧を読むとき、読めない要素だけを読み飛ばし、null は空として扱う。
/// 要素が 1 つ読めないだけで状態ファイル全体を読み捨てると、育てた進行がすべて消えるため。
/// 将来のバージョンで増えた項目を、古いアプリが読み込んだ場合にも効く。
/// </summary>
internal abstract class TolerantListConverter<T> : JsonConverter<IReadOnlyList<T>>
    where T : notnull
{
    // null のトークンでも Read を呼ばせて、空の一覧に置き換える
    public override bool HandleNull => true;

    protected abstract bool IsValid(T value);

    public override IReadOnlyList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return Array.Empty<T>();
        }

        var list = new List<T>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            // 要素ごとに JsonElement へ切り出してから読む。読み取りの途中で例外を捕まえると、リーダーの位置が戻せない
            var element = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
            try
            {
                if (element.Deserialize<T>(options) is { } value && IsValid(value))
                {
                    list.Add(value);
                }
            }
            catch (JsonException)
            {
            }
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndArray();
    }
}

internal sealed class ItemIdListConverter : TolerantListConverter<ItemId>
{
    // 数値で書かれていた場合、範囲外でも例外にならずに読めてしまうので、定義の有無を確かめる
    protected override bool IsValid(ItemId value) => Enum.IsDefined(value);
}

internal sealed class AccessoryListConverter : TolerantListConverter<Accessory>
{
    protected override bool IsValid(Accessory value) => value != Accessory.None && Enum.IsDefined(value);
}

internal sealed class ScatteredItemListConverter : TolerantListConverter<ScatteredItem>
{
    protected override bool IsValid(ScatteredItem value) => Enum.IsDefined(value.Item);
}
