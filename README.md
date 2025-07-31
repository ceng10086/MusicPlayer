# 🎵 MusicPlayer

一个功能强大、界面现代的 C# WPF 本地音乐播放器，采用 Fluent Design 设计语言，支持多种音频格式播放、歌词显示、音频可视化等功能。

![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square)
![WPF](https://img.shields.io/badge/WPF-UI-blue?style=flat-square)

## ✨ 主要特性

### 🎧 音频播放功能
- **多格式支持**: MP3, WAV, FLAC, M4A, OGG/OGA, AAC, WMA 等主流音频格式
- **完整播放控制**: 播放/暂停/停止、上一首/下一首、进度跳转
- **音量控制**: 音量调节滑块和静音功能
- **音频信息展示**: 显示歌曲标题、艺术家、专辑信息和专辑封面

### 🎤 歌词功能
- **多格式歌词**: 支持内嵌歌词和外部 `.srt`、`.lrc` 歌词文件
- **双语歌词**: 自动识别并同时显示中英文双语歌词
- **自动同步**: 歌词根据播放进度自动滚动，当前行高亮显示
- **智能加载**: 自动加载与音乐文件同名的歌词文件

### 📚 播放列表
- **本地音乐导入**: 支持单文件或批量导入音乐文件
- **搜索筛选**: 快速搜索和筛选播放列表中的歌曲
- **折叠功能**: 支持播放列表折叠/展开，节省界面空间
- **数据持久化**: 使用 JSON 文件本地存储播放列表信息
- **便携设计**: 所有数据文件与程序在同一目录，便于移植

### 🎨 现代化界面
- **Fluent Design**: 采用微软 Fluent Design 设计语言
- **毛玻璃效果**: Mica 材质背景，现代半透明视觉效果
- **自定义窗口**: 自定义标题栏设计，与应用内容风格统一
- **圆形封面**: 专辑封面圆形显示，播放时模拟唱片旋转动画

### 🎵 音频可视化
- **实时频谱**: 基于 FFT 的音频频谱分析器
- **动态效果**: 跳动的柱状图与音乐节奏同步
- **视觉反馈**: 丰富的视觉效果增强音乐体验

## 🏗️ 技术架构

### 核心技术栈
- **开发框架**: C# + WPF (.NET 8.0)
- **UI 框架**: [WPF-UI 4.0.3](https://github.com/lepoco/wpfui) - 现代化 WPF UI 组件库
- **音频处理**: [NAudio 2.2.1](https://github.com/naudio/NAudio) + NAudio.Vorbis 1.5.0
- **元数据处理**: [TagLib-Sharp 2.3.0](https://github.com/mono/taglib-sharp)
- **数据序列化**: System.Text.Json

### 架构模式
- **设计模式**: MVVM (Model-View-ViewModel)
- **数据绑定**: WPF 数据绑定和命令模式
- **音频引擎**: NAudio WaveOut + 自定义频谱分析器
- **文件系统**: 本地 JSON 存储，绿色便携式设计

## 📦 项目结构

```
MusicPlayer/
├── App.xaml/cs                    # 应用程序入口
├── MainWindow.xaml/cs             # 主窗口界面
├── MusicPlayer.csproj              # 项目配置文件
├── Audio/                          # 音频处理模块
│   ├── SpectrumAnalyzer.cs        # 频谱分析器
│   └── VorbisAudioFileReader.cs   # OGG 格式音频读取器
├── Converters/                     # WPF 值转换器
│   ├── SpectrumHeightConverter.cs # 频谱高度转换器
│   └── PlaylistWidthConverter.cs  # 播放列表宽度转换器
├── Models/                         # 数据模型
│   ├── Song.cs                    # 歌曲信息模型
│   └── LyricLine.cs               # 歌词行模型
├── Services/                       # 业务逻辑服务
│   └── PlaylistService.cs         # 播放列表管理服务
├── ViewModels/                     # 视图模型
│   ├── MainViewModel.cs           # 主视图模型
│   └── ObservableObject.cs       # MVVM 基类
└── docx/                          # 文档目录
    └── 需求文档.md                 # 项目需求文档
```

## 🚀 快速开始

### 环境要求
- **操作系统**: Windows 10/11
- **开发环境**: .NET 8.0 SDK
- **IDE**: Visual Studio 2022 或 VS Code

### 编译运行

#### 1. 克隆项目
```bash
git clone <repository-url>
cd MusicPlayer
```

#### 2. 还原 NuGet 包
```bash
dotnet restore
```

#### 3. 编译项目
```bash
# 调试版本
dotnet build

# 发布版本
dotnet build -c Release
```

#### 4. 运行程序
```bash
dotnet run
```

### 发布部署

#### 框架依赖发布 (推荐)
```bash
dotnet publish -c Release -o ./publish/framework-dependent
```
> 需要目标机器安装 .NET 8.0 运行时

#### 自包含发布
```bash
dotnet publish -c Release --self-contained true -r win-x64 -o ./publish/self-contained
```
> 包含 .NET 运行时，目标机器无需安装 .NET

#### 单文件发布 (最简洁)
```bash
dotnet publish -c Release --self-contained true -r win-x64 -p:PublishSingleFile=true -o ./publish/single-file
```
> 打包为单个可执行文件，便于分发

## 🎯 使用指南

### 基本操作
1. **导入音乐**: 点击"导入音乐"按钮，选择单个或多个音乐文件
2. **播放音乐**: 双击歌曲列表中的歌曲开始播放
3. **播放控制**: 使用底部播放控制面板进行播放/暂停/切换操作
4. **搜索歌曲**: 在搜索框中输入关键词快速查找歌曲
5. **折叠播放列表**: 点击播放列表右上角的箭头按钮折叠/展开播放列表

### 高级功能
- **歌词显示**: 放置同名的 `.lrc` 或 `.srt` 歌词文件在音乐文件同目录
- **双语歌词**: 歌词文件中相同时间戳的行会自动组合显示
- **音频可视化**: 播放时底部会显示实时音频频谱效果
- **封面旋转**: 播放时专辑封面会模拟唱片旋转

### 数据存储
- 播放列表信息保存在程序目录的 `playlist.json` 文件中
- 程序采用绿色便携设计，不会在系统其他位置创建配置文件
- 可以整体移动程序文件夹到其他位置使用

## 🔧 开发说明

### 核心组件

#### MainViewModel.cs
- 主要业务逻辑和数据绑定
- 音频播放控制和状态管理
- 歌词解析和同步显示
- 频谱数据处理

#### SpectrumAnalyzer.cs
- 实现 ISampleProvider 接口
- 实时 FFT 音频频谱分析
- 提供可视化数据源

#### PlaylistService.cs
- 音乐文件元数据读取
- JSON 数据序列化和持久化
- 多格式音频文件支持

#### PlaylistWidthConverter.cs
- 播放列表宽度动态转换器
- 支持折叠/展开状态的布局切换
- 响应式界面布局管理

### 依赖包说明
- **WPF-UI**: 提供 Fluent Design 风格的现代 WPF 控件
- **NAudio**: 强大的 .NET 音频处理库
- **NAudio.Vorbis**: OGG/Vorbis 格式支持
- **TagLib-Sharp**: 音频元数据读取（标题、艺术家、封面等）

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

### 提交 Issue
- 详细描述问题或功能需求
- 提供复现步骤（如果是 Bug）
- 包含系统环境信息

### 提交 Pull Request
- Fork 项目并创建特性分支
- 遵循现有代码风格
- 添加适当的注释和文档
- 确保代码编译通过

## 🎉 致谢

- [WPF-UI](https://github.com/lepoco/wpfui) - 现代化 WPF UI 框架
- [NAudio](https://github.com/naudio/NAudio) - .NET 音频处理库
- [TagLib-Sharp](https://github.com/mono/taglib-sharp) - 多媒体元数据库

---

<div align="center">

**🎵 享受音乐，享受编程！ 🎵**

</div>
