namespace Mendako.Core.Model;

/// <summary>
/// タスクバーに置いてあるアイテム 1 個。持ち物から減り、片付けると持ち物に戻る。
/// </summary>
public sealed record ScatteredItem
{
    /// <summary>
    /// 置いた 1 個を見分ける識別子。同じ種類を何個か置けるのと、片付けて置き直したものが
    /// 前のもののクールダウンを引き継がないようにするため、アイテムの種類では代用できない。
    /// </summary>
    public Guid Id { get; init; }

    public ItemId Item { get; init; }

    /// <summary>足場の全長に対する、アイテムの中心の位置 0.0 - 1.0。絶対座標だと解像度の変更で画面外に消える。</summary>
    public double Position { get; init; }

    /// <summary>置いたモニタのデバイス名。プライマリなら null。メンダコの居場所の保存と同じ考え方。</summary>
    public string? MonitorId { get; init; }
}
