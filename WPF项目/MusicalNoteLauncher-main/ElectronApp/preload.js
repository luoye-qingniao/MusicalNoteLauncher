const { contextBridge, ipcRenderer } = require('electron');

/**
 * Electron preload 脚本。
 * 提供一个精简的 bridge 接口，前端 bridge.js 在非 WebView2 环境下自动回退到 HTTP API。
 * 这里只处理 Electron 特有的窗口控制。
 */

contextBridge.exposeInMainWorld('electron', {
  // 窗口控制（bridge.js 中 MNL.window.xxx 的底层实现）
  window: {
    minimize: () => ipcRenderer.send('window-minimize'),
    maximize: () => ipcRenderer.send('window-maximize'),
    close: () => ipcRenderer.send('window-close'),
  },

  // 获取后端端口（bridge.js SSE 连接用）
  getBackendPort: () => {
    return window.location.port;
  },

  // 平台信息
  platform: process.platform,
});
