// ====== 模组管理 ModManagerPage (mm-) ======
// 对齐 UnoApp modmanager.html：加载器安装 + 模组管理
const ModManagerPage = {
  template: `
    <div class="page-content modmanager-page" style="display:flex;flex-direction:column;height:100%;overflow:hidden;">

      <!-- ═══ Header ═══ -->
      <div class="mm-header-row" style="display:flex;align-items:center;justify-content:space-between;padding:12px 20px;flex-shrink:0;background:var(--card-hover,#2a2a3a);flex-wrap:wrap;gap:10px;">
        <div style="display:flex;align-items:center;gap:14px;flex-wrap:wrap;">
          <div style="width:4px;height:26px;background:#2196F3;border-radius:2px;flex-shrink:0;"></div>
          <div>
            <span style="font-size:20px;font-weight:700;color:var(--text-primary,#333);">模组管理</span>
            <div style="font-size:12px;color:var(--text-secondary,#999);" v-if="hasLoader">{{ loaderName }}</div>
            <div style="font-size:12px;color:var(--text-secondary,#999);" v-else>未检测到模组加载器</div>
          </div>
          <!-- 内容类型标签 -->
          <div style="display:flex;background:#2E2E3E;border-radius:6px;padding:3px;margin-left:20px;">
            <button class="mm-tab-btn" :class="{ active: contentTab === 'mod' }" @click="contentTab = 'mod'">模组</button>
            <button class="mm-tab-btn" :class="{ active: contentTab === 'shader' }" @click="contentTab = 'shader'">光影</button>
            <button class="mm-tab-btn" :class="{ active: contentTab === 'resource' }" @click="contentTab = 'resource'">材质</button>
          </div>
          <span style="font-size:12px;color:var(--text-secondary,#999);margin-left:20px;">版本:</span>
          <div class="mnl-select" :class="{ open: versionOpen }" v-click-outside="() => versionOpen = false">
            <div class="mnl-select-trigger" @click="versionOpen = !versionOpen">
              <span class="mnl-select-text">{{ versionDisplayText }}</span>
              <span class="mnl-select-arrow">▾</span>
            </div>
            <div class="mnl-select-menu" v-if="versionOpen">
              <div class="mnl-select-item" v-for="v in versionOptions" :key="v.v"
                :class="{ active: selectedVersion === v.v }"
                @click="selectedVersion = v.v; versionOpen = false; onVersionChange()">
                {{ v.t }}
              </div>
            </div>
          </div>
        </div>
        <div style="display:flex;align-items:center;gap:10px;">
          <button class="mm-btn mm-btn-refresh" @click="refreshMods">刷新</button>
          <button class="mm-btn mm-btn-danger" @click="deleteCurrentVersion" v-if="selectedVersion">删除此版本</button>
          <button class="mm-btn mm-btn-primary" @click="installLoader">安装 / 切换加载器</button>
        </div>
      </div>

      <!-- ═══ 无加载器界面 ═══ -->
      <div v-if="!hasLoader" class="mm-no-loader-scroll" style="flex:1;overflow-y:auto;padding:24px 30px;">
        <div style="max-width:820px;margin:0 auto;">
          <div class="mm-warn-card">
            <div class="mm-warn-icon">!</div>
            <div class="mm-warn-title">还未安装任何模组加载器</div>
            <div class="mm-warn-hint">请先在"下载-游戏版本"页面选择你希望安装加载器的 Minecraft 版本。</div>
          </div>

          <div class="mm-loader-select-title">选择要安装的模组加载器</div>

          <div class="mm-loader-grid">
            <div class="mm-loader-card" :class="{ installing: installingLoader === 'Forge' }" @click="selectLoader('Forge')">
              <img class="mm-loader-icon-img" src="img/Anvil.png" alt="Forge">
              <div class="mm-loader-info">
                <div class="mm-loader-name">{{ installingLoader === 'Forge' ? '安装中...' : 'Forge' }}</div>
                <div class="mm-loader-desc" style="color:#87CEFA">最流行的 Mod 加载器，兼容性最好</div>
              </div>
            </div>
            <div class="mm-loader-card" :class="{ installing: installingLoader === 'Fabric' }" @click="selectLoader('Fabric')">
              <img class="mm-loader-icon-img" src="img/Fabric.png" alt="Fabric">
              <div class="mm-loader-info">
                <div class="mm-loader-name">{{ installingLoader === 'Fabric' ? '安装中...' : 'Fabric' }}</div>
                <div class="mm-loader-desc" style="color:#C8E6C9">轻量级加载器，启动更快，适合现代 Mod</div>
              </div>
            </div>
            <div class="mm-loader-card" :class="{ installing: installingLoader === 'NeoForge' }" @click="selectLoader('NeoForge')">
              <img class="mm-loader-icon-img" src="img/NeoForge.png" alt="NeoForge">
              <div class="mm-loader-info">
                <div class="mm-loader-name">{{ installingLoader === 'NeoForge' ? '安装中...' : 'NeoForge' }}</div>
                <div class="mm-loader-desc" style="color:#D2B4DE">Forge 的现代分支，支持最新游戏版本</div>
              </div>
            </div>
            <div class="mm-loader-card" :class="{ installing: installingLoader === 'OptiFine' }" @click="selectLoader('OptiFine')">
              <img class="mm-loader-icon-img" src="img/GrassPath.png" alt="OptiFine">
              <div class="mm-loader-info">
                <div class="mm-loader-name">{{ installingLoader === 'OptiFine' ? '安装中...' : 'OptiFine' }}</div>
                <div class="mm-loader-desc" style="color:#F5D7A1">性能优化与光影支持，提升帧率与画质</div>
              </div>
            </div>
          </div>

          <div class="mm-loader-hint">提示：请先在"游戏版本"页面选择你希望安装加载器的 Minecraft 版本。</div>
        </div>
      </div>

      <!-- ═══ 有加载器界面 ═══ -->
      <div v-if="hasLoader" style="flex:1;display:flex;flex-direction:column;gap:16px;padding:18px 24px;overflow:hidden;">
        <!-- Mod 列表卡片 -->
        <div style="flex:1;background:var(--card-hover,#2a2a3a);border:1px solid var(--border,#4a4a4a);border-radius:10px;display:flex;flex-direction:column;overflow:hidden;min-height:0;">
          <div style="display:flex;align-items:center;justify-content:space-between;padding:14px 18px;border-bottom:1px solid var(--border,#4a4a4a);flex-shrink:0;flex-wrap:wrap;gap:10px;">
            <div style="display:flex;align-items:center;gap:10px;">
              <span style="font-size:16px;font-weight:600;color:var(--text-primary,#333);">已安装模组</span>
              <span style="padding:2px 10px;background:var(--bg-secondary,#3a3a4a);border-radius:10px;font-size:12px;color:var(--text-secondary,#999);">共 {{ mods.length }} 个</span>
            </div>
            <div style="display:flex;gap:10px;">
              <button class="mm-action-btn mm-action-enable" @click="enableSelected">启用选中</button>
              <button class="mm-action-btn mm-action-disable" @click="disableSelected">禁用选中</button>
              <button class="mm-action-btn mm-action-uninstall" @click="uninstallSelected">卸载选中</button>
            </div>
          </div>
          <div style="flex:1;overflow-y:auto;padding:12px;display:flex;flex-direction:column;gap:4px;">
            <div v-if="mods.length === 0" style="display:flex;flex-direction:column;align-items:center;justify-content:center;padding:30px;gap:10px;color:var(--text-muted,#999);">
              <span style="font-size:48px;opacity:0.5;">📦</span>
              <span style="font-size:16px;font-weight:600;color:var(--text-secondary,#999);">暂无模组</span>
              <span style="font-size:12px;">点击下方"模组下载"浏览社区热门 Mod</span>
            </div>
            <div v-for="(m, idx) in mods" :key="m.id" class="mm-mod-item" :data-idx="idx" style="cursor:pointer;user-select:none;width:100%;box-sizing:border-box;">
              <img v-if="m.icon" class="mm-mod-item-img" :src="m.icon" :alt="m.name" />
              <div v-else class="mm-mod-item-icon">⚙</div>
              <div class="mm-mod-item-dot" :class="m.enabled ? 'enabled' : 'disabled'"></div>
              <div class="mm-mod-item-info">
                <div class="mm-mod-item-name">{{ m.name }}</div>
                <div class="mm-mod-item-version">{{ m.version }}</div>
              </div>
              <div class="mm-mod-item-badge" :class="m.enabled ? 'enabled' : 'disabled'">{{ m.enabled ? '已启用' : '已禁用' }}</div>
            </div>
          </div>
        </div>

        <!-- 底部操作卡片 -->
        <div style="display:grid;grid-template-columns:1fr 1fr 1fr;gap:8px;flex-shrink:0;">
          <div class="mm-bottom-card" @click="refreshMods">
            <div class="mm-bc-icon">🔄</div>
            <div class="mm-bc-text">
              <div class="mm-bc-title">刷新模组</div>
              <div class="mm-bc-desc">重新扫描 mods 目录</div>
            </div>
          </div>
          <div class="mm-bottom-card" @click="addMod">
            <div class="mm-bc-icon">+</div>
            <div class="mm-bc-text">
              <div class="mm-bc-title">添加模组</div>
              <div class="mm-bc-desc">选择 .jar 文件添加</div>
            </div>
          </div>
          <div class="mm-bottom-card" @click="goToModStore">
            <div class="mm-bc-icon">📦</div>
            <div class="mm-bc-text">
              <div class="mm-bc-title">模组下载</div>
              <div class="mm-bc-desc">浏览社区热门 Mod</div>
            </div>
          </div>
        </div>
      </div>

      <!-- 删除确认弹窗 -->
      <div class="modal-overlay" v-if="confirmShow" @click.self="confirmShow = false">
        <div class="cm-dialog" style="background:var(--card-bg);border:1px solid var(--border);border-radius:12px;padding:24px;min-width:360px;max-width:440px;">
          <div class="cm-dialog-title" style="font-size:16px;font-weight:700;color:var(--text-primary);margin-bottom:10px;">⚠️ {{ confirmTitle }}</div>
          <div class="pf-dialog-msg" style="font-size:14px;color:var(--text-secondary);margin-bottom:20px;line-height:1.5;">{{ confirmMsg }}</div>
          <div class="pf-dialog-btns" style="display:flex;gap:10px;justify-content:flex-end;">
            <button class="btn btn-ghost" @click="confirmShow = false" style="padding:8px 18px;border:1px solid var(--border);border-radius:6px;background:transparent;color:var(--text-primary);cursor:pointer;">取消</button>
            <button class="btn btn-primary" @click="doConfirmAction" style="padding:8px 18px;border:none;border-radius:6px;background:#E74C3C;color:#fff;cursor:pointer;">确认删除</button>
          </div>
        </div>
      </div>

    </div>
  `,
  data() {
    return {
      hasLoader: false,
      loaderName: '',
      loaderVersion: '',
      contentTab: 'mod',
      selectedVersion: '',
      versionOpen: false,
      availableVersions: [],
      mods: [],
      loading: false,
      confirmShow: false,
      confirmTitle: '',
      confirmMsg: '',
      confirmAction: null,
    };
  },
  computed: {
    versionDisplayText: function() {
      if (this.selectedVersion) return this.selectedVersion;
      return '请选择版本';
    },
    versionOptions: function() {
      var opts = [{ v: '', t: '请选择版本' }];
      for (var i = 0; i < this.availableVersions.length; i++) {
        var v = this.availableVersions[i];
        opts.push({ v: v, t: v });
      }
      return opts;
    }
  },
  methods: {
    selectLoader(loader) {
      var self = this;
      if (!self.selectedVersion) {
        window.showToast && window.showToast('请先选择 Minecraft 版本', 'warning');
        return;
      }
      if (window.MNL && window.MNL.modmanager && window.MNL.modmanager.installLoader) {
        window.showToast && window.showToast('正在安装 ' + loader + ' 加载器...', 'info');
        this.installingLoader = loader;
        window.MNL.modmanager.installLoader(self.selectedVersion, loader).then(function() {
          self.installingLoader = '';
          window.showToast && window.showToast(loader + ' 安装完成！', 'success');
          self.detectLoaders();
          self.scanMods();
        })["catch"](function(e) {
          self.installingLoader = '';
          window.showToast && window.showToast(loader + ' 安装失败: ' + (e && e.message || '未知错误'), 'error');
        });
      } else {
        window.showToast && window.showToast('加载器安装功能不可用', 'error');
      }
    },
    installLoader() {
      window.navigateTo && window.navigateTo('download');
    },
    onVersionChange() {
      this.detectLoaders();
      this.scanMods();
    },
    detectLoaders() {
      var self = this;
      if (window.MNL && window.MNL.modmanager && window.MNL.modmanager.detectLoadersJson) {
        window.MNL.modmanager.detectLoadersJson(self.selectedVersion).then(function(data) {
          if (data && data.hasLoader && data.loaders && data.loaders.length > 0) {
            self.hasLoader = true;
            self.loaderName = data.loaders[0].displayName || data.loaders[0].loaderName;
            self.loaderVersion = data.loaders[0].versionId || '';
          } else {
            self.hasLoader = false;
          }
        })["catch"](function() { self.hasLoader = false; });
      }
    },
    scanMods() {
      var self = this;
      this.loading = true;
      if (window.MNL && window.MNL.modmanager) {
        window.MNL.modmanager.getInstalledModsJson(self.selectedVersion, self.contentTab).then(function(data) {
          self.mods = (data && data.mods) ? data.mods.map(function(m) { m.selected = false; return m; }) : [];
          self.loading = false;
        })["catch"](function() { self.mods = []; self.loading = false; });
      } else {
        this.mods = []; this.loading = false;
      }
    },
    toggleMod: function(idx) {
      if (idx >= 0 && idx < this.mods.length) {
        this.mods[idx].selected = !this.mods[idx].selected;
        var el = this.$el.querySelector('.mm-mod-item[data-idx="' + idx + '"]');
        if (el) {
          if (this.mods[idx].selected) {
            el.classList.add('selected');
            el.style.setProperty('border-color', '#2196F3', 'important');
            el.style.setProperty('background-color', 'rgba(33,150,243,0.12)', 'important');
            el.style.setProperty('box-shadow', 'inset 0 0 0 1px rgba(33,150,243,0.3)', 'important');
          } else {
            el.classList.remove('selected');
            el.style.removeProperty('border-color');
            el.style.removeProperty('background-color');
            el.style.removeProperty('box-shadow');
          }
        }
      }
    },
    enableSelected() {
      var selected = this.mods.filter(function(m) { return m.selected && !m.enabled; });
      if (selected.length === 0) { window.showToast && window.showToast('请先选择未启用的模组', 'warning'); return; }
      var self = this;
      var count = 0;
      var promises = selected.map(function(m) {
        if (window.MNL && window.MNL.modmanager && window.MNL.modmanager.toggleModJson) {
          return window.MNL.modmanager.toggleModJson(m.fullPath, true).then(function(r) {
            if (r && r.success) { m.enabled = true; m.selected = false; count++; }
          });
        }
        return Promise.resolve();
      });
      Promise.all(promises).then(function() {
        window.showToast && window.showToast('已启用 ' + count + ' 个模组', 'success');
        self.scanMods();
      });
    },
    disableSelected() {
      var selected = this.mods.filter(function(m) { return m.selected && m.enabled; });
      if (selected.length === 0) { window.showToast && window.showToast('请先选择已启用的模组', 'warning'); return; }
      var self = this;
      var count = 0;
      var promises = selected.map(function(m) {
        if (window.MNL && window.MNL.modmanager && window.MNL.modmanager.toggleModJson) {
          return window.MNL.modmanager.toggleModJson(m.fullPath, false).then(function(r) {
            if (r && r.success) { m.enabled = false; m.selected = false; count++; }
          });
        }
        return Promise.resolve();
      });
      Promise.all(promises).then(function() {
        window.showToast && window.showToast('已禁用 ' + count + ' 个模组', 'warning');
        self.scanMods();
      });
    },
    uninstallSelected() {
      var selected = this.mods.filter(function(m) { return m.selected; });
      if (selected.length === 0) { window.showToast && window.showToast('请先选择模组', 'warning'); return; }
      var self = this;
      this.confirmTitle = '卸载模组';
      this.confirmMsg = '确定要卸载选中的 ' + selected.length + ' 个模组吗？此操作不可恢复。';
      this.confirmAction = function() {
        var count = 0;
        var promises = selected.map(function(m) {
          if (window.MNL && window.MNL.modmanager && window.MNL.modmanager.deleteModJson) {
            return window.MNL.modmanager.deleteModJson(m.fullPath).then(function(r) {
              if (r && r.success) count++;
            });
          }
          return Promise.resolve();
        });
        Promise.all(promises).then(function() {
          window.showToast && window.showToast('已卸载 ' + count + ' 个模组', 'warning');
          self.scanMods();
        });
      };
      this.confirmShow = true;
    },
    doConfirmAction() {
      if (this.confirmAction) this.confirmAction();
      this.confirmShow = false;
    },
    refreshMods() {
      this.scanMods();
      window.showToast && window.showToast('模组列表已刷新', 'success');
    },
    addMod() {
      window.showToast && window.showToast('请将 .jar 文件放入 mods 文件夹后点击刷新', 'info');
    },
    goToModStore() { window.navigateTo && window.navigateTo('download'); },
    deleteCurrentVersion() {
      var self = this;
      if (!self.selectedVersion) return;
      if (!confirm('确定要删除版本 ' + self.selectedVersion + ' 吗？\n此操作不可撤销，将删除该版本的所有文件。')) return;
      if (window.MNL && window.MNL.versions && window.MNL.versions.delete) {
        window.MNL.versions.delete(self.selectedVersion).then(function(r) {
          if (r) {
            window.showToast && window.showToast(self.selectedVersion + ' 已删除', 'success');
            self.selectedVersion = '';
            self.hasLoader = false;
            self.loaderName = '';
            self.loaderVersion = '';
            self.mods = [];
            self.availableVersions = self.availableVersions.filter(function(v) { return v !== self.selectedVersion; });
            window.dispatchEvent(new CustomEvent('mnl:versions-changed'));
          } else {
            window.showToast && window.showToast('删除失败', 'error');
          }
        })["catch"](function(e) {
          window.showToast && window.showToast('删除失败: ' + (e && e.message || '未知错误'), 'error');
        });
      }
    }
  },
  watch: {
    contentTab: function() { this.scanMods(); }
  },
  updated() {
    var self = this;
    this.$nextTick(function() {
      var items = self.$el.querySelectorAll('.mm-mod-item');
      for (var i = 0; i < items.length; i++) {
        var item = items[i];
        if (item.__mmBound) continue;
        item.__mmBound = true;
        item.addEventListener('click', function(e) {
          var el = e.currentTarget;
          var idx = parseInt(el.getAttribute('data-idx'));
          if (!isNaN(idx) && idx >= 0 && idx < self.mods.length) {
            var selected = !self.mods[idx].selected;
            self.mods[idx].selected = selected;
            // 内联 style 确保视觉更新（最高优先级，覆盖所有 CSS）
            if (selected) {
              el.classList.add('selected');
              el.style.setProperty('border-color', '#2196F3', 'important');
              el.style.setProperty('background-color', 'rgba(33,150,243,0.12)', 'important');
              el.style.setProperty('box-shadow', 'inset 0 0 0 1px rgba(33,150,243,0.3)', 'important');
            } else {
              el.classList.remove('selected');
              el.style.removeProperty('border-color');
              el.style.removeProperty('background-color');
              el.style.removeProperty('box-shadow');
            }
          }
        });
      }
    });
  },
  mounted() {
    var self = this;
    // 先加载版本列表，完成后再检测加载器 + 扫描模组
    if (window.MNL && window.MNL.versions && window.MNL.versions.getInstalledJson) {
      window.MNL.versions.getInstalledJson().then(function(list) {
        self.availableVersions = (list || []).map(function(v) { return v.id || v; });
        if (self.availableVersions.length > 0) {
          self.selectedVersion = self.availableVersions[0];
        }
        // 版本加载完成后才执行后续操作
        self.detectLoaders();
        self.scanMods();
      })["catch"](function() {
        // 即使版本加载失败也尝试扫描
        self.scanMods();
      });
    } else {
      // 无版本 API 时延迟尝试
      setTimeout(function() { self.scanMods(); }, 500);
    }
  }
};
