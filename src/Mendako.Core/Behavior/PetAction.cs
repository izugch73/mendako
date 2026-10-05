namespace Mendako.Core.Behavior;

/// <summary>
/// 待機以外の一時的な動き。ユーザー操作へのリアクションと、ひとりでにやるしぐさの両方を含む。
/// </summary>
public enum PetAction
{
    /// <summary>特になし。待機アニメーション。</summary>
    None,

    // --- リアクション ---

    /// <summary>もぐもぐ食べている。</summary>
    Eat,

    /// <summary>なでられて喜んでいる。</summary>
    Happy,

    /// <summary>断った (満腹・就寝中)。首を振る。</summary>
    Refuse,

    /// <summary>成長段階が上がった。</summary>
    Evolve,

    // --- しぐさ (ひとりでにやる) ---

    /// <summary>きょろきょろ見回す。</summary>
    LookAround,

    /// <summary>耳ビレをぱたぱたさせて小さく跳ねる。</summary>
    Flutter,

    /// <summary>のびをしてあくび。</summary>
    Yawn,

    /// <summary>ぺたんと平たくなる。メンダコの得意技。</summary>
    Flatten,

    /// <summary>たまごがかたかた揺れる。</summary>
    Wobble,

    /// <summary>タスクバーに沿って泳いで移動する。</summary>
    Swim,
}
