using System;
using System.Collections.Generic;
using System.Linq;
using Mendako.App.Sprites;
using Mendako.Core.Behavior;
using Mendako.Core.Model;

namespace Mendako.SpritePreview;

/// <summary>
/// 書き出せる一覧の中身。1 つの一覧は見出しつきのセクションの並び。
/// 新しいコマや効果を確認したいときは、セクションを返すメソッドを足して <see cref="All"/> に載せる。
/// </summary>
internal static class Sheets
{
    private static readonly GrowthStage[] HatchedStages =
    {
        GrowthStage.Hatchling, GrowthStage.Juvenile, GrowthStage.Adult, GrowthStage.Elder,
    };

    private static readonly FinPose[] Fins = { FinPose.Up, FinPose.Mid, FinPose.Droop };

    /// <summary>目のコマ。Open だけは視線ごとに分ける。</summary>
    private static readonly (EyePose Eyes, int Gaze, string Label)[] EyeFrames =
    {
        (EyePose.Open, -1, "Open ←"),
        (EyePose.Open, 0, "Open"),
        (EyePose.Open, 1, "Open →"),
        (EyePose.Closed, 0, "Closed"),
        (EyePose.Happy, 0, "Happy"),
        (EyePose.Surprised, 0, "Surprised"),
    };

    /// <summary>一覧の名前と、その中身を作る関数。</summary>
    public static readonly IReadOnlyDictionary<string, Func<GrowthStage, IReadOnlyList<PreviewSection>>> All =
        new Dictionary<string, Func<GrowthStage, IReadOnlyList<PreviewSection>>>
        {
            ["frames"] = stage => new[] { Stages(), AllFrames(stage), Palettes(), Bubbles() },
            ["motion"] = stage => new[] { Motion(stage) },
        };

    /// <summary>各段階の実寸。PixelScale の違いで大きさがどう変わるかを見る。</summary>
    private static PreviewSection Stages()
    {
        var cells = Enum.GetValues<GrowthStage>()
            .Select(stage =>
            {
                var scale = MendakoSprites.PixelScale(stage);
                var frame = MendakoSprites.Get(stage, FinPose.Mid, EyePose.Open);
                var width = frame.Bitmap.PixelWidth;
                var height = frame.Bitmap.PixelHeight;
                return Cells.Bitmap(
                    $"{stage} ×{scale}  {width * scale}×{height * scale} DIP",
                    frame.Bitmap,
                    scale);
            })
            .ToList();

        return new PreviewSection(
            $"成長段階ごとの実寸 (ドット {MendakoSprites.Width}×{MendakoSprites.Height})",
            new[] { cells });
    }

    /// <summary>MendakoSprites.Get の全組み合わせ。行が耳ビレ (と flat)、列が目。</summary>
    private static PreviewSection AllFrames(GrowthStage stage)
    {
        var scale = MendakoSprites.PixelScale(stage);
        var rows = new List<IReadOnlyList<PreviewCell>>();

        foreach (var flat in new[] { false, true })
        {
            foreach (var fin in Fins)
            {
                rows.Add(EyeFrames
                    .Select(e => Cells.Bitmap(
                        $"{fin}{(flat ? " flat" : string.Empty)} / {e.Label}",
                        MendakoSprites.Get(stage, fin, e.Eyes, e.Gaze, flat).Bitmap,
                        scale))
                    .ToList());
            }
        }

        return new PreviewSection($"全コマ ({stage} のパレット・×{scale})", rows);
    }

    /// <summary>段階ごとの配色を、同じ倍率で並べて比べる。</summary>
    private static PreviewSection Palettes()
    {
        const int Scale = 4;
        var samples = new (FinPose Fin, EyePose Eyes, bool Flat)[]
        {
            (FinPose.Up, EyePose.Open, false),
            (FinPose.Mid, EyePose.Happy, false),
            (FinPose.Droop, EyePose.Closed, false),
            (FinPose.Droop, EyePose.Surprised, true),
        };

        var rows = HatchedStages
            .Select(stage => (IReadOnlyList<PreviewCell>)samples
                .Select(s => Cells.Bitmap(
                    $"{stage} {s.Fin}{(s.Flat ? " flat" : string.Empty)} {s.Eyes}",
                    MendakoSprites.Get(stage, s.Fin, s.Eyes, 0, s.Flat).Bitmap,
                    Scale))
                .ToList())
            .Prepend(new[]
            {
                Cells.Bitmap("Egg", MendakoSprites.Get(GrowthStage.Egg, FinPose.Mid, EyePose.Open).Bitmap, Scale),
            })
            .ToList();

        return new PreviewSection($"段階ごとのパレット (×{Scale} にそろえて比較)", rows);
    }

    /// <summary>MendakoVisual に載せた状態。吹き出しの大きさと位置、接地位置を見る。</summary>
    private static PreviewSection Bubbles()
    {
        var row = new List<PreviewCell>();
        foreach (var stage in Enum.GetValues<GrowthStage>())
        {
            var state = Cells.StateAt(stage);
            row.Add(Cells.Visual(
                $"{stage} ShowHeart",
                new PetPose { Fin = FinPose.Up, Eyes = EyePose.Happy, ShowHeart = true },
                state));
            row.Add(Cells.Visual(
                $"{stage} ShowSleepMark",
                new PetPose { Fin = FinPose.Droop, Eyes = EyePose.Closed, ShowSleepMark = true },
                state));
        }

        return new PreviewSection(
            "MendakoVisual (160×150) 上の吹き出しと接地",
            new[] { row.Take(6).ToList(), row.Skip(6).ToList() });
    }

    /// <summary>
    /// BehaviorMachine で各 PetAction を再生し、等間隔に抜き出したコマを MendakoVisual で描く。
    /// 揺れや跳ねの量を、アプリと同じ換算で確認するためのもの。
    /// </summary>
    private static PreviewSection Motion(GrowthStage stage)
    {
        const int Samples = 8;
        const double Frame = 1d / 30d;

        var roomy = new BehaviorInput { RoomBeforeDots = 1000d, RoomAfterDots = 1000d };
        var scale = MendakoSprites.PixelScale(stage);
        var rows = new List<IReadOnlyList<PreviewCell>>();

        var actions = Enum.GetValues<PetAction>().Where(a => a != PetAction.Wobble || stage == GrowthStage.Egg);
        foreach (var action in actions)
        {
            var state = Cells.StateAt(stage);
            var machine = new BehaviorMachine(seed: 3) { GesturesEnabled = false };
            var seconds = 2d;
            if (action != PetAction.None)
            {
                machine.Trigger(action, action is PetAction.Held or PetAction.Fall or PetAction.Swim ? seconds : null);
            }

            var cells = new List<PreviewCell>();
            var travelled = 0d;
            var time = 0d;
            for (var i = 0; i < Samples; i++)
            {
                var until = seconds * i / Samples;
                PetPose pose;
                do
                {
                    pose = machine.Advance(Frame, state, roomy);
                    travelled += pose.TravelDots;
                    time += Frame;
                }
                while (time < until);

                var label = $"{action} {time:0.00}s bob {pose.BobDots:0.0} drift {pose.DriftDots:0.0}";
                if (action == PetAction.Swim)
                {
                    label += $" 移動 {travelled * scale:0} DIP";
                }

                cells.Add(Cells.Visual(label, pose, state));
            }

            rows.Add(cells);
        }

        return new PreviewSection($"PetAction ごとの動き ({stage}・×{scale}・{Samples} コマ等間隔)", rows);
    }
}
