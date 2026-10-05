namespace Mendako.App.Views;

/// <summary>メンダコの居場所。どのモニタのタスクバーの、どのあたりか。</summary>
/// <param name="MonitorId">モニタのデバイス名。プライマリなら null。</param>
/// <param name="Ratio">タスクバーに沿った位置 0.0 - 1.0。</param>
public readonly record struct PetPlacement(string? MonitorId, double Ratio);
