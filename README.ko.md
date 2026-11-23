# Netflix 자막 로컬 번역 · Windows

[简体中文](README.md) · [繁體中文](README.zh-TW.md) · [English](README.en.md) · [日本語](README.ja.md) · **한국어**

Windows 11에서 Netflix 플레이어에 표시되는 자막을 읽고 PC의 Ollama로 번역합니다. 데스크톱 제어판에서 번역 언어, 모델, 글꼴, 자막 위치를 설정할 수 있습니다.

- 원본 언어와 번역 언어를 13개 옵션에서 각각 선택할 수 있습니다. 기본값은 일본어에서 중국어 간체로 번역하는 설정입니다. 영어에서 일본어, 프랑스어에서 한국어 등도 선택할 수 있습니다.
- 번역만 표시하면 원본 자막 위치에, 이중 자막을 선택하면 원본 위에 번역을 표시합니다.
- 이미 표시된 자막 텍스트를 사용하며, 번역은 PC에서 처리합니다.

## 권장 사양

| 항목 | 권장 사양 |
| --- | --- |
| 운영 체제 | Windows 11 x64. 동봉된 자막 확장 프로그램을 사용하려면 Microsoft Edge가 필요합니다. |
| GPU | NVIDIA GeForce RTX 4060(데스크톱, 8 GB) 또는 VRAM이 8 GB 이상인 상위 모델을 권장합니다. CUDA를 지원하는 NVIDIA 드라이버를 사용하세요. |
| 메모리 | Netflix, 브라우저, 로컬 모델을 함께 실행하려면 RAM 16 GB 이상을 권장합니다. |
| 저장 공간 | 설치 임시 파일, Ollama, 기본 모델을 위해 SSD 여유 공간 10 GB 이상을 권장합니다. |
| 모델 | 기본값은 `qwen3:4b-instruct`이며, 최초 모델 다운로드 용량은 약 2.5 GB입니다. |

RTX 4060은 권장 기준이며, 이 GPU에서의 재생 중 지연 시간은 아직 측정하지 않았습니다. 테스트 PC는 Core Ultra 9 285H, RTX 5060 Laptop GPU(VRAM 8 GB), RAM 32 GB를 사용했습니다. 측정 시 기본 모델은 VRAM을 약 2.68 GiB 사용했습니다. [테스트 보고서(중국어)](测试报告.md)

## 사용 방법

1. [GitHub Releases](https://github.com/Tarppbp/netflix-subtitle-translator-windows/releases/latest)에서 Windows ZIP을 내려받아 압축을 풀고 `Install.cmd`를 실행합니다. 처음 설치할 때 Ollama와 기본 Qwen3 모델을 내려받습니다.
2. Netflix에서 자막 트랙을 선택합니다. 제어판에서 같은 언어를 원본 언어로 지정하고 번역 언어를 선택한 뒤 시작합니다. 다음부터는 `Start.cmd`로 제어판을 열 수 있습니다.
3. 번역만 표시하려면 `edge://extensions`에서 개발자 모드를 켜고, 압축 해제된 확장을 로드하는 메뉴로 동봉된 `extension` 폴더를 불러옵니다. 그런 다음 Netflix 재생 페이지를 새로 고칩니다.

제어판의 표시 언어는 중국어 간체입니다. 자세한 설정과 검증 범위는 [사용 안내](使用说明.md)와 [테스트 보고서](测试报告.md)(중국어)를 참고하세요. 이 프로젝트는 독립적으로 개발되었습니다.
