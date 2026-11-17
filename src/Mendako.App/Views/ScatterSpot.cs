namespace Mendako.App.Views;

/// <summary>アイテムを置く場所。どのモニタのタスクバーの、どのあたりか。</summary>
/// <param name="MonitorId">モニタのデバイス名。プライマリなら null。</param>
/// <param name="Position">足場の全長に対する、アイテムの中心の位置 0.0 - 1.0。</param>
public readonly record struct ScatterSpot(string? MonitorId, double Position);
