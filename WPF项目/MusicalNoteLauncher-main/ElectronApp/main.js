const { app, BrowserWindow, ipcMain } = require('electron');
const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');

let mainWindow = null;
let backendProcess = null;
let backendPort = 0;

// ── 查找 C# 后端可执行文件 ──
function findBackendExe() {
  const isPackaged = app.isPackaged;
  const basePath = isPackaged ? path.dirname(app.getPath('exe')) : __dirname;

  const names = [
    // 打包后：resources/backend/
    path.join(basePath, 'resources', 'backend', 'MNL.Backend.exe'),       // Windows
    path.join(basePath, 'resources', 'backend', 'MNL.Backend'),            // Linux/macOS
    // 开发模式：MNL.Backend/bin/...
    path.join(basePath, '..', 'MNL.Backend', 'bin', 'Release', 'net8.0', 'MNL.Backend.exe'),
    path.join(basePath, '..', 'MNL.Backend', 'bin', 'Release', 'net8.0', 'MNL.Backend'),
  ];

  for (const n of names) {
    if (fs.existsSync(n)) return n;
  }
  // .NET 发布后的目录结构
  if (!isPackaged) {
    const devDir = path.join(__dirname, '..', 'MNL.Backend', 'bin', 'Release', 'net8.0', 'publish');
    if (fs.existsSync(devDir)) {
      const found = fs.readdirSync(devDir).find(f =>
        f.startsWith('MNL.Backend') && !f.endsWith('.pdb')
      );
      if (found) return path.join(devDir, found);
    }
  }
  return null;
}

// ── 启动 C# 后端 ──
function startBackend() {
  return new Promise((resolve, reject) => {
    const exePath = findBackendExe();
    if (!exePath) {
      reject(new Error('未找到 C# 后端程序 (MNL.Backend)'));
      return;
    }
    console.log('[Electron] 启动后端:', exePath);

    const env = { ...process.env };
    backendProcess = spawn(exePath, [], {
      env,
      stdio: ['ignore', 'pipe', 'pipe'],
      windowsHide: true,
    });

    let started = false;

    backendProcess.stdout.on('data', (data) => {
      const text = data.toString();
      console.log('[Backend]', text.trim());

      const match = text.match(/PORT:(\d+)/);
      if (match && !started) {
        started = true;
        backendPort = parseInt(match[1]);
        console.log('[Electron] 后端端口:', backendPort);
        resolve(backendPort);
      }
    });

    backendProcess.stderr.on('data', (data) => {
      console.error('[Backend ERR]', data.toString().trim());
    });

    backendProcess.on('error', (err) => {
      console.error('[Electron] 后端启动失败:', err);
      if (!started) reject(err);
    });

    backendProcess.on('exit', (code) => {
      console.log('[Electron] 后端退出, code:', code);
      if (!started) reject(new Error(`后端退出码: ${code}`));
    });

    // 30 秒超时
    setTimeout(() => {
      if (!started) reject(new Error('后端启动超时'));
    }, 30000);
  });
}

// ── 创建主窗口 ──
async function createWindow() {
  try {
    await startBackend();
  } catch (err) {
    console.error('[Electron] 后端启动失败，无法继续:', err.message);
    app.quit();
    return;
  }

  mainWindow = new BrowserWindow({
    width: 1280,
    height: 800,
    minWidth: 900,
    minHeight: 600,
    frame: true,
    autoHideMenuBar: true,
    icon: path.join(__dirname, '..', 'Assets', '图标.ico'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      webviewTag: false,
      autoplayPolicy: 'no-user-gesture-required',
    },
    backgroundColor: '#0a0a0f',
  });

  mainWindow.loadURL(`http://localhost:${backendPort}/index.html`);

  mainWindow.webContents.on('did-finish-load', () => {
    console.log('[Electron] 页面加载完成');
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

// ── IPC：窗口控制 ──
ipcMain.on('window-minimize', () => {
  mainWindow?.minimize();
});
ipcMain.on('window-maximize', () => {
  if (mainWindow?.isMaximized()) {
    mainWindow.unmaximize();
  } else {
    mainWindow?.maximize();
  }
});
ipcMain.on('window-close', () => {
  app.quit();
});

// ── 应用生命周期 ──
app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  cleanup();
  app.quit();
});

app.on('before-quit', () => {
  cleanup();
});

function cleanup() {
  if (backendProcess) {
    console.log('[Electron] 关闭后端进程...');
    try { backendProcess.kill('SIGTERM'); } catch {}
    setTimeout(() => {
      try { backendProcess.kill('SIGKILL'); } catch {}
    }, 3000);
  }
}

// macOS: 点击 dock 图标重新创建窗口
app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow();
  }
});
