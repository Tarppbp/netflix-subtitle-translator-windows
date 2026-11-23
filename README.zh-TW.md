# Netflix 字幕本機翻譯 · Windows

[简体中文](README.md) · **繁體中文** · [English](README.en.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

在 Windows 11 上讀取 Netflix 已顯示的字幕，由電腦上的 Ollama 即時翻譯。從桌面控制面板選擇翻譯語言、模型、字型與字幕位置。

- 來源和目標語言各有 13 個選項，預設日文翻譯成簡體中文。也可以選擇英譯日、法譯韓等組合。
- 僅顯示譯文時，譯文放在原字幕的位置。雙語模式會把譯文放在原字幕上方。
- 翻譯在電腦上處理，只讀取播放器已顯示的字幕文字。

## 安裝和使用

1. 從 [GitHub Releases](https://github.com/Tarppbp/netflix-subtitle-translator-windows/releases/latest) 下載 Windows ZIP，解壓縮後執行 `Install.cmd`。首次安裝會下載 Ollama 和預設的 Qwen3 模型。
2. 在 Netflix 選擇字幕軌，在控制面板將來源語言設為相同的語言，接著選擇目標語言並開始翻譯。下次可以用 `Start.cmd` 開啟面板。
3. 使用僅顯示譯文模式時，在 `edge://extensions` 啟用開發人員模式並載入隨附的 `extension` 資料夾，再重新整理 Netflix 播放頁面。

桌面控制面板使用簡體中文。[使用說明](使用说明.md)與[測試報告](测试报告.md)有詳細設定及驗證範圍。本工具為獨立開發的專案。
