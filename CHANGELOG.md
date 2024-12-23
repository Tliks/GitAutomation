# Changelog

## [Unreleased]
### Added
- IPreprocessBuildWithReportを用いて非VRCSDK環境にも対応
- コミット履歴の表示ウィンドウを追加
- Restore機能を追加
    - `checkout {dst.Hash} -- .`
    - HEADを保ったまま指定したコミットまで状態を戻し、コミットします
    - 指定したコミットまで状態を戻すコミットを生成することで歴史改変を行いません。
    - 実験的につき不安定な可能性があります。
    - 失敗した場合は実行前のコミットへのresetを試行します。
- ToolBarに手動コミットを実行するボタンを追加
- ToolBarにコミット履歴の表示/Restore機能を実行するウィンドウを開くボタンを追加

### Changed
- `push {remoteName} {branchname}` =>  `push {remoteName} HEAD`
- デバッグログを改善
- コミット前にアセットの更新及びシーンの保存を行うように変更

### Deprecated

### Removed
- ブランチ名の設定項目を削除

### Fixed

### Security

## [0.1.0-beta.2] - 2024-12-13
### Changed
- コミットメッセージを簡素化
- 設定の保存をプロジェクトごとに独立させるよう変更
- 設定を行うUIの微調整
- `git add .`をメインスレッドで行うように変更

### Fixed
- 設定がドメインのリロード時に初期化される問題を修正

## [0.1.0-beta.1] - 2024-12-06
### Added
- initial release