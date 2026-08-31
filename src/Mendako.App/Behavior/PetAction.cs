namespace Mendako.App.Behavior;

/// <summary>ユーザー操作に対する一時的なリアクション。</summary>
public enum PetAction
{
    /// <summary>特になし。待機アニメーション。</summary>
    None,

    /// <summary>もぐもぐ食べている。</summary>
    Eat,

    /// <summary>なでられて喜んでいる。</summary>
    Happy,

    /// <summary>断った (満腹・就寝中)。首を振る。</summary>
    Refuse,

    /// <summary>成長段階が上がった。</summary>
    Evolve,
}
