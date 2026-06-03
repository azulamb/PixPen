# PixPen ファイルフォーマット仕様 (.ppx)

バージョン: 0.2.0

---

## 概要

`.ppx` ファイルは **無圧縮 ZIP アーカイブ**です。 内部には JSON
メタデータファイルとレイヤーごとの PNG 画像ファイルが格納されています。

ZIP
として実装されているため、ファイルマネージャーやアーカイバーで内容を直接確認・取り出すことができます。

---

## アーカイブ構成

```
example.ppx  (ZIP アーカイブ)
├── document.json        # ドキュメントメタデータ
├── layer_0000.png       # レイヤー 0 の画像データ
├── layer_0001.png       # レイヤー 1 の画像データ
└── ...                  # 最大 100 レイヤー
```

### エントリの命名規則

| エントリ名       | 説明                                                |
| ---------------- | --------------------------------------------------- |
| `document.json`  | ドキュメント全体のメタデータ（必須）                |
| `layer_XXXX.png` | レイヤー画像。`XXXX` は 0 始まりの 4 桁ゼロ埋め整数 |

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
  "Name": "Pen", // ペン名
  "Shape": "Round", // "Round" | "Square"
  "Size": 10.0, // 基本サイズ (px)
  "Opacity": 1.0, // 不透明度 (0.0–1.0)
  "PressureAffectsSize": true, // 筆圧がサイズに影響するか
  "PressureAffectsOpacity": false, // 筆圧が不透明度に影響するか
  "MinSizeFactor": 0.0 // 筆圧 0 時のサイズ比率 (0.0–1.0)
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
  "Visible": true, // 表示/非表示
  "OffsetX": 0.0, // X オフセット (px)
  "OffsetY": 0.0, // Y オフセット (px)
  "SpacingX": 8.0, // X 間隔 (px)
  "SpacingY": 8.0, // Y 間隔 (px)
  "Color": "#FF808080", // 線の色 (#AARRGGBB)
  "LineType": "Solid" // "Solid" | "Dashed"
}
```

### Layers 配列（メタデータのみ）

```jsonc
[
  {
    "Index":     0,          // レイヤーインデックス（layer_XXXX.png と対応）
    "Name":      "Layer 1",  // レイヤー名
    "IsVisible": true,       // 表示/非表示
    "Opacity":   1.0         // 不透明度 (0.0–1.0)
  },
  ...
]
```

> レイヤーの順序はインデックスの昇順で定義されます。\
> インデックスが小さいほど**下レイヤー**（合成時は先に描画）として扱われます。

---

## レイヤー画像 (layer_XXXX.png)

| 項目                 | 仕様                                         |
| -------------------- | -------------------------------------------- |
| フォーマット         | PNG（無圧縮で保存）                          |
| ピクセルフォーマット | BGRA32（各チャンネル 8 bit、アルファ付き）   |
| サイズ               | `document.json` の `Width` × `Height` と同一 |
| 原点                 | 左上 (0, 0)                                  |
| 背景                 | 透明（アルファ 0）で初期化                   |

PNG はアルファチャンネルを保持しているため、透過情報が完全に保存されます。

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

| 項目                          | 状態                         |
| ----------------------------- | ---------------------------- |
| ブレンドモード（Normal 以外） | 未実装（フィールド定義済み） |
| レイヤーグループ              | 未実装                       |
| テキストレイヤー              | 未実装                       |
| 調整レイヤー                  | 未実装                       |

> 新しいフィールドを追加する場合、古いバージョンで未知フィールドは無視されます（`System.Text.Json`
> のデフォルト動作）。

---

## サンプル: document.json（最小構成）

```json
{
  "Id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "Width": 1000,
  "Height": 1000,
  "Dpi": 96,
  "Title": "sample",
  "Palette": {
    "Colors": ["#FF000000", "#FFFFFFFF"],
    "ForegroundColor": "#FF000000",
    "BackgroundColor": "#FFFFFFFF"
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
    "Small": {
      "Visible": true,
      "OffsetX": 0,
      "OffsetY": 0,
      "SpacingX": 8,
      "SpacingY": 8,
      "Color": "#FF808080",
      "LineType": "Solid"
    },
    "Medium": {
      "Visible": false,
      "OffsetX": 0,
      "OffsetY": 0,
      "SpacingX": 32,
      "SpacingY": 32,
      "Color": "#FF404040",
      "LineType": "Solid"
    },
    "Large": {
      "Visible": false,
      "OffsetX": 0,
      "OffsetY": 0,
      "SpacingX": 128,
      "SpacingY": 128,
      "Color": "#FF202020",
      "LineType": "Solid"
    }
  },
  "Layers": [
    {
      "Index": 0,
      "Name": "Layer 1",
      "IsVisible": true,
      "Opacity": 1.0
    }
  ]
}
```
