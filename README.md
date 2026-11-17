# メンダコ

Windows のタスクバーの上に住みつくメンダコを育てる常駐アプリ。

- タスクバーの上端に立つ透過オーバーレイ（Alt+Tab に出ない・フォーカスを奪わない）
- サブモニタのタスクバーにも住める（DPI の違うモニタが混ざっていてもよい）
- ひとりでに泳いで移動し、きょろきょろ・あくび・ぺたん、と気分に応じたしぐさをする
- メンダコの上だけクリックを受け取り、それ以外は下のウィンドウへ素通し
- たまご → 稚メンダコ → 若メンダコ → メンダコ → ぬしメンダコ の 5 段階に育つ
- アプリが動いていない時間も経過する（オフライン進行、上限つき）
- 全画面ゲーム・プレゼン中は自動で引っ込む
- メンダコ（Adult）に育つと、3 時間ごとにアイテムを 1 個拾ってくる（100 種、持ち物は 20 個まで）
- 持ち物のアイテムは「もちもの」で売ってかいがらに換えるか、タスクバーに置ける。置くとメンダコが近寄って、分類ごとに違う反応をする
- 「おみせ」でかいがらを使って飾りを買い、付け外しできる（5 種、160〜360 かいがら）

## 操作

| 操作 | 動作 |
|---|---|
| 体の上でカーソルを左右に往復 | なでる |
| クリック | つつく（びっくりするだけ） |
| ダブルクリック | ごはんをあげる |
| ドラッグ | つまんで運ぶ。放すとふわふわ降りて、そのモニタのタスクバーに居着く（位置は記憶される） |
| ホバー | ステータスカードを表示 |
| 右クリック / トレイアイコン | メニュー |
| メニューの「もちもの」 | 持っているアイテムを売る・タスクバーに置く |
| メニューの「おみせ」 | 飾りを買う・付ける・外す |
| 置いたアイテムをクリック | 持ち物に戻す（持ち物がいっぱいのときは戻らない） |

## 構成

```
Mendako.sln
├─ src/Mendako.Core/        UI にも時計にも依存しない育成シミュレーション
│   ├─ Model/               MendakoState / GrowthStage / Mood / Accessory / ItemCatalog（アイテム 100 種）/ ScatteredItem
│   ├─ Behavior/            BehaviorMachine（状態 → ポーズ）, StrokeDetector（なで判定）
│   └─ Simulation/          Simulator（すべて純粋関数）, SimulationConfig,
│                           Foraging（拾う）, Inventory（売る）, Scattering（置く・反応）, Shop（飾りの売買）
├─ src/Mendako.Platform/    Win32 の隔離層
│   ├─ NativeMethods.cs     P/Invoke はここだけ
│   ├─ TaskbarLocator.cs    全モニタのタスクバー矩形と辺を取得
│   ├─ OverlayWindow.cs     拡張スタイル・クリックスルー・最前面の維持・物理ピクセルでの移動
│   ├─ UserPresence.cs      全画面アプリ／プレゼン検出
│   └─ AutoStart.cs         HKCU の Run キー
├─ src/Mendako.App/         WPF
│   ├─ Sprites/             ドット絵データとビットマップ展開（メンダコ本体は MendakoSprites、飾りは AccessorySprites）
│   ├─ Assets/Items/        アイテムのアイコン（PNG 100 枚、Resource として埋め込む）
│   ├─ Views/               PetWindow（オーバーレイ）, MendakoVisual（スプライト表示）,
│   │                       InventoryWindow（もちもの）, ShopWindow / AccessoryRow（おみせ）,
│   │                       ScatterLayer / ScatterItemWindow（タスクバーに置いたアイテム）
│   └─ Services/            永続化・トレイ・セッション
├─ tools/Mendako.IconGen/   ドット絵から exe の .ico を焼くビルド時の道具
├─ tools/Mendako.SpritePreview/  ドット絵を PNG に書き出して目視確認する道具（下の「確認用ツール」）
└─ tests/Mendako.Core.Tests/
```

依存の向きは `App → Platform → (なし)` と `App → Core → (なし)`。
Core は Win32 も WPF も知らないので、「3 日放置したらどうなるか」も「5 分眺めていたら何をするか」も
実時間を待たずにテストできる。

## 設計上の判断

**シミュレーションは純粋関数**
`Simulator.Advance(state, now, timeZone, config)` は状態と時刻だけを受け取り、新しい状態を返す。
乱数も時計も内部に持たないので、14 日放置や時計の巻き戻しをテストで再現できる。

**時間はウォールクロック基準、キャッチアップに上限**
アプリが動いていた時間ではなく `LastTickUtc` からの実経過で進める。
ただし既定で 3 日分（`MaxCatchUpTicks`）で打ち切り、旅行から帰ったら手遅れ、という体験を避ける。
打ち切る場合も基準時刻は現在まで進めるので、次回起動で同じ時間を二重に消化しない。
時計が巻き戻された場合は進めず、基準時刻だけ合わせ直す。

**クリックスルーは動的に切り替える**
WPF の `Image` は透明ピクセルでも矩形全体がヒットテスト対象になるため、
`MendakoVisual` は中の `Canvas` の `IsHitTestVisible` を落とし、カーソル位置を `GetCursorPos` でポーリングして
ドットのアルファで直接判定している（`MendakoVisual.HitTestSprite`）。
結果に応じて `WS_EX_TRANSPARENT` を付け外しする。
クリックスルーが有効なあいだ WPF はマウスイベントを受け取れないので、ポーリングが必要。

クリックが届いたあとに WPF がそれを配る相手も要るので、`MendakoVisual` は `HitTestCore` を上書きして
同じアルファ判定を返している。コントロールごと `IsHitTestVisible` を落とすと、
本体をクリックしてもマウスイベントが発火しない。

**ドット絵をソースに直接書く**
`MendakoSprites` が 20 x 17 ドットの絵を 1 ドット = 1 文字の文字列で持ち、
実行時に `PixelSprite` が `BitmapSource` へ展開する。メンダコ本体と飾りは PNG を置かずに済むので、
差分もレビューできる。トレイアイコンも `System.Drawing` で実行時に描いている。
例外はアイテムのアイコンだけで、`src/Mendako.App/Assets/Items/` に PNG を 100 枚置き、
`Resource` として埋め込んでいる。単一ファイル発行でも exe の中から読める。
この PNG は作者の自作（AI 生成を含む）の絵で、文字列には直さずそのまま置いている。

頭（耳ビレを含む上 8 行）と胴（下 9 行）を別に持ち、組み合わせ + 目のスタンプでコマを合成する。
耳ビレ 3 種 × 目 4 種に、視線（左・正面・右）と「ぺたん」の有無を掛けたコマを、使ったぶんだけ
成長段階ごとにキャッシュしている。ぺたんは頭の下 3 行を抜いて沈めるだけで、専用の絵は持たない。
細い天面と広い耳のあいだの段差（ノッチ）が「耳」に見せる肝で、ここをなだらかにすると
ただの多角形になる。

拡大時は `RenderOptions.BitmapScalingMode="NearestNeighbor"` が必須。
浮遊オフセットもドット単位に丸めてから実寸に直さないと、半端な位置でドットが滲む。

**exe のアイコンもドット絵から焼く**
`.ico` を置くとバイナリがリポジトリに入るので、`tools/Mendako.IconGen` がビルド時に
`src/Mendako.App/obj/mendako.ico` を生成し、`ApplicationIcon` がそれを指す。
生成物は gitignore 済みの `obj/` に落ちるので、リポジトリに入るのはドット絵の文字列だけ。

アイコン用の絵は本体スプライトとは別に 16 x 16 で持っている。
本体は 20 x 17 で 16 px に整数倍で収まらず、端数倍率で縮めると 1 ドットの輪郭が飛ぶため。
16 を基準にすると 32 / 48 / 64 / 128 / 256 が x2 / x3 / x4 / x8 / x16 の整数倍になり、
どの表示サイズでもドットが滲まない。20 や 24 のような中途半端なサイズは入れず、Windows 側で縮めさせる。

`.ico` も PNG も自前で書いている（`IcoWriter` / `PngEncoder`）ので、生成ツールの NuGet 参照はゼロ。
大きいコマを非圧縮の DIB で入れると 256 x 256 だけで 270 KB になるため、64 px 以上は PNG のコマにしてある。
結果、全 6 サイズ入りで 17 KB。小さいコマを DIB のままにしているのは、
GDI+ の `Icon.ToBitmap()` が PNG のコマを読めないため（Windows のシェルが使う WIC は読める）。

**アイテムを拾う・売る・置く**
`Simulator.Advance` は、成長段階が `SimulationConfig.ForageFromStage`（既定は Adult）以上のあいだ、
`ForageInterval`（3 時間）ごとにアイテムを 1 個拾う。数え始めは Adult に達したティックの終わりで、
それ以前の時間は数えない。持ち物は `MaxInventoryItems`（20 個）までで、いっぱいで拾えなかった時刻も
「過ぎたもの」として基準を進めるので、売ったあとに溜まっていた分をまとめて拾うことはない。
抽選は `ItemCatalog`（100 種）の頻度（よく・ときどき・まれに）で重みを付ける。

持ち物のアイテムは、売ると 1 個ずつ決まった値段のかいがらになる。`Scattering.Place` でタスクバーに置くこともでき、
同時に置けるのは `MaxScatteredItems`（3 個）まで。置いたアイテムにはメンダコが近寄り、
分類で反応が決まる。食べ物は食べる（ごはんと同じ効果。満腹だと食べずに残す）、素材は喜ぶ、
宝物は見とれる、道具はつついてびっくりする。一度反応したアイテムには `BehaviorMachine.ScatterCooldownSeconds`（10 分）近寄らない。
片付けて置き直すと別のアイテムとして数える。

**飾りは見た目だけの層**
`AccessorySprites` が、本体のコマの上に重ねる飾りの層（上と左右に余白を足した絵）を持つ。
飾りは「おみせ」で 160〜360 かいがら（`SimulationConfig.AccessoryPrices`）で買い、付けられるのは 1 つだけ。
Adult と Elder は同じ頭の絵で倍率だけが違うので、同じ層をどちらにも重ねる。

**保存はアトミック、間隔を空ける**
一時ファイルに書いてから `File.Replace`。3 分間隔＋`SessionEnding`＋終了時に保存し、毎ティックは書かない。

**位置決めは物理ピクセルで、Win32 から直接**
WPF の `Left` / `Top` は DIP で、DPI の違うモニタをまたぐとどの倍率で換算されるかが当てにならない。
タスクバーの矩形もカーソルも物理ピクセルで返ってくるので、位置だけは DIP に直さず
`SetWindowPos` で動かしている（`OverlayWindow.MoveTo`）。大きさは WPF に任せたままでよく、
モニタをまたいで `WM_DPICHANGED` が来たら足元を合わせ直す。

**動きには優先度がある**
`BehaviorMachine.Trigger` は、実行中の動きより優先度が低いものを断る。
しぐさ < 世話のリアクション・つまみ上げ < 段階アップ、の順。
ごはんで段階が上がると「進化」の直後に「食べる」が届くので、後勝ちのままだと進化の演出が消える。

**しぐさと散歩は Core、位置を動かすのは App**
`BehaviorMachine` は「このフレームでタスクバーに沿って何ドット進むか」（`PetPose.TravelDots`）までを決め、
ウィンドウを実際に動かすのは `PetWindow`。左右にどれだけ動けるかとカーソルの位置は
`BehaviorInput` で毎フレーム渡す。乱数のシードを固定すれば、しぐさの順番まで再現できる。

**常駐アプリとしての省リソース**
`CompositionTarget.Rendering` は使わず `DispatcherTimer` を 30fps で回し、
就寝中かつ非ホバー時は約 7fps に落とす。全画面アプリ検出時はタイマーごと止める。

## 確認用ツール

`tools/Mendako.SpritePreview` は、ドット絵を PNG に書き出して目視で確かめる道具。
アプリは起動しない。

```powershell
dotnet run --project tools/Mendako.SpritePreview -- out.png --sheet accessories --stage Adult
```

- `--sheet` は `frames`（既定）/ `motion` / `accessories` / `accessories-visual`。
  `accessories` は飾りごとの全ポーズの見比べ、`accessories-visual` は飾りを付けた `MendakoVisual`（184 x 186）を実寸で並べる。
  後者は出力先がそのまま使われるので、2 種類を続けて書き出すときは出力名を変える。
- `--stage` は `Egg` / `Hatchling` / `Juvenile` / `Adult` / `Elder`
- `--zoom N` で倍率、`--section 番号,...` で一部の節だけを書き出す

## ビルド

.NET 8 SDK が必要。

```powershell
winget install Microsoft.DotNet.SDK.8
dotnet build Mendako.sln
dotnet test tests/Mendako.Core.Tests/Mendako.Core.Tests.csproj
dotnet run --project src/Mendako.App/Mendako.App.csproj
```

## 配布

配布用のビルドは発行プロファイルにまとめてある。

```powershell
dotnet publish src/Mendako.App/Mendako.App.csproj -p:PublishProfile=win-x64 -o publish
```

出るのは `Mendako.exe` 1 個だけ（約 63 MB）。自己完結にしてあるので、
渡す相手に .NET 8 Desktop Runtime を入れてもらう必要はない。
WPF は `PublishTrimmed` に対応しないため、ここから縮める余地はない。

`v1.2.3` の形でタグを打つと `.github/workflows/release.yml` がテストして publish し、
zip を GitHub Release に貼る。タグから `v` を落としたものが exe のバージョンになる。

```powershell
git tag v1.0.0
git push origin v1.0.0
```

**未署名なので SmartScreen が出る。** ダウンロードした利用者は初回起動で
「WindowsによってPCが保護されました」→「詳細情報」→「実行」を踏むことになる。
消すにはコード署名証明書（OV でも年 3〜5 万円、かつハードウェアトークンかクラウド HSM が必須）か、
Microsoft Store 経由での配布が要る。ただし Store は MSIX 化が前提で、
`HKCU\Run` による自動起動が使えなくなり `StartupTask` 拡張への書き換えが必要になる。

exe を直接置くとブラウザ側のダウンロード警告を踏みやすいので、Release には zip で貼っている。

## 保存先

- 育成状態: `%LOCALAPPDATA%\Mendako\state.json`
- 設定: `%LOCALAPPDATA%\Mendako\settings.json`
- 自動起動: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Mendako`

飼い直したいときは `state.json` を消す。

環境変数 `MENDAKO_DATA_DIR` を設定すると、育成状態と設定の保存先をそのフォルダに差し替えられる。
開発中に、飼っているメンダコを巻き込まずに試すためのもの。

```powershell
$env:MENDAKO_DATA_DIR = "$env:TEMP\mendako-dev"
dotnet run --project src/Mendako.App/Mendako.App.csproj
```

## アンインストール

インストーラを使わないので、消すものは 3 つ。

```powershell
# 1. 常駐を止める
Stop-Process -Name Mendako -ErrorAction SilentlyContinue
# 2. 自動起動の登録を消す
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Mendako -ErrorAction SilentlyContinue
# 3. 育成状態と設定を消す
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Mendako"
```

あとは `Mendako.exe` を捨てれば残らない。

## 既知の制限

- サブモニタに住めるのは、Windows の「タスクバーをすべてのディスプレイに表示する」が有効なときだけ。
  タスクバーの無いモニタで放すと、元いたモニタへ帰っていく。
- 居着いたモニタはデバイス名（`\\.\DISPLAY2` など）で覚えている。配線を挿し替えて番号が変わると
  別のモニタとみなし、見つからないあいだはプライマリに避難する。
- おさんぽでモニタをまたぐことはない。勝手に別の画面へ行ってしまうと探すことになるので、
  またぐのはつまんで運んだときだけにしている。
- 全画面アプリで引っ込むのは、メンダコと同じモニタで全画面になったときだけ。ただし
  `SHQueryUserNotificationState` はプライマリモニタの全画面しか教えてくれないので、
  サブモニタで全画面にしたアプリは検出できず、そのモニタにいるメンダコは上に出たままになる。
- タスクバーを自動的に隠す設定では、矩形が画面外になるため作業領域の下端を基準にしている。
- ステータスカードが頭の上端に少し重なる。跳ねるしぐさのときは頭がカードの裏に隠れる。
- `Mendako.Platform` と `PetWindow`（位置決め・落下）にはテストがない。モニタ構成に依存するので、
  混在 DPI の実機で動かして確かめている。
- アイテムを置いたあとの 10 分の記録（同じアイテムに近寄らない時間）はメモリにだけあり、
  アプリを再起動すると消える。
- 飾りの見た目を目視で確かめたのは Adult と Elder だけ。それより若い段階には、おみせでも Adult の絵を出している。
