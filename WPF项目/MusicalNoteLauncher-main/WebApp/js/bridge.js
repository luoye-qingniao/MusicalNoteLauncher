/* ============================================================
   bridge.js - MNL WebView2 / Photino ↔ C# 通信桥接层
   ============================================================ */

// Photino 通信：消息 ID 管理
var _mnlPending = {};
var _mnlMsgId = 0;

/**
 * MNL API 对象 - 通过 WebView2 HostObjects 或 Photino 调用 C# 方法
 */
const MNL = {
    /**
     * 调用 C# 方法（Promise 风格）
     * @param {string} method - C# 方法名
     * @param {...any} args - 参数
     */
    async invoke(method, ...args) {
        try {
            // WPF WebView2 模式
            if (window.chrome?.webview?.hostObjects?.mnl) {
                return await window.chrome.webview.hostObjects.mnl[method](...args);
            }
            // Photino 模式：window.external.sendMessage
            if (!window.external || !window.external.sendMessage) {
                throw new Error('MNL bridge not available (Photino external not ready)');
            }
            return new Promise(function(resolve, reject) {
                var id = ++_mnlMsgId;
                _mnlPending[id] = { resolve: resolve, reject: reject,
                    timer: setTimeout(function() {
                        delete _mnlPending[id];
                        reject(new Error('MNL invoke timeout: ' + method));
                    }, 30000)
                };
                window.external.sendMessage(JSON.stringify({ id: id, method: method, args: args }));
            });
        } catch (err) {
            console.error('[MNL] invoke error:', method, err);
            throw err;
        }
    },

    /* === 窗口控制 === */
    window: {
        minimize: () => MNL.invoke('Minimize'),
        maximize: () => MNL.invoke('Maximize'),
        close: () => MNL.invoke('Close'),
    },

    /* === 版本管理 === */
    versions: {
        getInstalled: () => MNL.invoke('GetInstalledVersions'),
        getInstalledBedrock: () => MNL.invoke('GetInstalledBedrockVersions'),
        getAvailable: (type) => MNL.invoke('GetAvailableVersions', type || 'release'),
        install: (versionId, loader, directoryName, versionUrl) => MNL.invoke('InstallVersion', versionId, loader || '', directoryName || '', versionUrl || ''),
        cancelInstall: (versionId) => MNL.invoke('CancelInstall', versionId),
        launch: (versionId, options) => MNL.invoke('LaunchGame', versionId, JSON.stringify(options || {})),
        /// 检查 Java 兼容性，返回 { compatible, currentJavaMajor, requiredMin, requiredMax, reason, recommendedPath, recommendedMajor, hasAlternatives }
        checkJava: async (versionId) => {
            try {
                var raw = await MNL.invoke('CheckJavaCompatibility', versionId);
                if (typeof raw === 'string') return JSON.parse(raw);
                return raw;
            } catch(e) { return { compatible: true, error: e.message }; }
        },
        delete: (versionId) => MNL.invoke('DeleteVersion', versionId),
        repair: (versionId) => MNL.invoke('RepairVersion', versionId),
        // JSON 版本：绕过 COM 对象，返回纯 JS 数组（安全用于 Vue 响应式数据）
        getAvailableJson: function(type) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetAvailableVersionsJson) {
                return native.GetAvailableVersionsJson(type || 'release').then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve([]);
        },
        getInstalledJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetInstalledVersionsJson) {
                return native.GetInstalledVersionsJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve([]);
        },
        getInstalledBedrockJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetInstalledBedrockVersionsJson) {
                return native.GetInstalledBedrockVersionsJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve([]);
        },
        // 基岩版远程版本列表
        getAvailableBedrockJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetAvailableBedrockVersionsJson) {
                return native.GetAvailableBedrockVersionsJson().then(function(json) {
                    try { return JSON.parse(json); } catch(e) { return []; }
                });
            }
            // Photino fallback
            return MNL.invoke('GetAvailableBedrockVersionsJson').then(function(r) {
                if (typeof r === 'string') { try { return JSON.parse(r); } catch(e) { return []; } }
                return r || [];
            }).catch(function() { return []; });
        },
        // 安装基岩版版本
        installBedrockJson: function(versionId) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.InstallBedrockVersionJson) {
                return native.InstallBedrockVersionJson(versionId).then(function(json) { return JSON.parse(json); });
            }
            // Photino fallback
            return MNL.invoke('InstallBedrockVersionJson', versionId).then(function(r) {
                if (typeof r === 'string') { try { return JSON.parse(r); } catch(e) { return { success: false, error: e }; } }
                return r || { success: false, error: '未知错误' };
            }).catch(function(e) { return { success: false, error: e.message }; });
        },
        // 获取基岩版下载进度
        getBedrockDownloadProgressJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetBedrockDownloadProgressJson) {
                return native.GetBedrockDownloadProgressJson().then(function(json) { return JSON.parse(json); });
            }
            // Photino fallback
            return MNL.invoke('GetBedrockDownloadProgressJson').then(function(r) {
                if (typeof r === 'string') { try { return JSON.parse(r); } catch(e) { return { progress: 0, isDownloading: false }; } }
                return r || { progress: 0, isDownloading: false };
            }).catch(function() { return { progress: 0, isDownloading: false }; });
        },
        // 取消基岩版下载
        cancelBedrockDownloadJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.CancelBedrockDownloadJson) {
                return native.CancelBedrockDownloadJson().then(function(json) { return JSON.parse(json); });
            }
            // Photino fallback
            return MNL.invoke('CancelBedrockDownloadJson').then(function(r) {
                return { success: true };
            }).catch(function() { return { success: false }; });
        },
    },

    /* === 模组管理 === */
    mods: {
        getInstalled: (versionId) => MNL.invoke('GetInstalledMods', versionId || ''),
        search: (query, offset, limit) => MNL.invoke('SearchMods', query, offset || 0, limit || 50),
        install: (modId, versionId) => MNL.invoke('InstallMod', modId, versionId || ''),
        uninstall: (modId, versionId) => MNL.invoke('UninstallMod', modId, versionId || ''),
        getDetail: (modId) => MNL.invoke('GetModDetail', modId),
    },

    /* === 模组管理器（本地） === */
    modmanager: {
        detectLoadersJson: function(versionId) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DetectModLoadersJson) {
                return native.DetectModLoadersJson(versionId || '').then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ loaders: [], hasLoader: false });
        },
        getInstalledModsJson: function(versionId, contentType) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetInstalledModsJson) {
                return native.GetInstalledModsJson(versionId || '', contentType || 'mod').then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ mods: [] });
        },
        toggleModJson: function(modPath, enabled) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.ToggleModJson) {
                return native.ToggleModJson(modPath, enabled).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
        deleteModJson: function(modPath) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DeleteModJson) {
                return native.DeleteModJson(modPath).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
        installLoader: function(versionId, loader) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.InstallModLoader) {
                return native.InstallModLoader(versionId || '', loader || 'Fabric');
            }
            return Promise.reject(new Error('InstallModLoader 不可用'));
        },
    },

    /* === 存档管理 === */
    saves: {
        getWorldsJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetSavedWorldsJson) {
                return native.GetSavedWorldsJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ saves: [], hasSaves: false });
        },
        backupJson: function(folderPath) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.BackupSaveJson) {
                return native.BackupSaveJson(folderPath).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge 不可用' });
        },
        renameJson: function(folderPath, newName) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.RenameSaveJson) {
                return native.RenameSaveJson(folderPath, newName).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge 不可用' });
        },
        deleteJson: function(folderPath) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DeleteSaveJson) {
                return native.DeleteSaveJson(folderPath).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge 不可用' });
        },
        importJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.ImportSaveJson) {
                return native.ImportSaveJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge 不可用' });
        },
        launchJson: function(savePath) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.LaunchSaveJson) {
                return native.LaunchSaveJson(savePath).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge 不可用' });
        },
        list: function() { return MNL.invoke('GetSaves'); },
        backup: function(name) { return MNL.invoke('BackupSave', name); },
        restore: function(name) { return MNL.invoke('RestoreSave', name); },
    },

    /* === 下载管理 === */
    downloads: {
        getTasks: () => MNL.invoke('GetDownloadTasks'),
        cancel: (taskId) => MNL.invoke('CancelDownload', taskId),
        pause: (taskId) => MNL.invoke('PauseDownload', taskId),
        resume: (taskId) => MNL.invoke('ResumeDownload', taskId),
        searchItemsJson: function(category, query, source, version, type, page) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.SearchDownloadItemsJson) {
                return native.SearchDownloadItemsJson(category, query || '', source || '', version || '', type || '', page || 1)
                    .then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ items: [], hasMore: false });
        },
        getDependenciesJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetDependenciesJson) {
                return native.GetDependenciesJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ items: [] });
        },
        fetchBbsmcJson: function(type, query, page) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.FetchBbsmcJson) {
                return native.FetchBbsmcJson(type || 'mods', query || '', page || 1).then(function(json) { return JSON.parse(json); });
            }
            // Photino fallback
            return MNL.invoke('FetchBbsmcJson', type || 'mods', query || '', page || 1).then(function(r) {
                if (typeof r === 'string') { try { return JSON.parse(r); } catch(e) { return { items: [], hasMore: false }; } }
                return r || { items: [], hasMore: false };
            }).catch(function() { return { items: [], hasMore: false }; });
        },
    },

    /* === 账户管理 === */
    account: {
        login: (type) => MNL.invoke('Login', type || 'qingniao').then(function(r) {
            if (typeof r === 'string') { try { return JSON.parse(r); } catch(e) { return { success: false, error: r }; } }
            return r || { success: false };
        }),
        logout: () => MNL.invoke('Logout'),
        getProfile: () => MNL.invoke('GetProfile'),
        switchMode: (isOffline, username) => MNL.invoke('SwitchMode', isOffline, username || ''),
        cancelMicrosoftAuth: () => { try { MNL.invoke('CancelMicrosoftAuth'); } catch(e) {} },
    },

    /* === 设置 === */
    settings: {
        get: (key) => MNL.invoke('GetSetting', key),
        set: (key, value) => MNL.invoke('SetSetting', key, value),
        getAll: async function() {
            var raw = await MNL.invoke('GetAllSettings');
            if (typeof raw === 'string') {
                try { return JSON.parse(raw); } catch(e) { return {}; }
            }
            return raw || {};
        },
        getGameFolders: async function() {
            var raw = await MNL.invoke('GetGameFoldersJson');
            if (typeof raw === 'string') {
                try { return JSON.parse(raw); } catch(e) { return []; }
            }
            return raw || [];
        },
        selectGameFolder: (index) => MNL.invoke('SelectGameFolder', index),
        addGameFolder: (path, name) => MNL.invoke('AddGameFolderJson', path, name),
        removeGameFolder: (path) => MNL.invoke('RemoveGameFolderJson', path),
        renameGameFolder: (path, name) => MNL.invoke('RenameGameFolderJson', path, name),
        openGameFolder: (path) => MNL.invoke('OpenGameFolderJson', path),
    },

    /* === AI 浏览器操控 === */
    aiBrowse: {
        navigate: (url) => MNL.invoke('AiBrowseNavigate', url),
        click: (selector) => MNL.invoke('AiBrowseClick', selector),
        type: (selector, text) => MNL.invoke('AiBrowseType', selector, text),
        scroll: (x, y) => MNL.invoke('AiBrowseScroll', x, y),
        getContent: () => MNL.invoke('AiBrowseGetContent'),
        executeScript: (script) => MNL.invoke('AiBrowseExecuteScript', script),
        close: () => MNL.invoke('AiBrowseClose'),
    },

    /* === Java管理 === */
    java: {
        detect: () => MNL.invoke('DetectJava'),
        getList: () => MNL.invoke('GetJavaList'),
        download: (version) => MNL.invoke('DownloadJava', version),
        savePath: (path) => MNL.invoke('SaveJavaPath', path),
        getSavedPath: () => MNL.invoke('GetSavedJavaPath'),
        detectJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DetectJavaJson) {
                return native.DetectJavaJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve([]);
        },
    },

    /* === 组件商店 === */
    store: {
        getComponentsJson: function(cat, search, page) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetStoreComponentsJson) {
                return native.GetStoreComponentsJson(cat || '', search || '', page || 1).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ components: [], total: 0, page: 1 });
        },
        trackDownloadJson: function(componentId) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.TrackStoreDownloadJson) {
                return native.TrackStoreDownloadJson(componentId).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
        // === 背景（挂在 store 命名空间下，store.js 从此处调用） ===
        getBackgroundsJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetBackgroundsJson) {
                return native.GetBackgroundsJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ backgrounds: [], total: 0 });
        },
        uploadBackgroundJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.UploadBackgroundJson) {
                return native.UploadBackgroundJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
        useBackgroundJson: function(id) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.UseBackgroundJson) {
                return native.UseBackgroundJson(id || '').then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
        deleteBackgroundJson: function(id) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DeleteBackgroundJson) {
                return native.DeleteBackgroundJson(id || '').then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
    },

    /* === 背景 === */
    background: {
        getBackgroundsJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetBackgroundsJson) {
                return native.GetBackgroundsJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ backgrounds: [] });
        },
        uploadBackgroundJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.UploadBackgroundJson) {
                return native.UploadBackgroundJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
        useBackgroundJson: function(id) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.UseBackgroundJson) {
                return native.UseBackgroundJson(id || '').then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
        deleteBackgroundJson: function(id) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DeleteBackgroundJson) {
                return native.DeleteBackgroundJson(id || '').then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
    },

    /* === 个人主页 === */
    profile: {
        getProfileJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetProfileJson) {
                return native.GetProfileJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({});
        },
        addOfflineAccountJson: function(name) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.AddOfflineAccountJson) {
                return native.AddOfflineAccountJson(name).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
        deleteAccountJson: function(type, name) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DeleteAccountJson) {
                return native.DeleteAccountJson(type, name).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
        logout: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.ProfileLogout) { native.ProfileLogout(); }
        },
        switchAccount: function(type) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.SwitchProfileAccount) { native.SwitchProfileAccount(type); }
        },
        setQingniaoNameJson: function(newName) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.SetQingniaoNameJson) {
                return native.SetQingniaoNameJson(newName).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
        qingniaoAuthJson: function(email, password) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.QingNiaoAuthJson) {
                return native.QingNiaoAuthJson(email, password).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: '桥接不可用' });
        },
        uploadQingniaoAvatarJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.UploadQingniaoAvatarJson) {
                return native.UploadQingniaoAvatarJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: '桥接不可用' });
        },
        uploadAccountSkinJson: function(uuid) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.UploadAccountSkinJson) {
                return native.UploadAccountSkinJson(uuid).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: '桥接不可用' });
        },
        refreshAccountHeadJson: function(uuid, name) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.RefreshAccountHeadJson) {
                return native.RefreshAccountHeadJson(uuid, name).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: '桥接不可用' });
        },
        saveSkinBase64Json: function(uuid, name, base64Data, isSlim) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.SaveSkinBase64Json) {
                return native.SaveSkinBase64Json(uuid, name, base64Data, isSlim).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: '桥接不可用' });
        },
    },

    /* === 用户协议 === */
    agreement: {
        getStatus: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetAgreementStatusJson) {
                return native.GetAgreementStatusJson().then(function(json) { return JSON.parse(json); });
            }
            // Photino fallback
            return MNL.invoke('GetAgreementStatusJson').then(function(r) {
                if (typeof r === 'string') return JSON.parse(r);
                return r || { termsAccepted: false, terracottaAccepted: false };
            }).catch(function() {
                return { termsAccepted: false, terracottaAccepted: false };
            });
        },
        acceptTerms: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.AcceptTermsOfServiceJson) {
                return native.AcceptTermsOfServiceJson().then(function(json) { return JSON.parse(json); });
            }
            return MNL.invoke('AcceptTermsOfServiceJson').then(function(r) {
                if (typeof r === 'string') return JSON.parse(r);
                return r || { success: true };
            });
        },
        acceptTerracotta: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.AcceptTerracottaAgreementJson) {
                return native.AcceptTerracottaAgreementJson().then(function(json) { return JSON.parse(json); });
            }
            return MNL.invoke('AcceptTerracottaAgreementJson').then(function(r) {
                if (typeof r === 'string') return JSON.parse(r);
                return r || { success: true };
            });
        },
        declineTerms: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DeclineTermsOfService) { native.DeclineTermsOfService(); }
        },
    },

    /* === 自定义下载 === */
    customDownload: {
        getPath: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetCustomDownloadPathJson) {
                return native.GetCustomDownloadPathJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ path: '', error: 'Bridge不可用' });
        },
        setPath: function(path) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.SetCustomDownloadPathJson) {
                return native.SetCustomDownloadPathJson(path).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        start: function(url, saveDir) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.StartCustomDownloadJson) {
                return native.StartCustomDownloadJson(url, saveDir).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        getProgress: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetCustomDownloadProgressJson) {
                return native.GetCustomDownloadProgressJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ status: 'error', errorMessage: 'Bridge不可用' });
        },
        cancel: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.CancelCustomDownloadJson) {
                return native.CancelCustomDownloadJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        openFile: function(filePath) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.CustomDlOpenFileJson) {
                return native.CustomDlOpenFileJson(filePath).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        openFolder: function(filePath) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.CustomDlOpenFolderJson) {
                return native.CustomDlOpenFolderJson(filePath).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
    },

    /* === 启动器更新 === */
    update: {
        getAutoCheck: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetAutoCheckUpdateJson) {
                return native.GetAutoCheckUpdateJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ autoCheckUpdate: true });
        },
        setAutoCheck: function(enabled) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.SetAutoCheckUpdateJson) {
                return native.SetAutoCheckUpdateJson(enabled);
            }
            return Promise.resolve({ success: true });
        },
        checkForUpdate: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.CheckForUpdateJson) {
                return native.CheckForUpdateJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: '后端不可用' });
        },
        downloadUpdate: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DownloadUpdateJson) {
                return native.DownloadUpdateJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: '后端不可用' });
        },
        getProgress: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetUpdateProgressJson) {
                return native.GetUpdateProgressJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ status: '', percent: 0 });
        },
    },

    /* === 资源包 === */
    resources: {
        shaders: (action, params) => MNL.invoke('ManageShaders', action, JSON.stringify(params || {})),
        datapacks: (action, params) => MNL.invoke('ManageDatapacks', action, JSON.stringify(params || {})),
        texturepacks: (action, params) => MNL.invoke('ManageTexturePacks', action, JSON.stringify(params || {})),
    },

    /* === 整合包 === */
    modpacks: {
        search: (query, platform) => MNL.invoke('SearchModpacks', query, platform || 'modrinth'),
        import: (filePath) => MNL.invoke('ImportModpack', filePath || '').then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
        importWithDialog: () => MNL.invoke('ImportModpackWithDialog').then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
        probeWithDialog: () => MNL.invoke('ProbeModpackWithDialog').then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
        startInstall: (filePath, customName) => MNL.invoke('StartModpackInstall', filePath, customName || '').then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
        install: (projectId, source) => MNL.invoke('InstallModpack', JSON.stringify({ projectId: projectId, source: source })).then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
    },

    /* === 社区/社交 === */
    community: {
        getUserInfoJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetCommunityUserInfo) {
                return native.GetCommunityUserInfo().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ isLoggedIn: false });
        },
        getChannelsJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetCommunityChannelsJson) {
                return native.GetCommunityChannelsJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ channels: [] });
        },
        getMessagesJson: function(channelId) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetCommunityMessagesJson) {
                return native.GetCommunityMessagesJson(channelId).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ messages: [] });
        },
        sendMessageJson: function(channelId, senderId, title, content) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.SendCommunityMessageJson) {
                return native.SendCommunityMessageJson(channelId, senderId, title, content).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
        createChannelJson: function(name, description, iconEmoji) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.CreateCommunityChannelJson) {
                return native.CreateCommunityChannelJson(name, description, iconEmoji).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false });
        },
    },

    /* === 联机工具 === */
    multiplayer: {
        getToolsStatusJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetMultiplayerToolsStatusJson) {
                return native.GetMultiplayerToolsStatusJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ mctier: { installed: false }, taowa: { installed: false } });
        },
        downloadMctierJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DownloadMctierJson) {
                return native.DownloadMctierJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        openMctierFolderJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.OpenMctierFolderJson) {
                return native.OpenMctierFolderJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        launchMctierJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.LaunchMctierJson) {
                return native.LaunchMctierJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        downloadTaowaJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DownloadTaowaJson) {
                return native.DownloadTaowaJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },

        /// MNL 服务端管理器 — 下载
        downloadServerManagerJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DownloadServerManagerJson) {
                return native.DownloadServerManagerJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        /// MNL 服务端管理器 — 打开目录
        openServerManagerFolderJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.OpenServerManagerFolderJson) {
                return native.OpenServerManagerFolderJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        /// MNL 服务端管理器 — 启动
        launchServerManagerJson: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.LaunchServerManagerJson) {
                return native.LaunchServerManagerJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },

        // ═══ RedStone 红石联机 ═══
        getServers: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetRedstoneServersJson) {
                return native.GetRedstoneServersJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        registerApikey: function(serverIp, apikey) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.RegisterRedstoneApikeyJson) {
                return native.RegisterRedstoneApikeyJson(serverIp, apikey).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        createTunnel: function(serverIp, apikey, isPublic, title, description, mods, version, online) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.CreateRedstoneTunnelJson) {
                return native.CreateRedstoneTunnelJson(serverIp, apikey, isPublic || false, title || '', description || '', mods || '', version || '', online !== false)
                    .then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        queryTunnel: function(serverIp, apikey) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.QueryRedstoneTunnelJson) {
                return native.QueryRedstoneTunnelJson(serverIp, apikey).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        deleteTunnel: function(serverIp, apikey) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.DeleteRedstoneTunnelJson) {
                return native.DeleteRedstoneTunnelJson(serverIp, apikey).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        getPublicRooms: function(offset) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetRedstonePublicRoomsJson) {
                return native.GetRedstonePublicRoomsJson(offset || 0).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        getRoomMods: function(roomId) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetRedstoneRoomModsJson) {
                return native.GetRedstoneRoomModsJson(roomId).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        startRelay: function(serverIp, apikey) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.StartRedstoneRelayJson) {
                return native.StartRedstoneRelayJson(serverIp, apikey).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        stopRelay: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.StopRedstoneRelayJson) {
                return native.StopRedstoneRelayJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ success: false, error: 'Bridge不可用' });
        },
        getRelayStatus: function() {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetRedstoneRelayStatusJson) {
                return native.GetRedstoneRelayStatusJson().then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve({ running: false, localPort: 0, controlStatus: '' });
        },
    },

    friends: {
        list: () => MNL.invoke('GetFriendList').then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
        add: (friendId) => MNL.invoke('AddFriend', friendId).then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
        remove: (friendId) => MNL.invoke('RemoveFriend', friendId).then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
    },

    /* === 加密聊天（RSA+AES-GCM 端到端加密）=== */
    chat: {
        getMyPublicKey: () => MNL.invoke('GetMyPublicKey'),
        send: (friendId, plaintext) => MNL.invoke('SendEncryptedChatMessage', friendId, plaintext).then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
        history: (friendId, sinceId) => MNL.invoke('GetChatHistory', friendId, sinceId || 0).then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
        poll: (sinceId) => MNL.invoke('PollChatMessages', sinceId || 0).then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
    },

    /* === 音乐 === */
    music: {
        search: (query) => MNL.invoke('SearchMusic', query),
        searchPlaylists: (query) => MNL.invoke('SearchMusicPlaylists', query),
        getPlaylistDetail: (id) => MNL.invoke('GetMusicPlaylistDetail', id),
        play: (trackId) => MNL.invoke('PlayMusic', trackId),
        pause: () => MNL.invoke('PauseMusic'),
        resume: () => MNL.invoke('ResumeMusic'),
        getPlaylist: () => MNL.invoke('GetMusicPlaylist'),
        // JSON 版本：绕过 COM 对象，返回纯 JS 数组（安全用于 Vue 响应式数据）
        searchJson: function(query) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.SearchMusicJson) {
                return native.SearchMusicJson(query).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve([]);
        },
        searchPlaylistsJson: function(query) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.SearchMusicPlaylistsJson) {
                return native.SearchMusicPlaylistsJson(query).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve([]);
        },
        getPlaylistDetailJson: function(id) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetMusicPlaylistDetailJson) {
                return native.GetMusicPlaylistDetailJson(id).then(function(json) { return JSON.parse(json); });
            }
            return Promise.resolve([]);
        },
        getSongUrl: function(trackId) {
            var native = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects && window.chrome.webview.hostObjects.mnl;
            if (native && native.GetSongUrlJson) {
                return native.GetSongUrlJson(trackId);
            }
            return Promise.resolve(null);
        },
    },

    /* === AI助手 === */
    ai: {
        chat: (message, model) => MNL.invoke('AIChat', message, model || ''),
        cancel: () => MNL.invoke('CancelAI'),
        getModels: () => MNL.invoke('GetAIModelsJson').then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
        getConfig: () => MNL.invoke('GetAIConfigJson').then(function(r) { return typeof r === 'string' ? JSON.parse(r) : r; }),
        saveConfig: (endpoint, apiKey, model, preset) => MNL.invoke('SaveAIConfig', endpoint || '', apiKey || '', model || '', preset || ''),
        clearHistory: () => MNL.invoke('ClearAIHistory'),
    },

    /* === 文件对话框 === */
    dialog: {
        openFile: (filters) => MNL.invoke('ShowOpenFileDialog', filters || ''),
        openFolder: () => MNL.invoke('ShowOpenFolderDialog'),
    },

    /* === 推荐/热门 === */
    recommendations: {
        getRecommendations: () => MNL.invoke('GetRecommendationsJson').then(JSON.parse),
        getDownloadOptions: (projectId, name, type, source) => MNL.invoke('GetDownloadOptionsJson', projectId, name, type, source || '').then(JSON.parse),
        downloadRecommend: (projectId, name, type, savePath, fileName, versionIndex, source) => MNL.invoke('DownloadRecommendJson', projectId, name, type, savePath || '', fileName || '', versionIndex != null ? String(versionIndex) : '', source || '').then(JSON.parse),
    },

    /* === 工具 === */
    utils: {
        openUrl: (url) => MNL.invoke('OpenUrl', url),
        getAppVersion: () => MNL.invoke('GetAppVersion'),
        getMemoryInfo: () => MNL.invoke('GetMemoryInfo'),
        optimizeMemory: () => MNL.invoke('OptimizeMemory'),
    },
};

/**
 * 监听来自 C# 的消息（事件推送）
 * - WPF/WebView2: 通过 window.postMessage
 * - Photino: 通过 window.external.receiveMessage
 */

// Photino 消息接收：同时处理 bridge 响应 和 事件推送
window.external.receiveMessage = function(msg) {
    try {
        var data = JSON.parse(msg);
        // 过滤 Photino 内部消息（remoteObjectId, callId, kind 等非桥接字段）
        if (data.remoteObjectId !== undefined || data.callId !== undefined || data.kind) return;
        // 事件推送（有 type 字段）
        if (data.type && data.payload !== undefined) {
            dispatchMNL(data.type, data.payload);
            return;
        }
        // Bridge 响应（有 id 字段）
        if (data.id !== undefined && _mnlPending[data.id]) {
            var p = _mnlPending[data.id];
            clearTimeout(p.timer);
            delete _mnlPending[data.id];
            if (data.error) { var errMsg = typeof data.error === 'string' ? data.error : (data.error.message || JSON.stringify(data.error)); p.reject(new Error(errMsg)); }
            else { p.resolve(data.result); }
        }
    } catch (err) {
        console.error('[MNL] receiveMessage parse error:', err);
    }
};

window.addEventListener('message', (e) => {
    if (!e.data || typeof e.data !== 'object') return;
    
    const { type, payload } = e.data;
    dispatchMNL(type, payload);
});

/** 统一的 MNL 事件分发 */
function dispatchMNL(type, payload) {
    if (!type) return;
    switch (type) {
        case 'download-progress':
            // 触发全局下载进度事件
            window.dispatchEvent(new CustomEvent('mnl:download-progress', { detail: payload }));
            break;
        case 'launch-status':
            window.dispatchEvent(new CustomEvent('mnl:launch-status', { detail: payload }));
            break;
        case 'auth-callback':
            window.dispatchEvent(new CustomEvent('mnl:auth-callback', { detail: payload }));
            break;
        case 'update-available':
            window.dispatchEvent(new CustomEvent('mnl:update-available', { detail: payload }));
            break;
        case 'navigation':
            window.dispatchEvent(new CustomEvent('mnl:navigate', { detail: payload }));
            break;
        case 'toast':
            window.dispatchEvent(new CustomEvent('mnl:toast', { detail: payload }));
            break;
        case 'ms-device-code':
            window.dispatchEvent(new CustomEvent('mnl:ms-device-code', { detail: payload }));
            break;
        case 'ms-login-success':
            window.dispatchEvent(new CustomEvent('mnl:ms-login-success', { detail: payload }));
            break;
        case 'ms-login-error':
            window.dispatchEvent(new CustomEvent('mnl:ms-login-error', { detail: payload }));
            break;
        case 'user-update':
            window.dispatchEvent(new CustomEvent('mnl:user-update', { detail: payload }));
            break;
        case 'background-changed':
            // 使用统一的 wallpaper-bg 层
            if (payload && payload.fileName) {
                window.applyWallpaper(payload.fileName);
            }
            break;
        case 'modpack-install-progress':
            window.dispatchEvent(new CustomEvent('mnl:modpack-progress', { detail: payload }));
            break;
        case 'modpack-install-complete':
            window.dispatchEvent(new CustomEvent('mnl:modpack-complete', { detail: payload }));
            break;
        case 'version-download-progress':
            window.dispatchEvent(new CustomEvent('mnl:version-download-progress', { detail: payload }));
            // 同时更新下载任务页
            window.dispatchEvent(new CustomEvent('mnl:download-task-update', { detail: payload }));
            break;
        case 'version-download-complete':
            window.dispatchEvent(new CustomEvent('mnl:version-download-complete', { detail: payload }));
            break;
        case 'mnl:versions-changed':
            window.dispatchEvent(new CustomEvent('mnl:versions-changed', { detail: payload }));
            break;
        case 'ai-tool':
            window.dispatchEvent(new CustomEvent('mnl:ai-tool', { detail: payload }));
            break;
    }
}

// 导出到全局
window.MNL = MNL;

// === 全局壁纸工具函数 ===
window.applyWallpaper = function(fileName, blur) {
    var bgEl = document.getElementById('wallpaper-bg');
    if (!bgEl) { console.error('[Wallpaper] 找不到 #wallpaper-bg'); return; }
    if (fileName) {
        var url = window.location.origin + '/api/bg/' + fileName;
        console.log('[Wallpaper] 设置背景图:', url, 'blur:', blur);
        bgEl.style.backgroundImage = 'url(' + url + ')';
        bgEl.style.display = '';
        if (typeof blur === 'number' && blur > 0) {
            bgEl.style.filter = 'blur(' + blur + 'px)';
        }
        // 清除视频背景，激活半透明内容层
        var oldVideo = document.getElementById('video-bg');
        if (oldVideo) oldVideo.remove();
        document.body.classList.add('bg-active');
    } else {
        bgEl.style.backgroundImage = '';
        bgEl.style.display = 'none';
        bgEl.style.filter = '';
        document.body.classList.remove('bg-active');
    }
};

/**
 * 实时更新界面透明度（让壁纸/视频透出）
 * @param {number} opacity - 透明度百分比 (0-100)，100=完全不透明，0=完全透明
 */
window.updateBgOpacity = function(opacity) {
    // 滑块值: 0=完全不透明(无视频), 100=完全透明(视频全显示)
    // CSS --bg-opacity 是 UI 容器背景的 alpha，需要取反
    var val = (100 - opacity) / 100;
    console.log('[Wallpaper] updateBgOpacity:', opacity + '% -> --bg-opacity:', val.toFixed(2));
    document.body.style.setProperty('--bg-opacity', val);
    // 全景模式下直接控制 canvas 透明度
    var glCanvas = document.getElementById('wallpaper-canvas-gl');
    if (glCanvas && glCanvas.style.display !== 'none') {
        glCanvas.style.opacity = opacity / 100;
    }
    // 可视化模式下控制可视化 canvas 透明度
    var vizCanvas = document.getElementById('visualizer-canvas');
    if (vizCanvas && vizCanvas.style.display !== 'none') {
        vizCanvas.style.opacity = opacity / 100;
    }
    // Canvas 视频渲染器透明度
    if (window._videoBgRenderer) {
        window._videoBgRenderer.setOpacity(opacity / 100);
    }
}

/**
 * 实时更新背景层模糊
 * @param {number} blur - 模糊像素值
 */
window.updateBgBlur = function(blur) {
    var imgEl = document.getElementById('wallpaper-bg');
    if (imgEl && imgEl.style.display !== 'none') {
        imgEl.style.filter = blur > 0 ? 'blur(' + blur + 'px)' : '';
    }
    // Canvas 视频渲染器模糊
    if (window._videoBgRenderer) {
        window._videoBgRenderer.setBlur(blur);
    }
};

/**
 * 实时更新背景覆盖层（透明 UI 下的文字可读性辅助层）
 * @param {number} overlay - 覆盖层强度 (0-100)，0=无覆盖，100=最大覆盖(0.6 alpha)
 */
window.updateBgOverlay = function(overlay) {
    // 映射 0-100 → 0-0.6 (alpha)
    var val = overlay / 100 * 0.6;
    document.body.style.setProperty('--bg-overlay', val);
    console.log('[Wallpaper] updateBgOverlay:', overlay + '% -> --bg-overlay:', val.toFixed(3));
};

/* ============================================================
   聚光灯效果 - 元素级鼠标追踪（每个卡片/按钮内部跟随）
   ============================================================ */
(function initSpotlight() {
    // 聚光灯追踪的选择器
    var selector = '.btn, .btn-ghost, .btn-primary, .btn-success, .btn-launch, ' +
                   '.dl-card, .card, .vcard, .vp-latest-card, ' +
                   '.mm-loader-card, .mm-bottom-card, .mm-mod-item, .dl-cat-item, ' +
                   '.quick-card, .sp-cat-btn, .vp-tab, .nav-item, ' +
                   '.dl-cat-banner, .dl-custom, .launch-panel, .settings-panel';

    // 事件委托：鼠标移入/移动/离开
    var appContainer = document.getElementById('app-container');
    if (!appContainer) return;

    // 统一的位置计算
    function updateSpotlight(e, el) {
        var rect = el.getBoundingClientRect();
        var x = ((e.clientX - rect.left) / rect.width) * 100;
        var y = ((e.clientY - rect.top) / rect.height) * 100;
        el.style.setProperty('--spot-x', x + '%');
        el.style.setProperty('--spot-y', y + '%');
    }

    appContainer.addEventListener('mouseover', function(e) {
        var el = e.target.closest(selector);
        if (!el) return;
        updateSpotlight(e, el);
    }, true);

    appContainer.addEventListener('mousemove', function(e) {
        var el = e.target.closest(selector);
        if (!el) return;
        updateSpotlight(e, el);
    });

    // 鼠标离开时重置到居中
    appContainer.addEventListener('mouseout', function(e) {
        var el = e.target.closest(selector);
        if (!el) return;
        el.style.setProperty('--spot-x', '50%');
        el.style.setProperty('--spot-y', '50%');
    }, true);
})();

/**
 * 应用视频背景
 * @param {string|null} fileName - 视频文件名，null 则清除
 * @param {number} [blur=0] - 模糊像素值
 */
window.applyVideoBackground = function(fileName, blur) {
    var oldVideo = document.getElementById('video-bg');
    if (oldVideo) { oldVideo.pause(); oldVideo.remove(); }

    if (fileName) {
        var video = document.createElement('video');
        video.id = 'video-bg';
        video.src = 'https://mnl-bg.local/' + fileName;
        video.autoplay = true;
        video.loop = true;
        video.muted = true;
        video.playsInline = true;
        if (blur && blur > 0) video.style.filter = 'blur(' + blur + 'px)';
        video.style.cssText += ';position:fixed;top:0;left:0;width:100%;height:100%;z-index:0;object-fit:cover;';
        document.body.insertBefore(video, document.body.firstChild);
        document.body.classList.add('bg-active');
    } else {
        document.body.classList.remove('bg-active');
    }
};

window._videoBgRenderer = {
    start: function() {},
    stop: function() {
        var video = document.getElementById('video-bg');
        if (video) { video.pause(); video.remove(); }
    },
    setOpacity: function(val) {
        var video = document.getElementById('video-bg');
        if (video) video.style.opacity = val;
    },
    setBlur: function(val) {
        var video = document.getElementById('video-bg');
        if (video) video.style.filter = val > 0 ? 'blur(' + val + 'px)' : '';
    }
};
