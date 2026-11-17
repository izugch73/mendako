using Mendako.Core.Model;

namespace Mendako.App.Services;

/// <summary>1 回のシミュレーションで知らせたいこと。何も無いものは null か 0。</summary>
/// <param name="StageAdvancedTo">上がった先の成長段階。</param>
/// <param name="SkippedTicks">オフライン進行の上限で打ち切られたティック数。</param>
/// <param name="Forage">拾ったもの・拾えなかったこと。</param>
public sealed record AdvanceReport(GrowthStage? StageAdvancedTo, long SkippedTicks, ForageReport? Forage);
