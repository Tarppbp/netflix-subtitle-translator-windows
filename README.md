# Netflix 字幕本地翻译 · Windows

**简体中文** · [繁體中文](README.zh-TW.md) · [English](README.en.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

在 Windows 11 上读取 Netflix 正在显示的字幕，用本机 Ollama 实时翻译。桌面控制面板统一管理翻译开关、语言、模型、字体和字幕位置。

- 源语言和目标语言独立选择，默认日语 → 简体中文。13 个选项可任意组合，包括简体中文、繁體中文、日语、英语、韩语、法语、德语、西班牙语、意大利语、葡萄牙语、俄语、泰语和越南语。
- 只显示译文时，译文放在原字幕位置。双语模式下，译文显示在原字幕上方。
- 使用 Windows 无障碍接口或 Edge 扩展读取已经显示的字幕。桌面端与翻译引擎通过本机回环地址通信。
- 支持重复对白缓存。首次出现的字幕需要等待模型生成译文。

## 推荐配置

| 项目 | 建议 |
| --- | --- |
| 系统 | Windows 11 x64。使用配套字幕扩展时需要 Microsoft Edge。 |
| 显卡 | NVIDIA GeForce RTX 系列，建议 8 GB 或以上显存，并使用支持 CUDA 的 NVIDIA 驱动。 |
| 内存 | 建议 16 GB 或以上，给 Netflix、浏览器和本地模型留出空间。 |
| 磁盘 | 建议预留至少 10 GB SSD 空间，包含安装时的临时文件、Ollama 和默认模型。 |
| 模型 | 默认 `qwen3:4b-instruct`，首次下载约 2.5 GB。 |

实测机器为 Core Ultra 9 285H、RTX 5060 Laptop 8 GB、32 GB 内存。默认模型在一次测试中约占 2.68 GiB 显存。这是配置建议，CPU 推理或低于推荐规格时的播放延迟还有待验证。 [测试报告](测试报告.md)

## 安装

1. 从 [GitHub Releases](https://github.com/Tarppbp/netflix-subtitle-translator-windows/releases/latest) 下载并解压 Windows 安装包，或下载本仓库源码。
2. 双击 `Install.cmd`。首次安装需要联网下载 Ollama 和默认的 Qwen3 4B Instruct 模型。
3. 在 Netflix 选择字幕轨，再在控制面板选相同的「源字幕语言」和想看的「目标字幕语言」。点击「开始翻译」。以后可以双击 `Start.cmd` 打开面板。

使用「只显示译文」时，请[加载配套 Edge 扩展](使用说明.md#字幕扩展)。升级后，用新目录中的扩展并刷新 Netflix 播放页。

## 文档

- [使用说明](使用说明.md)
- [技术调查](技术调查.md)
- [测试报告与验证范围](测试报告.md)

本项目是独立开发的工具。Netflix 名称用于说明兼容平台。
