namespace Mendako.Core.Model;

/// <summary>表示・振る舞いの決定に使う気分。状態から導出される。</summary>
public enum Mood
{
    /// <summary>ごきげん。</summary>
    Happy,

    /// <summary>ふつう。</summary>
    Content,

    /// <summary>おなかがすいた。</summary>
    Hungry,

    /// <summary>ねむい。</summary>
    Sleepy,

    /// <summary>かまってほしい。</summary>
    Lonely,

    /// <summary>しょんぼり。放置が続いた状態。</summary>
    Gloomy,
}

public static class Moods
{
    /// <summary>各パラメータから気分を導出する。優先度の高い不満から順に判定する。</summary>
    /// <remarks>
    /// この判定を <see cref="MendakoState"/> ではなくこちらに置いているのは、
    /// あちらでは Mood プロパティが Mood 型を隠してしまい参照が読みにくくなるため。
    /// </remarks>
    public static Mood Resolve(double satiety, double energy, double affection)
    {
        if (satiety <= 15d && affection <= 25d)
        {
            return Mood.Gloomy;
        }

        if (satiety <= 25d)
        {
            return Mood.Hungry;
        }

        if (energy <= 20d)
        {
            return Mood.Sleepy;
        }

        if (affection <= 20d)
        {
            return Mood.Lonely;
        }

        if (satiety >= 60d && energy >= 50d && affection >= 55d)
        {
            return Mood.Happy;
        }

        return Mood.Content;
    }

    public static string DisplayName(Mood mood) => mood switch
    {
        Mood.Happy => "ごきげん",
        Mood.Content => "ふつう",
        Mood.Hungry => "おなかぺこぺこ",
        Mood.Sleepy => "ねむねむ",
        Mood.Lonely => "かまってほしい",
        Mood.Gloomy => "しょんぼり",
        _ => "???",
    };
}
