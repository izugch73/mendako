namespace Mendako.Core.Model;

/// <summary>アイテムの分類。散らかしたときの反応の違いもこれで決める。</summary>
public enum ItemCategory
{
    /// <summary>食べ物。</summary>
    Food = 0,

    /// <summary>草花・素材。</summary>
    Material = 1,

    /// <summary>道具・武具。ポーションや爆弾のような危なげなものもここに含める。</summary>
    Gear = 2,

    /// <summary>宝物。</summary>
    Treasure = 3,
}

/// <summary>アイテムを拾う頻度。重みの実数値は <see cref="Simulation.SimulationConfig"/> にある。</summary>
public enum ItemFrequency
{
    /// <summary>よく拾う。</summary>
    Common = 0,

    /// <summary>ときどき拾う。</summary>
    Occasional = 1,

    /// <summary>まれに拾う。</summary>
    Rare = 2,
}

/// <summary>アイテム 1 種類の定義。</summary>
/// <param name="Row">アイコン元画像 (10x10) での行。1 始まり。</param>
/// <param name="Column">アイコン元画像 (10x10) での列。1 始まり。</param>
public sealed record ItemDefinition(
    ItemId Id,
    string DisplayName,
    ItemCategory Category,
    ItemFrequency Frequency,
    int SellPrice,
    int Row,
    int Column);

public static class ItemCatalog
{
    /// <summary>全アイテム。画像の行・列の順に並べてある。</summary>
    public static readonly IReadOnlyList<ItemDefinition> All = new ItemDefinition[]
    {
        new(ItemId.RedPotion, "赤いポーション", ItemCategory.Gear, ItemFrequency.Occasional, 10, 1, 1),
        new(ItemId.BluePotion, "青いポーション", ItemCategory.Gear, ItemFrequency.Occasional, 10, 1, 2),
        new(ItemId.GreenPotion, "緑のポーション", ItemCategory.Gear, ItemFrequency.Occasional, 10, 1, 3),
        new(ItemId.YellowPotion, "黄色いポーション", ItemCategory.Gear, ItemFrequency.Occasional, 10, 1, 4),
        new(ItemId.PurplePotion, "紫のポーション", ItemCategory.Gear, ItemFrequency.Occasional, 12, 1, 5),
        new(ItemId.Elixir, "エリクサー", ItemCategory.Treasure, ItemFrequency.Rare, 60, 1, 6),
        new(ItemId.RedScroll, "赤い巻物", ItemCategory.Gear, ItemFrequency.Occasional, 12, 1, 7),
        new(ItemId.BlueScroll, "青い巻物", ItemCategory.Gear, ItemFrequency.Occasional, 12, 1, 8),
        new(ItemId.Bread, "パン", ItemCategory.Food, ItemFrequency.Common, 2, 1, 9),
        new(ItemId.BoneMeat, "骨付き肉", ItemCategory.Food, ItemFrequency.Common, 3, 1, 10),
        new(ItemId.Apple, "りんご", ItemCategory.Food, ItemFrequency.Common, 1, 2, 1),
        new(ItemId.Grapes, "ぶどう", ItemCategory.Food, ItemFrequency.Common, 2, 2, 2),
        new(ItemId.Carrot, "にんじん", ItemCategory.Food, ItemFrequency.Common, 1, 2, 3),
        new(ItemId.Mushroom, "きのこ", ItemCategory.Food, ItemFrequency.Common, 1, 2, 4),
        new(ItemId.Cheese, "チーズ", ItemCategory.Food, ItemFrequency.Common, 2, 2, 5),
        new(ItemId.Meat, "肉", ItemCategory.Food, ItemFrequency.Common, 3, 2, 6),
        new(ItemId.Fish, "魚", ItemCategory.Food, ItemFrequency.Common, 2, 2, 7),
        new(ItemId.RiceBall, "おにぎり", ItemCategory.Food, ItemFrequency.Common, 2, 2, 8),
        new(ItemId.Soup, "スープ", ItemCategory.Food, ItemFrequency.Common, 3, 2, 9),
        new(ItemId.Cake, "ケーキ", ItemCategory.Food, ItemFrequency.Common, 3, 2, 10),
        new(ItemId.Leaf, "葉っぱ", ItemCategory.Material, ItemFrequency.Common, 2, 3, 1),
        new(ItemId.RedFlower, "赤い花", ItemCategory.Material, ItemFrequency.Common, 3, 3, 2),
        new(ItemId.BlueFlower, "青い花", ItemCategory.Material, ItemFrequency.Common, 3, 3, 3),
        new(ItemId.WhiteFlower, "白い花", ItemCategory.Material, ItemFrequency.Common, 3, 3, 4),
        new(ItemId.Clover, "四つ葉のクローバー", ItemCategory.Material, ItemFrequency.Common, 5, 3, 5),
        new(ItemId.SeedBag, "種の袋", ItemCategory.Material, ItemFrequency.Common, 3, 3, 6),
        new(ItemId.Salt, "塩", ItemCategory.Material, ItemFrequency.Common, 2, 3, 7),
        new(ItemId.Honey, "はちみつ", ItemCategory.Food, ItemFrequency.Common, 3, 3, 8),
        new(ItemId.Egg, "たまご", ItemCategory.Food, ItemFrequency.Common, 2, 3, 9),
        new(ItemId.Milk, "牛乳", ItemCategory.Food, ItemFrequency.Common, 2, 3, 10),
        new(ItemId.Stick, "木の枝", ItemCategory.Material, ItemFrequency.Common, 2, 4, 1),
        new(ItemId.IronOre, "鉄鉱石", ItemCategory.Material, ItemFrequency.Common, 4, 4, 2),
        new(ItemId.CopperOre, "銅鉱石", ItemCategory.Material, ItemFrequency.Common, 4, 4, 3),
        new(ItemId.Stone, "石", ItemCategory.Material, ItemFrequency.Common, 2, 4, 4),
        new(ItemId.GoldNugget, "金の塊", ItemCategory.Treasure, ItemFrequency.Rare, 40, 4, 5),
        new(ItemId.IceCrystal, "氷の結晶", ItemCategory.Treasure, ItemFrequency.Rare, 35, 4, 6),
        new(ItemId.AmethystCrystal, "紫の水晶", ItemCategory.Treasure, ItemFrequency.Rare, 45, 4, 7),
        new(ItemId.RainbowCrystal, "虹の結晶", ItemCategory.Treasure, ItemFrequency.Rare, 70, 4, 8),
        new(ItemId.Bone, "骨", ItemCategory.Material, ItemFrequency.Common, 3, 4, 9),
        new(ItemId.Fang, "牙", ItemCategory.Material, ItemFrequency.Common, 4, 4, 10),
        new(ItemId.Scale, "うろこ", ItemCategory.Material, ItemFrequency.Common, 4, 5, 1),
        new(ItemId.Hide, "毛皮", ItemCategory.Material, ItemFrequency.Common, 4, 5, 2),
        new(ItemId.Feather, "羽根", ItemCategory.Material, ItemFrequency.Common, 3, 5, 3),
        new(ItemId.Cobweb, "クモの巣", ItemCategory.Material, ItemFrequency.Common, 2, 5, 4),
        new(ItemId.Thread, "糸の束", ItemCategory.Material, ItemFrequency.Common, 3, 5, 5),
        new(ItemId.Cloth, "布", ItemCategory.Material, ItemFrequency.Common, 3, 5, 6),
        new(ItemId.Log, "丸太", ItemCategory.Material, ItemFrequency.Common, 3, 5, 7),
        new(ItemId.Plank, "木の板", ItemCategory.Material, ItemFrequency.Common, 3, 5, 8),
        new(ItemId.IronIngot, "鉄のインゴット", ItemCategory.Material, ItemFrequency.Common, 5, 5, 9),
        new(ItemId.SilverIngot, "銀のインゴット", ItemCategory.Treasure, ItemFrequency.Rare, 30, 5, 10),
        new(ItemId.Sword, "剣", ItemCategory.Gear, ItemFrequency.Occasional, 20, 6, 1),
        new(ItemId.Dagger, "短剣", ItemCategory.Gear, ItemFrequency.Occasional, 15, 6, 2),
        new(ItemId.Spear, "槍", ItemCategory.Gear, ItemFrequency.Occasional, 15, 6, 3),
        new(ItemId.Axe, "斧", ItemCategory.Gear, ItemFrequency.Occasional, 16, 6, 4),
        new(ItemId.Hammer, "ハンマー", ItemCategory.Gear, ItemFrequency.Occasional, 14, 6, 5),
        new(ItemId.Bow, "弓", ItemCategory.Gear, ItemFrequency.Occasional, 14, 6, 6),
        new(ItemId.Arrows, "矢の束", ItemCategory.Gear, ItemFrequency.Occasional, 8, 6, 7),
        new(ItemId.BlueStaff, "青い杖", ItemCategory.Gear, ItemFrequency.Occasional, 18, 6, 8),
        new(ItemId.PinkWand, "ピンクの杖", ItemCategory.Gear, ItemFrequency.Occasional, 18, 6, 9),
        new(ItemId.Spellbook, "魔導書", ItemCategory.Gear, ItemFrequency.Occasional, 20, 6, 10),
        new(ItemId.Shield, "盾", ItemCategory.Gear, ItemFrequency.Occasional, 16, 7, 1),
        new(ItemId.Helmet, "兜", ItemCategory.Gear, ItemFrequency.Occasional, 16, 7, 2),
        new(ItemId.Armor, "鎧", ItemCategory.Gear, ItemFrequency.Occasional, 20, 7, 3),
        new(ItemId.Gauntlet, "籠手", ItemCategory.Gear, ItemFrequency.Occasional, 14, 7, 4),
        new(ItemId.Boots, "ブーツ", ItemCategory.Gear, ItemFrequency.Occasional, 12, 7, 5),
        new(ItemId.Cape, "マント", ItemCategory.Gear, ItemFrequency.Occasional, 14, 7, 6),
        new(ItemId.SilverRing, "銀の指輪", ItemCategory.Treasure, ItemFrequency.Rare, 50, 7, 7),
        new(ItemId.GoldRing, "金の指輪", ItemCategory.Treasure, ItemFrequency.Rare, 60, 7, 8),
        new(ItemId.BluePendant, "青いペンダント", ItemCategory.Treasure, ItemFrequency.Rare, 55, 7, 9),
        new(ItemId.Medallion, "メダリオン", ItemCategory.Treasure, ItemFrequency.Rare, 40, 7, 10),
        new(ItemId.Bomb, "爆弾", ItemCategory.Gear, ItemFrequency.Occasional, 15, 8, 1),
        new(ItemId.RoundBomb, "丸い爆弾", ItemCategory.Gear, ItemFrequency.Occasional, 12, 8, 2),
        new(ItemId.Shuriken, "手裏剣", ItemCategory.Gear, ItemFrequency.Occasional, 12, 8, 3),
        new(ItemId.PoisonBottle, "毒びん", ItemCategory.Gear, ItemFrequency.Occasional, 10, 8, 4),
        new(ItemId.FrostBottle, "氷のびん", ItemCategory.Gear, ItemFrequency.Occasional, 10, 8, 5),
        new(ItemId.Bell, "ベル", ItemCategory.Gear, ItemFrequency.Occasional, 8, 8, 6),
        new(ItemId.Key, "鍵", ItemCategory.Gear, ItemFrequency.Occasional, 10, 8, 7),
        new(ItemId.WoodenChest, "木の宝箱", ItemCategory.Treasure, ItemFrequency.Rare, 40, 8, 8),
        new(ItemId.RedChest, "赤い宝箱", ItemCategory.Treasure, ItemFrequency.Rare, 80, 8, 9),
        new(ItemId.CoinPouch, "おかねの袋", ItemCategory.Treasure, ItemFrequency.Rare, 50, 8, 10),
        new(ItemId.Lantern, "ランタン", ItemCategory.Gear, ItemFrequency.Occasional, 12, 9, 1),
        new(ItemId.Torch, "たいまつ", ItemCategory.Gear, ItemFrequency.Occasional, 8, 9, 2),
        new(ItemId.IronChest, "鉄の宝箱", ItemCategory.Treasure, ItemFrequency.Rare, 45, 9, 3),
        new(ItemId.Rope, "ロープ", ItemCategory.Gear, ItemFrequency.Occasional, 8, 9, 4),
        new(ItemId.Pickaxe, "つるはし", ItemCategory.Gear, ItemFrequency.Occasional, 14, 9, 5),
        new(ItemId.Shovel, "スコップ", ItemCategory.Gear, ItemFrequency.Occasional, 12, 9, 6),
        new(ItemId.FishingRod, "釣りざお", ItemCategory.Gear, ItemFrequency.Occasional, 12, 9, 7),
        new(ItemId.BugNet, "虫あみ", ItemCategory.Gear, ItemFrequency.Occasional, 10, 9, 8),
        new(ItemId.Compass, "コンパス", ItemCategory.Gear, ItemFrequency.Occasional, 18, 9, 9),
        new(ItemId.TreasureMap, "宝の地図", ItemCategory.Treasure, ItemFrequency.Rare, 60, 9, 10),
        new(ItemId.Wing, "天使のつばさ", ItemCategory.Treasure, ItemFrequency.Rare, 40, 10, 1),
        new(ItemId.Hourglass, "砂時計", ItemCategory.Treasure, ItemFrequency.Rare, 35, 10, 2),
        new(ItemId.BlueCrystalBall, "青い水晶玉", ItemCategory.Treasure, ItemFrequency.Rare, 90, 10, 3),
        new(ItemId.PurpleCrystalBall, "紫の水晶玉", ItemCategory.Treasure, ItemFrequency.Rare, 100, 10, 4),
        new(ItemId.IceFlower, "氷の花", ItemCategory.Treasure, ItemFrequency.Rare, 60, 10, 5),
        new(ItemId.Ruby, "ルビー", ItemCategory.Treasure, ItemFrequency.Rare, 70, 10, 6),
        new(ItemId.Sapphire, "サファイア", ItemCategory.Treasure, ItemFrequency.Rare, 70, 10, 7),
        new(ItemId.Emerald, "エメラルド", ItemCategory.Treasure, ItemFrequency.Rare, 80, 10, 8),
        new(ItemId.Topaz, "トパーズ", ItemCategory.Treasure, ItemFrequency.Rare, 60, 10, 9),
        new(ItemId.Diamond, "ダイヤモンド", ItemCategory.Treasure, ItemFrequency.Rare, 100, 10, 10),
    };

    private static readonly Dictionary<ItemId, ItemDefinition> ById = All.ToDictionary(d => d.Id);

    private static readonly Dictionary<ItemId, int> OrderById =
        All.Select((definition, index) => (definition.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);

    public static ItemDefinition Get(ItemId id) => ById[id];

    /// <summary>一覧 (<see cref="All"/>) での位置。並べ替えに使う。</summary>
    public static int OrderOf(ItemId id) => OrderById[id];
}
