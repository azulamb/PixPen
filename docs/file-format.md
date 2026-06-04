# PixPen ファイルフォーマット仕様 (.ppx)

バージョン: 0.2.0

---

## 概要

`.ppx` ファイルは **無圧縮 ZIP アーカイブ**です。内部には JSON
メタデータファイルとレイヤーごとの PNG 画像ファイルが格納されています。

ZIP として実装されているため、ファイルマネージャーやアーカイバーで内容を直接確認・取り出すことができます。

---

## アーカイブ構成

```
example.ppx  (ZIP アーカイブ)
├── document.json        # ドキュメントメタデータ
├── layer_0000.png       # 通常レイヤー 0 の画像データ
├── layer_0001.png       # 通常レイヤー 1 の画像データ
├── ref_0002.png         # 参照レイヤー 2 の元画像データ
└── ...                  # 最大 100 レイヤー
```

### エントリの命名規則

| エントリ名       | 説明 |
| ---------------- | ---- |
| `document.json`  | ドキュメント全体のメタデータ（必須） |
| `layer_XXXX.png` | **通常レイヤー**の画像。`XXXX` は 0 始まりの 4 桁ゼロ埋め整数 |
| `ref_XXXX.png`   | **参照レイヤー**の元画像。インデックスは `layer_XXXX.png` と同じ体系 |

> 同一インデックスに `layer_XXXX.png` と `ref_XXXX.png` が共存することはありません。  
> `document.json` の `IsReference` フラグでどちらを使うか判断します。

---

## document.json

UTF-8 エンコードの JSON ファイルです。

### トップレベル構造

```jsonc
{
  "Id":     "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // GUID（ドキュメント識別子）
  "Width":  1000,        // キャンバス幅 (px)
  "Height": 1000,        // キャンバス高さ (px)
  "Dpi":    96,          // 解像度
  "Title":  "myfile",    // ドキュメントタイトル
  "Palette": { ... },    // カラーパレット
  "Pens":   [ ... ],     // ペン定義リスト
  "Grid":   { ... },     // グリッド設定
  "Layers": [ ... ]      // レイヤーメタデータリスト
}
```

### Palette オブジェクト

```jsonc
{
  "Colors": [
    "#FF000000",   // 色リスト（#AARRGGBB 形式）
    "#FFFFFFFF",
    ...
  ],
  "ForegroundColor": "#FF000000",  // 前景色
  "BackgroundColor": "#FFFFFFFF"   // 背景色
}
```

> **色フォーマット**: `#AARRGGBB`（8 桁 16 進数、上位 2 桁がアルファ値）

### Pen オブジェクト（配列要素）

```jsonc
{
  "Name": "Pen",                   // ペン名
  "Shape": "Round",                // "Round" | "Square"
  "Size": 10.0,                    // 基本サイズ (px)
  "Opacity": 1.0,                  // 不透明度 (0.0–1.0)
  "PressureAffectsSize": true,     // 筆圧がサイズに影響するか
  "PressureAffectsOpacity": false, // 筆圧が不透明度に影響するか
  "MinSizeFactor": 0.0             // 筆圧 0 時のサイズ比率 (0.0–1.0)
}
```

### Grid オブジェクト

3 段階（Small / Medium / Large）のグリッド設定を持ちます。

```jsonc
{
  "Small":  { ... },   // 小グリッド
  "Medium": { ... },   // 中グリッド
  "Large":  { ... }    // 大グリッド
}
```

各グリッドの構造:

```jsonc
{
  "Visible":  true,        // 表示/非表示
  "OffsetX":  0.0,         // X オフセット (px)
  "OffsetY":  0.0,         // Y オフセット (px)
  "SpacingX": 8.0,         // X 間隔 (px)
  "SpacingY": 8.0,         // Y 間隔 (px)
  "Color":    "#FF808080", // 線の色 (#AARRGGBB)
  "LineType": "Solid"      // "Solid" | "Dashed"
}
```

### Layers 配列

レイヤーメタデータの一覧です。通常レイヤーと参照レイヤーで共通のフィールドと、参照レイヤー専用のフィールドがあります。

#### 共通フィールド

```jsonc
[
  {
    "Index":       0,         // レイヤーインデックス（PNG ファイル名の XXXX と対応）
    "Name":        "Layer 1", // レイヤー名
    "IsVisible":   true,      // 表示/非表示
    "IsLocked":    false,     // ロック状態（ロック中は描画不可）
    "Opacity":     1.0,       // 不透明度 (0.0–1.0)
    "IsReference": false,     // 参照レイヤーかどうか（省略時 false）
    ...                       // 参照レイヤーの場合は下記フィールドが追加
  }
]
```

> レイヤーの順序はインデックスの昇順で定義されます。  
> インデックスが小さいほど**下レイヤー**（合成時は先に描画）として扱われます。

#### 参照レイヤー専用フィールド（`IsReference: true` の場合のみ）

```jsonc
{
  "IsReference": true,
  "RefX":        100.0,  // キャンバス上の表示 X 座標 (px)
  "RefY":        50.0,   // キャンバス上の表示 Y 座標 (px)
  "RefWidth":    800.0,  // 表示幅 (px)。0 の場合は元画像の幅をそのまま使用
  "RefHeight":   600.0   // 表示高さ (px)。0 の場合は元画像の高さをそのまま使用
}
```

---

## 通常レイヤー画像 (layer_XXXX.png)

| 項目                 | 仕様 |
| -------------------- | ---- |
| フォーマット         | PNG（無圧縮で保存） |
| ピクセルフォーマット | BGRA32（各チャンネル 8 bit、アルファ付き） |
| サイズ               | `document.json` の `Width` × `Height` と同一 |
| 原点                 | 左上 (0, 0) |
| 背景                 | 透明（アルファ 0）で初期化 |

PNG はアルファチャンネルを保持しているため、透過情報が完全に保存されます。

---

## 参照レイヤー画像 (ref_XXXX.png)

参照レイヤー（下書き・資料表示用）は通常レイヤーとは異なる扱いをします。

| 項目                 | 仕様 |
| -------------------- | ---- |
| フォーマット         | PNG（無圧縮で保存） |
| ピクセルフォーマット | BGRA32（各チャンネル 8 bit、アルファ付き） |
| サイズ               | **元画像そのまま**（キャンバスサイズと無関係） |
| 原点                 | 左上 (0, 0) |

### 通常レイヤーとの相違点

| 項目 | 通常レイヤー | 参照レイヤー |
| ---- | ------------ | ------------ |
| PNG のサイズ | キャンバスと同一 | 元画像の原寸 |
| 描画ツール | ペン・消しゴム等で描画可 | 描画不可（変形のみ） |
| キャンバスリサイズ | PNG もリサイズされる | 影響を受けない（元画像を保持） |
| 表示位置 | キャンバス全体に固定 | `RefX`/`RefY` で自由に配置 |
| 表示サイズ | キャンバスと同一 | `RefWidth`/`RefHeight` でスケーリング |

### 合成・エクスポート時の動作

- 参照レイヤーは `RefX`/`RefY`/`RefWidth`/`RefHeight` の変換を適用してキャンバス座標系にスケーリングします。
- キャンバス範囲外にはみ出した部分はクリップされます。
- `IsVisible: false` の場合はエクスポートにも含まれません。

---

## 保存の仕組み（アトミック保存）

書き込み中のクラッシュで既存ファイルが破損しないよう、次の手順で保存します。

```
1. <path>.tmp に書き込む
2. 書き込み成功後、<path> を削除
3. <path>.tmp を <path> にリネーム
```

保存に失敗した場合、`.tmp` ファイルは削除されます。

---

## 将来の拡張予定

| 項目                          | 状態 |
| ----------------------------- | ---- |
| ブレンドモード（Normal 以外） | 未実装（フィールド定義済み） |
| レイヤーグループ              | 未実装 |
| テキストレイヤー              | 未実装 |
| 調整レイヤー                  | 未実装 |

> 新しいフィールドを追加する場合、古いバージョンで未知フィールドは無視されます（`System.Text.Json` のデフォルト動作）。

---

## サンプル: document.json（通常レイヤーと参照レイヤーの混在）

```json
{
  "Id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "Width": 1000,
  "Height": 1000,
  "Dpi": 96,
  "Title": "sample",
  "Palette": {
    "Colors": ["#FF141820", "#FFF5F8FF"],
    "ForegroundColor": "#FF141820",
    "BackgroundColor": "#FFF5F8FF"
  },
  "Pens": [
    {
      "Name": "Pen",
      "Shape": "Round",
      "Size": 10.0,
      "Opacity": 1.0,
      "PressureAffectsSize": true,
      "PressureAffectsOpacity": false,
      "MinSizeFactor": 0.0
    }
  ],
  "Grid": {
    "Small":  { "Visible": true,  "OffsetX": 0, "OffsetY": 0, "SpacingX": 8,   "SpacingY": 8,   "Color": "#FF808080", "LineType": "Solid" },
    "Medium": { "Visible": false, "OffsetX": 0, "OffsetY": 0, "SpacingX": 32,  "SpacingY": 32,  "Color": "#FF404040", "LineType": "Solid" },
    "Large":  { "Visible": false, "OffsetX": 0, "OffsetY": 0, "SpacingX": 128, "SpacingY": 128, "Color": "#FF202020", "LineType": "Solid" }
  },
  "Layers": [
    {
      "Index": 0,
      "Name": "下書き参照",
      "IsVisible": true,
      "IsLocked": false,
      "Opacity": 0.5,
      "IsReference": true,
      "RefX": -100.0,
      "RefY": 0.0,
      "RefWidth": 1200.0,
      "RefHeight": 900.0
    },
    {
      "Index": 1,
      "Name": "Layer 1",
      "IsVisible": true,
      "IsLocked": false,
      "Opacity": 1.0,
      "IsReference": false,
      "RefX": 0.0,
      "RefY": 0.0,
      "RefWidth": 0.0,
      "RefHeight": 0.0
    }
  ]
}
```

このサンプルに対応するアーカイブ構成:

```
sample.ppx
├── document.json   # 上記 JSON
├── ref_0000.png    # 下書き参照の元画像（原寸 PNG）
└── layer_0001.png  # Layer 1 の画像（1000×1000 px）
```
