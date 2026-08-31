# メンダコ

Windows のタスクバーの上に住みつくメンダコを育てる常駐アプリ。

- タスクバーの上端に立つ透過オーバーレイ（Alt+Tab に出ない・フォーカスを奪わない）
- メンダコの上だけクリックを受け取り、それ以外は下のウィンドウへ素通し
- たまご → 稚メンダコ → 若メンダコ → メンダコ → ぬしメンダコ の 5 段階に育つ
- アプリが動いていない時間も経過する（オフライン進行、上限つき）
- 全画面ゲーム・プレゼン中は自動で引っ込む

## 操作

| 操作 | 動作 |
|---|---|
| クリック | なでる |
| ダブルクリック | ごはんをあげる |
| ドラッグ | タスクバーに沿って移動（位置は記憶される） |
| ホバー | ステータスカードを表示 |
| 右クリック / トレイアイコン | メニュー |

## 構成

```
Mendako.sln
├─ src/Mendako.Core/        UI にも時計にも依存しない育成シミュレーション
│   ├─ Model/               MendakoState / GrowthStage / Mood
│   └─ Simulation/          Simulator（すべて純粋関数）, SimulationConfig
├─ src/Mendako.Platform/    Win32 の隔離層
│   ├─ NativeMethods.cs     P/Invoke はここだけ
│   ├─ TaskbarLocator.cs    SHAppBarMessage でタスクバー矩形と辺を取得
│   ├─ OverlayWindow.cs     拡張スタイル・クリックスルー・最前面の維持
│   ├─ UserPresence.cs      全画面アプリ／プレゼン検出
│   └─ AutoStart.cs         HKCU の Run キー
├─ src/Mendako.App/         WPF
│   ├─ Behavior/            BehaviorMachine（状態 → ポーズ、描画非依存）
│   ├─ Sprites/             ドット絵データとビットマップ展開
│   ├─ Views/               PetWindow（オーバーレイ）, MendakoVisual（スプライト表示）
│   └─ Services/            永続化・トレイ・セッション
├─ tools/Mendako.IconGen/   ドット絵から exe の .ico を焼くビルド時の道具
└─ tests/Mendako.Core.Tests/
```

依存の向きは `App → Platform → (なし)` と `App → Core → (なし)`。
Core は Win32 も WPF も知らないので、「3 日放置したらどうなるか」を実時間を待たずにテストできる。

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
`MendakoVisual` は `IsHitTestVisible` を落とし、カーソル位置を `GetCursorPos` でポーリングして
ドットのアルファで直接判定している（`MendakoVisual.HitTestSprite`）。
結果に応じて `WS_EX_TRANSPARENT` を付け外しする。
クリックスルーが有効なあいだ WPF はマウスイベントを受け取れないので、ポーリングが必要。

**ドット絵をソースに直接書く**
`MendakoSprites` が 20 x 17 ドットの絵を 1 ドット = 1 文字の文字列で持ち、
実行時に `PixelSprite` が `BitmapSource` へ展開する。PNG を置かずに済むので
バイナリ資産ゼロのまま、差分もレビューできる。トレイアイコンも `System.Drawing` で実行時に描いている。

頭（耳ビレを含む上 8 行）と胴（下 9 行）を別に持ち、組み合わせ + 目のスタンプでコマを合成する。
耳ビレ 3 種 × 目 3 種 = 9 コマを成長段階ごとにキャッシュしている。
細い天面と広い耳のあいだの段差（ノッチ）が「耳」に見せる肝で、ここをなだらかにすると
ただの多角形になる。

拡大時は `RenderOptions.BitmapScalingMode="NearestNeighbor"` が必須。
浮遊オフセットもドット単位に丸めてから実寸に直さないと、半端な位置でドットが滲む。

**exe のアイコンもドット絵から焼く**
`.ico` を置くとバイナリ資産ゼロが崩れるので、`tools/Mendako.IconGen` がビルド時に
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

**保存はアトミック、間隔を空ける**
一時ファイルに書いてから `File.Replace`。3 分間隔＋`SessionEnding`＋終了時に保存し、毎ティックは書かない。

**常駐アプリとしての省リソース**
`CompositionTarget.Rendering` は使わず `DispatcherTimer` を 30fps で回し、
就寝中かつ非ホバー時は約 7fps に落とす。全画面アプリ検出時はタイマーごと止める。

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

- サブモニタのタスクバーには乗らない（`ABM_GETTASKBARPOS` はプライマリのみ返すため）。
  対応するには `Shell_SecondaryTrayWnd` を `EnumWindows` で探す必要がある。
- タスクバーを自動的に隠す設定では、矩形が画面外になるため作業領域の下端を基準にしている。
- `BehaviorMachine` にテストがない。WPF に依存してはいないが `Mendako.App` に置いてあるため、
  テストするなら `net8.0-windows` のテストプロジェクトを足すことになる。
