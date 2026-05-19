# リコレクション / Recollection

> 作品技術資料 / Technical Documentation

| 項目 | 内容 |
|------|------|
| 作品名 | リコレクション (Recollection) |
| 作成期間 | 3ヶ月（2025年4月～2025年7月） |
| 制作人数 | 13人 |
| 受賞 | ゲームクリエイター甲子園2026 総合第5位・サイバーエージェント賞・今村孝矢賞 / サイゲームスクリエイティブコンテスト2025 ゲームコンテンツ部門賞 / インディーゲームコンテスト2025 Best 12 |
| 作品ページ | https://gameparade.creators-guild.com/works/3069 |

> このリポジトリには**個人担当実装**（シェーダー・カメラワークなど）のみを収録しています。
---

## 開発環境

| 項目 | バージョン |
|------|-----------|
| OS | Windows 11 64bit |
| Engine | Unity 2022.3.59f1 |
| TextMeshPro | 3.0.6 |
| Universal Render Pipeline (URP) | 14.0.11 |
| IDE | Visual Studio 2022 Version 17.x |

---

## 実装モジュール

### BlockDissolve.shader / TeleportEffectController.cs — テレポートディゾルブ

#### 制作動機

当初のテレポート演出は、プレイヤーの周囲に複数のリングを展開し、回転しながら包み込んで瞬間移動するというものでした。
しかしチームメンバーや試遊した友人・指導教員からも「魔法のように見えてSFの世界観から外れている」という指摘を受けました。
そこでリファレンスを収集し直し、「データのパケット転送」をモチーフに、プレイヤーのメッシュをブロック単位で分解・転送するディゾルブ表現へ刷新することをチームで決定しました。
この表現を実現するために、HLSLによるカスタムシェーダーの実装に初挑戦しました。

#### 工夫した点

- ブロックのサイズをランダムに変化させることで、単調さを排除しSF的な質感を演出
- `DissolveProgress` パラメータを外部（`TeleportEffectController.cs`）から制御できるよう設計し、再利用性を向上
- マテリアルをインスタンス化（`new Material`）して使用することで、他オブジェクトへの影響を防止
- `EaseIn` / `EaseOut` 関数を実装し、演出の緩急を制御
- `StartDissolve()` と `ReverseDissolve()` を分離した設計により、呼び出し側スクリプトで `InputPermission` 等の入力制御タイミングを正確に管理

#### 苦労した点

- ワールド座標に直接ノイズをかける実装では砂状の細かい消え方になり「消滅」に見えてしまう問題 → `floor()` でグリッドに揃えてブロック状の消え方を実現
- ブロックサイズが均一で単調という問題 → 粗いグリッドでランダム値を取得してサイズを決定し、再グリッドでサイズを多様化
- Y方向の飛び上がりオフセット追加時にプレイヤー全体が引き延ばされるバグ → ブロックごとに独立した `noise` と `DissolveProgress` の比較でタイミングを個別計算
- `ReverseDissolve` 完了後の `originalMaterial` 復元漏れによる白化バグ → `DissolveAndRestore` コルーチン内で必ず `ApplyMaterial(originalMaterial)` を呼ぶことで解決

#### 注目ポイント

頂点シェーダー内でワールド座標をグリッド分割し、ブロックごとに独立したランダムノイズ値を生成している部分（`vert` 関数内）。
ノイズ値に基づいてブロックが上方向に飛んでいくオフセット処理と、フラグメントシェーダー側での `discard` によるブロック独立の消滅タイミング制御が本シェーダーの核心部分。

#### 参考資料

- **Unity公式ドキュメント — Writing HLSL shader programs**
  HLSLの頂点・フラグメントシェーダーの基本構文、`TransformObjectToWorld` / `TransformWorldToHClip` 等のURP座標変換関数の使い方
  https://docs.unity3d.com/Manual/writing-shader-programs-introduction.html

- **Cyanilux — URP Shader Tutorials**
  URPにおけるパス構成・SRP Batcherとの互換性・`UnityPerMaterial` CBUFFERの扱い
  https://www.cyanilux.com/tutorials/urp-shader-code/

> ※ ランダムノイズ生成・グリッド分割・ディゾルブ処理のロジックはすべて自身で設計・実装

---

### ScreenDoor.shader — 障害物透過（ディザリング）

#### 制作動機

本作はパズルゲームであり、ギミックを解くにはフィールド全体を観察する必要があります。
そのため、カメラと障害物に衝突判定を設けてカメラを押し戻す手法ではなく、カメラに近いオブジェクトをシェーダー側で透過させ、視界を常に確保する方針を選択しました。
カメラ挙動をシンプルに保ちつつ、パズルゲームとして最重要なプレイヤーの視認性を安定して確保できると判断したためです。

#### 工夫した点

- `ShadowCaster` パスを別途実装し、影の描画が正常に機能するよう配慮
- URPのPBRライティング（`UniversalFragmentPBR`）の計算後にディザ抜き処理を追加することで、ライティングを維持しながら距離に応じた透過を実現
- ライティングなしの軽量 Pass（Pass2）とPBRライティングありの Pass（Pass3）の両方にディザ抜き処理を実装
- `Stencil` バッファに `Ref=1` を書き込む設計により、Player・Echo・Goal など複数のシェーダーと連動

#### 苦労した点

- Pass2 と Pass3 どちらか片方だけでは一方が不透明なまま残る問題 → 両パスへの実装で解決（それぞれをコメントアウトして動作確認しながら構成を確立）
- `BayerテクスチャのUV` にオブジェクトUVを使うとカメラ移動時にパターンが泳いで見える問題 → `ComputeScreenPos()` で取得したスクリーン座標に変更して解決

#### 注目ポイント

Pass2（ライティングなし）と Pass3（PBRライティングあり）の両方に実装されているディザ抜き処理。
また Pass3 の `Stencil` バッファ（`Ref=1`）への書き込みが、Player・Echo・Goal シェーダーのシルエット描画と連動している点。
障害物領域を `Stencil=1` でマークすることで、各キャラクターシェーダーが障害物の背後にいる領域（`Stencil=2`）を検出してシルエットとして描画する仕組み。

#### 参考資料

- **DigitalRune — Screen-Door Transparency**
  Bayerマトリクスを閾値として用いるスクリーンドア透過のアルゴリズムおよび `clip()` による実装方法
  https://digitalrune.github.io/DigitalRune-Documentation/html/fa431d48-b457-4c70-a590-d44b0840ab1e.htm

- **Daniel Ilett — Transparency Dithering in Shader Graph and URP**
  URPにおけるディザリング透過の考え方とBayerテクスチャのUV計算方法
  https://danielilett.com/2020-04-19-tut5-5-urp-dither-transparency/

> ※ スクリーンドア処理部分のロジックは自身で設計・実装。ライティング処理はUnity URP標準の `UniversalFragmentPBR` を使用

---

### StageStartCameraWork.cs — ステージ開始カメラワーク

#### 制作動機

ステージ開始時にゴールの位置をプレイヤーへ伝えるカメラ演出を実装しました。
Hermite曲線を採用した理由は主に2点あります。

1. ステージ全体を俯瞰する軌跡でゴールまでを見せることで、プレイヤーがフィールドのレイアウトを直感的に把握できるようにするため
2. 直線移動ではカメラがステージ内のブロックや壁をすり抜けてしまうため、曲線で障害物を回り込む経路を取ることで視覚的な破綻を防ぐため

#### 工夫した点

- `null` 参照が発生しないよう、制御点リストのクリーンアップ処理を `Start()` 時に実行
- `Time.unscaledDeltaTime` を使用し、TimeScaleの影響を受けずに演出が再生されるよう対応
- カメラワーク完了後に各種 `InputPermission` を正確に有効化し、プレイヤー操作の開始タイミングがずれないよう制御
- `OnDrawGizmos()` によりエディタ上でパスを可視化。インスペクターから制御点を自由に増減・配置でき、プログラマー以外のメンバーでも演出調整が可能なツールとして設計

#### 苦労した点

- Hermite曲線の数式をコードに落とし込む際、接線ベクトル（`m1`, `m2`）の方向と強さの調整が難しく、意図した軌跡を描くまで試行錯誤
- カメラオフセットをプレイヤーの向きに合わせて回転させる処理でクォータニオンの掛け算の順序に注意が必要

#### 注目ポイント

`GetHermitePathPosition()` 関数内のHermite曲線計算と、`OnDrawGizmos()` によるエディタ上でのパス可視化。
インスペクターから制御点を自由に増減・配置でき、プログラマー以外のメンバーでも演出調整が可能なツール設計。

#### 参考資料

- **授業資料 / 数学的定義**
  Hermite曲線の数式を参照し、自身でコードに実装。特定のソースコードの流用なし。
