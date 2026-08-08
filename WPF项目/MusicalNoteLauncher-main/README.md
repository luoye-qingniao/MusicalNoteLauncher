# 🎵 MusicalNoteLauncher（MNL 音乐笔记启动器）

> 一款为 Minecraft 玩家打造的 **音乐 + 启动 + 社交** 一体化启动器。
> 由 FallenLeaves 开发，致力于把「玩游戏」这件事，变得像听歌一样轻松、优雅。

---

## ✨ 项目简介

MusicalNoteLauncher 不是传统意义上的单一启动器——它在完整 MC 启动能力之上，融入了**音乐播放器、社区社交、AI 助手、多端支持**四大板块，让启动器不再只是「下载版本、点开始」的工具，而是玩家桌面上的娱乐中心。

- 🎼 名称含义：Music（音乐）+ Note（音符/笔记）+ Launcher（启动器）
- 🧩 架构：WPF 主界面 + WebView2 渲染引擎，本地后端服务 + Web 前端动态页面
- 🚀 多端：Windows 桌面（WPF / Electron / Photino 三套壳任选）

---

## 🎯 核心功能

### 🚀 游戏启动
- 支持 **Java 版** 与 **基岩版（Bedrock）** 双平台启动
- 原版 / Paper / Purpur / Spigot / Fabric / Forge / NeoForge 全核心支持
- **Forge / Fabric / NeoForge / OptiFine** 一键安装，自动检测模组加载器
- Java 自动检测、下载与配置管理
- 游戏崩溃分析（CrashAnalyzer）与错误日志上报（LogReporter）
- 版本扫描、修复与重复管理

### 📥 下载中心
- 游戏版本、模组、整合包、光影、材质、数据包、依赖库一站式下载
- 内置 **CurseForge / Modrinth** API 聚合搜索
- 多线程下载管理器，支持断点续传、任务队列与进度可视化
- 基岩版增强下载服务（BedrockEnhancedDownloadService）

### 🎵 音乐播放器
- 内置音乐播放与缓存管理（MusicPlayerService / MusicCacheManager）
- 音乐盒页面（MusicBoxPage），边玩游戏边听歌
- 音乐账号体系（MusicAccountService），支持登录与歌单同步

### 👥 社区与联机
- 社区页面（CommunityPage）：帖子、频道、好友动态
- 好友系统与实时聊天（FriendService）
- 多人在线联机：**EasyTier / MCTier** 组网，Redstone 红石联机
- 白名单管理与服务器配置

### 🤖 AI 助手
- 内置 AI 助手页面（AIAssistantPage）与智能体服务（AIAgentService）
- AI 工具调用（AIAgentTools）：回答问题、查攻略、辅助排障

### 🎨 外观与皮肤
- 3D 皮肤预览（Skin3DViewer，WebGL 渲染）
- 皮肤服务器（SkinServer）与自定义皮肤资源包生成
- 多套全景动态背景（9 大 MC 生态场景）
- 主题色自定义（ThemeColorService），精致暗色 UI

### 🛠️ 实用工具
- 存档管理器（SaveManagerPage）
- 模组管理器（ModManagerPage）、光影 / 材质 / 数据包 / 依赖库管理
- 内存优化器（MemoryOptimizer）
- 启动日志查看与在线更新（UpdateService）

---

## 🧩 技术栈

| 层 | 技术 |
| --- | --- |
| 桌面壳 | WPF / Electron / Photino |
| 前端 | HTML + CSS + Vue.js（本地 Web 页面，WebView2 渲染） |
| 后端 | C# .NET 8（MNL.Backend 本地 HTTP 服务） |
| 渲染 | WebView2 / Three.js（3D 皮肤、全景背景） |
| 数据 | JSON 本地持久化（SettingsManager / ConfigManager） |
| 网络 | HttpClient + 多线程下载器（Downloader） |
| 安全 | SSH.NET、微软账号 OAuth（MsAuthFlow） |

---

## 📁 多端目录说明

- `MNL.Backend/` — 本地后端服务（WebSocket + HTTP 桥接）
- `MNL.Photino/` / `PhotinoApp/` — Photino 轻量壳
- `ElectronApp/` — Electron 桌面壳
- `WebApp/` — 前端页面与静态资源（含 Vue 运行时）

---

## 🚀 快速开始（开发环境）

```powershell
# 1. 打开项目
cd MusicalNoteLauncher

# 2. 还原依赖（.NET 8 SDK + WebView2 Runtime 需预装）
dotnet restore

# 3. 构建并运行
dotnet run
```

> 提示：游戏下载与部分功能依赖网络环境，首次使用建议在设置中配置合适的镜像源。

---

## 📜 更新记录

详见 [CHANGELOG.md](CHANGELOG.md)。

---

## 🧡 致谢

感谢所有开源社区与玩家的支持。启动器中的生态图片、皮肤资源与联动功能，均归其原作者所有。

**Made with ❤️ by FallenLeaves**
