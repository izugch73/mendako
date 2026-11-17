using System.Collections.Generic;
using Mendako.Core.Model;

namespace Mendako.App.Services;

/// <summary>
/// 1 回のシミュレーションで拾ったものと、拾えなかった回数。
/// 閉じていた間の分も 1 回のシミュレーションで進むので、通知はこの単位でまとめる。
/// </summary>
/// <param name="Items">拾ったアイテム。拾った順。</param>
/// <param name="Missed">持ち物がいっぱいで拾えなかった回数。</param>
public sealed record ForageReport(IReadOnlyList<ItemId> Items, int Missed);
