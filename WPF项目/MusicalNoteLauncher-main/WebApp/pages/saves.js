// ====== 存档管理 SavesPage (sv-) ======
const SavesPage = {
  template: `
    <div class="sv-page">
      <div class="sv-header">
        <span class="sv-title">存档管理</span>
      </div>
      <div class="sv-body">
        <!-- 操作按钮栏 -->
        <div class="sv-toolbar">
          <span class="sv-toolbar-title">游戏存档 <span class="sv-count">({{ saves.length }})</span></span>
          <div class="sv-toolbar-btns">
            <button class="sv-btn sv-btn-primary" @click="launchSave" :disabled="!selectedSave">启动存档</button>
            <button class="sv-btn sv-btn-success" @click="backupSave" :disabled="!selectedSave">备份存档</button>
            <button class="sv-btn sv-btn-default" @click="openRename" :disabled="!selectedSave">重命名</button>
            <button class="sv-btn sv-btn-danger" @click="openDelete" :disabled="!selectedSave">删除存档</button>
          </div>
        </div>

        <!-- 存档列表 -->
        <div class="sv-list">
          <div v-if="loading" class="sv-loading">
            <div class="sv-loading-spinner"></div>
            <span>正在加载存档...</span>
          </div>

          <div v-if="!loading && saves.length > 0">
            <div class="sv-item" v-for="s in saves" :key="s.path"
              :class="{ selected: selectedSave === s.path }" @click="selectedSave = s.path">
              <span class="sv-item-icon">📁</span>
              <div class="sv-item-info">
                <span class="sv-item-name">{{ s.name }}</span>
                <span class="sv-item-size">{{ s.sizeText }}</span>
              </div>
              <span class="sv-item-time">{{ s.playTime }}</span>
            </div>
          </div>

          <div v-if="!loading && saves.length === 0" class="sv-empty">
            <span class="sv-empty-icon">📂</span>
            <span>暂无存档文件</span>
          </div>
        </div>
      </div>

      <!-- 底部操作卡片 -->
      <div class="sv-bottom">
        <div class="sv-bottom-card" @click="refreshSaves">
          <span class="sv-bottom-icon">📁</span>
          <span class="sv-bottom-label">游戏存档</span>
        </div>
        <div class="sv-bottom-card" @click="backupAllSaves">
          <span class="sv-bottom-icon">💾</span>
          <span class="sv-bottom-label">备份管理</span>
        </div>
        <div class="sv-bottom-card" @click="importSave">
          <span class="sv-bottom-icon">📥</span>
          <span class="sv-bottom-label">导入存档</span>
        </div>
      </div>

      <!-- ═══ 重命名弹窗 ═══ -->
      <div class="modal-overlay" v-if="renameShow" @click.self="renameShow = false">
        <div class="terms-modal" style="max-width:420px;">
          <div class="terms-title">✏️ 重命名存档</div>
          <div class="terms-desc" style="margin-bottom:12px;">当前名称: {{ renameTarget ? renameTarget.name : '' }}</div>
          <input type="text" v-model="renameInput" placeholder="请输入新名称" @keyup.enter="renameConfirm"
            style="width:100%;padding:10px 12px;border:1px solid var(--border);border-radius:6px;background:var(--input-bg);color:var(--text-primary);font-size:14px;outline:none;box-sizing:border-box;" />
          <div v-if="renameError" style="margin-top:8px;font-size:12px;color:#E53935;">{{ renameError }}</div>
          <div style="display:flex;gap:10px;margin-top:16px;justify-content:flex-end;">
            <button class="terms-btn terms-btn-decline" @click="renameShow = false">取消</button>
            <button class="terms-btn terms-btn-accept" @click="renameConfirm" :disabled="!renameInput.trim()">确认</button>
          </div>
        </div>
      </div>

      <!-- ═══ 删除确认弹窗（二次点击） ═══ -->
      <div class="modal-overlay" v-if="deleteShow" @click.self="deleteShow = false">
        <div class="terms-modal" style="max-width:420px;">
          <div class="terms-title">🗑️ 删除存档</div>
          <div class="terms-desc" style="margin-bottom:12px;">
            确定删除 <strong>{{ deleteTarget ? deleteTarget.name : '' }}</strong> 吗？此操作不可撤销！
          </div>
          <div style="display:flex;gap:10px;justify-content:flex-end;">
            <button class="terms-btn terms-btn-decline" @click="deleteShow = false">取消</button>
            <button class="terms-btn terms-btn-accept" @click="deleteConfirm" style="background:#E53935;">确定删除</button>
          </div>
        </div>
      </div>
    </div>
  `,
  data: function() {
    return {
      saves: [],
      selectedSave: null,
      loading: false,
      renameShow: false,
      renameTarget: null,
      renameInput: '',
      renameError: '',
      deleteShow: false,
      deleteTarget: null
    };
  },
  methods: {
    toast: function(msg, type) { window.showToast && window.showToast(msg, type); },
    refreshSaves: function() {
      var self = this;
      self.loading = true;
      if (window.MNL && window.MNL.saves && window.MNL.saves.getWorldsJson) {
        window.MNL.saves.getWorldsJson().then(function(data) {
          self.saves = (data && data.saves) ? data.saves : [];
          self.loading = false;
        })["catch"](function() {
          self.saves = [];
          self.loading = false;
        });
      } else {
        self.loading = false;
      }
    },
    launchSave: function() {
      if (!this.selectedSave) return;
      var save = this.saves.find(function(s) { return s.path === this.selectedSave; }, this);
      if (!save) return;
      var self = this;
      if (window.MNL && window.MNL.saves && window.MNL.saves.launchJson) {
        window.MNL.saves.launchJson(save.path).then(function(r) {
          if (r && r.success) {
            self.toast('正在启动存档: ' + save.name, 'success');
          } else {
            self.toast('启动失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        })["catch"](function(e) {
          self.toast('启动失败: ' + (e.message || '未知错误'), 'error');
        });
      } else {
        this.toast('启动存档功能暂不可用', 'info');
      }
    },
    backupSave: function() {
      if (!this.selectedSave) return;
      var self = this;
      if (window.MNL && window.MNL.saves && window.MNL.saves.backupJson) {
        window.MNL.saves.backupJson(self.selectedSave).then(function(r) {
          if (r && r.success) {
            self.toast('备份成功: ' + r.name + ' (' + r.size + ')', 'success');
          } else {
            self.toast('备份失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        })["catch"](function(e) {
          self.toast('备份失败: ' + (e.message || '未知错误'), 'error');
        });
      }
    },
    openRename: function() {
      if (!this.selectedSave) return;
      var save = this.saves.find(function(s) { return s.path === this.selectedSave; }, this);
      if (!save) return;
      this.renameTarget = save;
      this.renameInput = save.name;
      this.renameError = '';
      this.renameShow = true;
    },
    renameConfirm: function() {
      var newName = (this.renameInput || '').trim();
      if (!newName) return;
      var save = this.renameTarget;
      if (!save || newName === save.name) { this.renameShow = false; return; }
      // 校验无效字符
      if (/[<>:"/\\|?*]/.test(newName)) {
        this.renameError = '名称包含无效字符：< > : " / \\ | ? *';
        return;
      }
      var self = this;
      if (window.MNL && window.MNL.saves && window.MNL.saves.renameJson) {
        window.MNL.saves.renameJson(self.selectedSave, newName).then(function(r) {
          if (r && r.success) {
            save.name = newName;
            save.path = r.newPath;
            self.selectedSave = r.newPath;
            self.renameShow = false;
            self.toast('重命名成功', 'success');
          } else {
            self.renameError = r && r.error || '重命名失败';
          }
        })["catch"](function(e) {
          self.renameError = e.message || '重命名失败';
        });
      }
    },
    openDelete: function() {
      if (!this.selectedSave) return;
      var save = this.saves.find(function(s) { return s.path === this.selectedSave; }, this);
      if (!save) return;
      this.deleteTarget = save;
      this.deleteShow = true;
    },
    deleteConfirm: function() {
      if (!this.deleteTarget) return;
      var self = this;
      var path = this.selectedSave;
      this.deleteShow = false;
      if (window.MNL && window.MNL.saves && window.MNL.saves.deleteJson) {
        window.MNL.saves.deleteJson(path).then(function(r) {
          if (r && r.success) {
            self.saves = self.saves.filter(function(s) { return s.path !== path; });
            self.selectedSave = null;
            self.deleteTarget = null;
            self.toast('已删除存档', 'warning');
          } else {
            self.toast('删除失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        })["catch"](function(e) {
          self.toast('删除失败: ' + (e.message || '未知错误'), 'error');
        });
      }
    },
    backupAllSaves: function() {
      if (this.saves.length === 0) { this.toast('没有可备份的存档', 'info'); return; }
      var self = this;
      var count = 0;
      var total = this.saves.length;
      this.saves.forEach(function(s) {
        if (window.MNL && window.MNL.saves && window.MNL.saves.backupJson) {
          window.MNL.saves.backupJson(s.path).then(function(r) { 
            count++;
            if (r && r.success && count === total) self.toast('已备份全部 ' + count + ' 个存档', 'success');
          });
        }
      });
      this.toast('正在备份 ' + total + ' 个存档...', 'info');
    },
    importSave: function() {
      var self = this;
      if (window.MNL && window.MNL.saves && window.MNL.saves.importJson) {
        this.toast('正在打开文件选择器...', 'info');
        window.MNL.saves.importJson().then(function(r) {
          if (r && r.success) {
            self.toast('存档导入成功: ' + r.name, 'success');
            self.refreshSaves();
          } else if (r && r.cancelled) {
            // 用户取消，不提示
          } else {
            self.toast('导入失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        })["catch"](function(e) {
          self.toast('导入失败: ' + (e.message || '未知错误'), 'error');
        });
      }
    }
  },
  mounted: function() { this.refreshSaves(); }
};