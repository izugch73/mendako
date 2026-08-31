namespace Mendako.IconGen;

/// <summary>ドット絵の 1 色。</summary>
internal readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>
/// exe のアイコン用ドット絵。1 ドット = 1 文字で、記法は
/// <c>MendakoSprites</c> と同じ (<c>.</c> 透明 / <c>K</c> 輪郭 / <c>R</c> 体 / <c>D</c> 影 / <c>W</c> 白目)。
///
/// 本体のスプライトは 20 x 17 で、16 px アイコンに整数倍で収まらない。
/// 端数倍率で縮めると 1 ドットの輪郭が飛んで輪郭が崩れるので、
/// アイコンは 16 x 16 を基準に描き起こし、32 / 48 / 64 / 128 / 256 を
/// x2 / x3 / x4 / x8 / x16 の整数倍で作る。どの表示サイズでもドットが滲まない。
///
/// 細い天面と広い耳ビレのあいだの段差 (2-4 行目) が「耳」に見せる肝で、
/// ここをなだらかにするとただの丸になる。本体スプライトと同じ理屈。
/// </summary>
internal static class IconArt
{
    /// <summary>基準の一辺。生成する各サイズはこれの整数倍でなければならない。</summary>
    public const int Size = 16;

    public static readonly string[] Rows =
    {
        "......KKKK......",
        "....KKRRRRKK....",
        "..KKKRRRRRRKKK..",
        ".KKRRRRRRRRRRKK.",
        "..KKRRRRRRRRKK..",
        "...KRRRRRRRRK...",
        "...KRRRRRRRRK...",
        "...KRRRRRRRRK...",
        "...KRWRRRRWRK...",
        "...KRWRRRRWRK...",
        "...KRRRRRRRRK...",
        "...KRRRRRRRRK...",
        "...KDDDDDDDDK...",
        "....KDDDDDDK....",
        "....KDKDDKDK....",
        "....KK.KK.KK....",
    };

    /// <summary>
    /// 色は <c>MendakoSprites.Palette</c> の Adult 段階に合わせてある。
    /// 向こうを変えたらここも合わせること (アプリは WPF の Color、こちらは依存なしの <see cref="Rgb"/>)。
    /// </summary>
    public static readonly IReadOnlyDictionary<char, Rgb> Palette = new Dictionary<char, Rgb>
    {
        ['K'] = new(0x1B, 0x24, 0x40),
        ['R'] = new(0xE2, 0x57, 0x4B),
        ['D'] = new(0xC4, 0x48, 0x3E),
        ['W'] = new(0xFF, 0xFF, 0xFF),
    };

    /// <summary>透明を表す文字。</summary>
    public const char Transparent = '.';
}
