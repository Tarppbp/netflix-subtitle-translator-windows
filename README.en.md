# Netflix Local Subtitle Translator for Windows

[简体中文](README.md) · [繁體中文](README.zh-TW.md) · **English** · [日本語](README.ja.md) · [한국어](README.ko.md)

Translate Netflix subtitles as they appear on Windows 11. Ollama runs the translation on your PC. The desktop control panel handles the language pair, model, font, and subtitle position.

- Select the source and target independently from 13 options. The default is Japanese to Simplified Chinese. You can also choose pairs such as English to Japanese or French to Korean.
- Show the translation in the original position, or display it above the original subtitles in bilingual mode.
- Subtitle text stays on your PC. The tool reads subtitles the player has already displayed.

## Getting started

1. Download the Windows ZIP from [GitHub Releases](https://github.com/Tarppbp/netflix-subtitle-translator-windows/releases/latest), extract it, and run `Install.cmd`. The first installation downloads Ollama and the default Qwen3 model.
2. Pick a subtitle track in Netflix. Select that language as the source in the desktop panel, choose your target, and start translation. Use `Start.cmd` to reopen the panel later.
3. For translation-only mode, load the included `extension` folder under `edge://extensions` using Developer mode and **Load unpacked**, then refresh the Netflix playback page.

The Windows control panel is in Simplified Chinese. See the [Chinese user guide](使用说明.md) and [test report](测试报告.md) for setup details and verification limits. This is an independent project.
