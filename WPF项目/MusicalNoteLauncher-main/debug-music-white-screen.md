# Debug Session: music-white-screen

**Status**: FIXED (pending verification)
**Created**: 2026-07-22
**Updated**: 2026-07-22
**Summary**: 音乐盒页面始终白屏，CSS flex 修复后仍未解决。

## Hypotheses

1. **H1 - CSS 缓存未刷新**: WebView2 缓存了旧 CSS，`flex: 1` 修复未生效。→ 通过 cache buster v7 解决
2. **H2 - MusicPage 组件初始化异常**: MusicPage 的 `mounted()` 或 `data()` 中有运行时错误 → 待验证
3. **H3 - HTTP Server 文件路径不匹配**: 修改了错误的目录，实际服务的文件仍是旧版本 → 已同步 bin/Debug 和源 WebApp
4. **H4 - Flex 链在 WebView2 中未生效**: WebView2/Chromium 的 flex 布局行为与标准有差异 → 通过对比 UnoApp 的 CSS 修复
5. **H5 - `.page.active` display:none 未切换**: Vue 的 `:class` 绑定未正确添加 `active` 类 → 待验证

## Root Cause Analysis

**CSS 重复定义冲突**:
- style.css 中有两套音乐盒 CSS 定义（第1010行和第4053行）
- 第二套（4053-4126）使用 `display: grid` 覆盖了第一套的 `display: flex`
- `.mp-content` 从 flex 变成了 grid，导致子元素布局不一致
- `.mp-container` 第一套缺失 `flex: 1; min-height: 0`
- `.mp-search` 第一套缺失 `flex-shrink: 0`
- `.music-page` 只在第二套中有定义

## Changes Made

1. **删除重复 CSS** (第4053-4126行) — 从 bin/Debug 和源 WebApp 的 style.css 中移除
2. **修复第一套 CSS 定义**:
   - 添加 `.music-page { padding: 0; flex: 1; min-height: 0; display: flex; flex-direction: column; overflow: hidden; }`
   - 修复 `.mp-container` 添加 `flex: 1; min-height: 0;`
   - 修复 `.mp-search` 添加 `flex-shrink: 0;`
3. **清理调试代码**:
   - 移除 index.html 中的绿色调试栏 `NAV-OK`
   - 移除 index.html 中的 component-check debug region
   - 同步 clean v7 music.js 到源 WebApp（移除 _dbgSend 和红色调试条）
4. **更新缓存版本号**:
   - CSS: `themes.css?v=7`, `style.css?v=7`
   - music.js: `music.js?v=7`

## Flex Chain (after fix)

```
#main-content → flex column, flex:1, min-height:0
  .page.active → flex column, flex:1, min-height:0, overflow:hidden
    .page-content.music-page → flex column, flex:1, min-height:0, overflow:hidden
      .mp-container → flex column, flex:1, min-height:0
        .mp-search → flex-shrink:0  ← FIXED
        .mp-content → flex, flex:1 (no longer grid!)
          .mp-left → flex:3
          .mp-right → flex:2
        .mp-player → flex-shrink:0 ✓ (was already correct)
```

## Verification

需要用户启动应用验证：
1. 音乐盒是否正常显示（搜索栏、推荐歌单、播放队列、底部播放器）
2. 版本卡片是否正确从网络获取版本
