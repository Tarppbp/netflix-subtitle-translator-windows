# Netflix Local Subtitle Translator for Windows

[简体中文](README.md) · [繁體中文](README.zh-TW.md) · **English** · [日本語](README.ja.md) · [한국어](README.ko.md)

Translate Netflix subtitles as they appear on Windows 11. Ollama runs the translation on your PC. The desktop control panel handles the language pair, model, font, and subtitle position.

- Select the source and target independently from 13 options. The default is Japanese to Simplified Chinese. You can also choose pairs such as English to Japanese or French to Korean.
- Show the translation in the original position, or display it above the original subtitles in bilingual mode.
- Subtitle text stays on your PC. The tool reads subtitles the player has already displayed.

## Recommended configuration

| Component | Recommendation |
| --- | --- |
| OS | Windows 11 x64. Microsoft Edge is required for the included subtitle extension. |
| GPU | NVIDIA GeForce RTX with at least 8 GB of VRAM and a CUDA-capable NVIDIA driver. |
| Memory | 16 GB of RAM or more for Netflix, the browser, and the local model. |
| Storage | At least 10 GB of free SSD space for temporary installation files, Ollama, and the default model. |
| Model | `qwen3:4b-instruct` by default, with an initial model download of about 2.5 GB. |

The test PC had a Core Ultra 9 285H, an RTX 5060 Laptop GPU with 8 GB of VRAM, and 32 GB of RAM. The default model used about 2.68 GiB of VRAM in one test. Playback latency with CPU inference or lower specifications remains unverified. [Test report](测试报告.md)

## Getting started

1. Download the Windows ZIP from [GitHub Releases](https://github.com/Tarppbp/netflix-subtitle-translator-windows/releases/latest), extract it, and run `Install.cmd`. The first installation downloads Ollama and the default Qwen3 model.
2. Pick a subtitle track in Netflix. Select that language as the source in the desktop panel, choose your target, and start translation. Use `Start.cmd` to reopen the panel later.
3. For translation-only mode, load the included `extension` folder under `edge://extensions` using Developer mode and **Load unpacked**, then refresh the Netflix playback page.

The Windows control panel is in Simplified Chinese. See the [Chinese user guide](使用说明.md) and [test report](测试报告.md) for setup details and verification limits. This is an independent project.
