# PixPen

PixPenはシンプルなお絵かきツールです。

アンチエイリアスなしのイラストを描けます。完全に個人のイラスト練習用なので機能は多くない代わりに軽量に動きます。

https://azulamb.github.io/PixPen/

---

## ライセンス

[MIT License](./LICENSE)

---

## 使用ライブラリ

| ライブラリ | バージョン | ライセンス | 用途 |
|------------|-----------|-----------|------|
| [MahApps.Metro.IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks) | 6.2.1 | MIT / Apache 2.0 | ツールバーアイコン |

### システム依存（再配布なし）

| 依存 | 説明 |
|------|------|
| user32.dll / kernel32.dll | Windows OS 標準 API（P/Invoke） |
| WinTab32.dll | Wacom ドライバ付属 DLL（筆圧取得用・ユーザー環境に依存） |

---

## 開発環境

| 項目 | 内容 |
|------|------|
| 言語 | C# |
| フレームワーク | .NET 10 / WPF |
| UI | Windows Presentation Foundation (WPF) |
| ターゲット | Windows (win-x64) |
