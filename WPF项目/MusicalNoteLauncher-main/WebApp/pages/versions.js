// ====== 版本管理 VersionsPage (vp-) ======
const VersionsPage = {
  template: `
    <div class="versions-page">
      <div class="vp-header"><span class="vp-header-title">游戏版本</span></div>
      <div style="padding:0 20px;display:flex;flex-direction:column;flex:1;min-height:0;">
        <div class="vp-filter" style="margin-top:20px;">
          <div style="display:flex;align-items:center;gap:12px;flex-wrap:wrap;margin-bottom:8px;">
            <input v-model="searchText" placeholder="搜索版本 ID..." style="flex:1;min-width:180px;" @keyup.enter="page=1" />
            <select v-model="versionType" style="min-width:120px;" @change="page=1">
              <option value="">全部类型</option>
              <option value="release">正式版</option>
              <option value="snapshot">预览版</option>
              <option value="old_alpha">远古版</option>
              <option value="old_beta">远古版(beta)</option>
            </select>
          </div>
          <div class="vp-filter-actions">
            <button class="btn btn-sm" @click="searchText='';versionType='';page=1">重置条件</button>
          </div>
        </div>
        <div class="vp-tabs">
          <button class="vp-tab" :class="{ active: tab === 'downloadable' }" @click="tab='downloadable';loadVersions()">可下载版本</button>
          <button class="vp-tab" :class="{ active: tab === 'installed' }" @click="showInstalled">已安装版本</button>
        </div>
        <div class="vp-list" style="flex:1;overflow-y:auto;">
          <!-- 可下载版本 -->
          <template v-if="tab === 'downloadable'">
            <div class="vp-item" v-for="v in pagedVersions" :key="v.id">
              <div class="vp-item-icon" :style="{ background: v.type === 'release' ? '#27AE60' : v.type === 'snapshot' ? '#F39C12' : '#7F8C8D' }">{{ (v.id || '?').charAt(0).toUpperCase() }}</div>
              <div class="vp-item-info">
                <div class="vp-item-name">{{ v.id }}</div>
                <div class="vp-item-date">{{ v.releaseTime || '' }}</div>
              </div>
              <span class="vp-item-tag" :style="{ background: v.type === 'release' ? '#27AE60' : v.type === 'snapshot' ? '#F39C12' : '#7F8C8D' }">{{ v.type }}</span>
              <button class="vp-item-btn" @click.stop="installVersion(v)" :disabled="v.installing">{{ v.installing ? '安装中...' : loading ? '加载中...' : '安装' }}</button>
            </div>
          </template>
          <!-- 已安装版本 -->
          <template v-if="tab === 'installed'">
            <div class="vp-item vp-item-installed" v-for="v in pagedVersions" :key="v.id" @dblclick="manageMods(v)">
              <div class="vp-item-icon" :style="{ background: v.typeColor || '#546E7A' }">{{ (v.id || '?').charAt(0).toUpperCase() }}</div>
              <div class="vp-item-info">
                <div class="vp-item-name">{{ v.id }}</div>
                <div class="vp-item-date">{{ v.date || '' }}</div>
              </div>
              <span class="vp-item-tag" :style="{ background: v.typeColor || '#546E7A' }">{{ v.type || '未知' }}</span>
              <div class="vp-actions">
                <button class="vp-action-btn vp-action-play" @click.stop="launchVersion(v)" title="启动">▶</button>
                <button class="vp-action-btn vp-action-mods" @click.stop="manageMods(v)" title="管理 Mod">🔧</button>
      <button class="vp-action-btn vp-action-repair" @click.stop="repairVersion(v)" title="修复文件">🩹</button>
      <button class="vp-action-btn vp-action-delete" @click.stop="deleteVersion(v)" title="删除">🗑</button>
              </div>
            </div>
          </template>
          <div class="vp-empty" v-if="!loading && pagedVersions.length === 0">暂无匹配版本</div>
          <div style="text-align:center;padding:20px;color:var(--text-muted);" v-if="loading">加载中...</div>
        </div>
        <div class="md-pagination" v-if="totalPages > 1" style="justify-content:center;margin-top:8px;">
          <button @click="page--" :disabled="page <= 1">上一页</button>
          <button v-for="p in totalPages" :key="p" :class="{ active: p === page }" @click="page = p">{{ p }}</button>
          <button @click="page++" :disabled="page >= totalPages">下一页</button>
        </div>
      </div>
    </div>

      <!-- Java 兼容性对话框 -->
      <div class="modal-overlay" v-if="javaCheckShow" @click.self="javaCheckShow = false">
        <div style="background:var(--card-bg);border:1px solid var(--border);border-radius:12px;padding:0;max-width:500px;width:90vw;">
          <div style="display:flex;align-items:center;justify-content:space-between;padding:16px 20px;border-bottom:1px solid var(--border);">
            <div>
              <div style="font-weight:700;font-size:16px;color:var(--text-primary);">⚠️ Java 兼容性警告</div>
              <div style="font-size:12px;color:var(--text-secondary);margin-top:2px;">当前 Java 版本与游戏版本不匹配</div>
            </div>
            <button @click="javaCheckShow = false" style="background:none;border:none;font-size:20px;color:var(--text-secondary);cursor:pointer;">✕</button>
          </div>
          <div style="padding:20px;">
            <div style="font-size:13px;color:var(--text-secondary);margin-bottom:12px;line-height:1.6;">
              当前 Java：<strong style="color:var(--text-primary);">{{ javaCheckResult.currentJavaMajor || '??' }}</strong><br/>
              {{ javaCheckResult.reason }}<br/>
              <span v-if="javaCheckResult.recommendedPath" style="color:#FF9800;">
                推荐使用 Java {{ javaCheckResult.recommendedMajor }}，否则可能导致游戏崩溃。
              </span>
              <span v-else style="color:#E53935;">
                未检测到其他可用 Java 版本，将自动强制使用当前 Java。
              </span>
            </div>
            <div v-if="javaCheckResult.alternatives && javaCheckResult.alternatives.length > 0" style="margin-bottom:12px;">
              <div style="font-size:12px;color:var(--text-secondary);margin-bottom:6px;">已检测到的 Java 版本：</div>
              <div v-for="alt in javaCheckResult.alternatives.slice(0, 6)" :key="alt.path"
                :style="{ padding:'4px 8px', fontSize:'12px', borderRadius:'4px', marginBottom:'2px',
                  background: alt.compatible ? 'rgba(39,174,96,0.1)' : 'rgba(229,57,53,0.05)',
                  color: alt.compatible ? '#27AE60' : 'var(--text-secondary)' }">
                {{ alt.compatible ? '✅' : '❌' }} Java {{ alt.majorVersion }}
                <span v-if="alt.isCurrent" style="font-weight:600;">(当前)</span>
              </div>
            </div>
          </div>
          <div style="display:flex;gap:10px;justify-content:flex-end;padding:16px 20px;border-top:1px solid var(--border);">
            <button @click="javaCheckShow = false" style="padding:8px 16px;border:1px solid var(--border);border-radius:6px;background:transparent;color:var(--text-primary);cursor:pointer;font-size:13px;">取消</button>
            <button v-if="javaCheckResult.recommendedPath" @click="javaCheckUseRecommended()"
              style="padding:8px 16px;border:none;border-radius:6px;background:#27AE60;color:#fff;cursor:pointer;font-size:13px;">
              使用推荐版本 (Java {{ javaCheckResult.recommendedMajor }})
            </button>
            <button @click="javaCheckForce()"
              style="padding:8px 16px;border:none;border-radius:6px;background:#E65100;color:#fff;cursor:pointer;font-size:13px;">
              强制使用当前 Java
            </button>
          </div>
        </div>
      </div>
  `,
  data() {
    return { tab: 'downloadable', searchText: '', versionType: '', versions: [], installedVersions: [], page: 1, pageSize: 20, loading: false,
      // Java 兼容性对话框
      javaCheckShow: false,
      javaCheckResult: null,
      javaCheckVersionId: null };
  },
  computed: {
    filteredVersions() {
      let list = this.tab === 'installed' ? this.installedVersions : this.versions;
      if (this.searchText) { const kw = this.searchText.toLowerCase(); list = list.filter(v => (v.id || '').toLowerCase().includes(kw)); }
      if (this.versionType) { list = list.filter(v => v.type === this.versionType); }
      return list;
    },
    pagedVersions() { const start = (this.page - 1) * this.pageSize; return this.filteredVersions.slice(start, start + this.pageSize); },
    totalPages() { return Math.ceil(this.filteredVersions.length / this.pageSize); }
  },
  methods: {
    async loadVersions() {
      this.loading = true;
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.getAvailable) {
          this.versions = await window.MNL.versions.getAvailable(this.versionType) || [];
        }
      } catch(e) {}
      this.loading = false;
    },
    async showInstalled() {
      this.tab = 'installed'; this.loading = true; this.page = 1;
      try {
        // 使用 getInstalledJson（JSON 安全版本，避免 Vue 响应式崩溃）
        if (window.MNL && window.MNL.versions && window.MNL.versions.getInstalledJson) {
          this.installedVersions = await window.MNL.versions.getInstalledJson() || [];
        }
      } catch(e) { this.installedVersions = []; }
      this.loading = false;
    },
    async installVersion(v) {
      v.installing = true;
      window.showToast && window.showToast('正在安装 ' + v.id + '...', 'info');
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.install) {
          await window.MNL.versions.install(v.id);
        } else { await new Promise(r => setTimeout(r, 1500)); }
        window.showToast && window.showToast(v.id + ' 安装完成', 'success');
      } catch(e) { window.showToast && window.showToast('安装失败', 'error'); }
      v.installing = false;
    },
    async launchVersion(v) {
      // 启动前检查 Java 兼容性（参考 PCL McLaunchJava）
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.checkJava) {
          var result = await window.MNL.versions.checkJava(v.id);
          if (result && result.compatible === false) {
            // 如果没有替代 Java，直接强制使用
            if (!result.recommendedPath || !result.hasAlternatives) {
              window.showToast && window.showToast('未检测到兼容 Java，将强制使用当前版本', 'warning');
              window.MNL.versions.launch(v.id, {});
              return;
            }
            // 有替代 Java，弹窗确认
            this.javaCheckResult = result;
            this.javaCheckVersionId = v.id;
            this.javaCheckShow = true;
            return;
          }
        }
      } catch(e) { /* 检查失败则直接启动 */ }
      // 兼容或检查失败，直接启动
      window.showToast && window.showToast('正在启动 ' + v.id + '...', 'info');
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.launch) {
          await window.MNL.versions.launch(v.id, {});
        }
      } catch(e) { window.showToast && window.showToast('启动失败: ' + e.message, 'error'); }
    },
    javaCheckForce: function() {
      this.javaCheckShow = false;
      var id = this.javaCheckVersionId;
      if (id) {
        try { window.MNL.versions.launch(id, {}); } catch(e) {}
      }
    },
    javaCheckUseRecommended: async function() {
      this.javaCheckShow = false;
      var id = this.javaCheckVersionId;
      if (this.javaCheckResult && this.javaCheckResult.recommendedPath) {
        try {
          if (window.MNL && window.MNL.java && window.MNL.java.savePath) {
            await window.MNL.java.savePath(this.javaCheckResult.recommendedPath);
          }
          window.showToast && window.showToast('已切换为 Java ' + this.javaCheckResult.recommendedMajor, 'success');
        } catch(e) {}
      }
      if (id) { try { window.MNL.versions.launch(id, {}); } catch(e) {} }
    },
    async manageMods(v) {
      try {
        this.$emit('navigate', 'mods');
        // 如果有版本上下文切换，可以传递版本信息
      } catch(e) {}
    },
    async repairVersion(v) {
      window.showToast && window.showToast('正在修复 ' + v.id + '...', 'info');
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.repair) {
          await window.MNL.versions.repair(v.id);
        }
        // 修复完成后刷新已安装列表
        this.loadVersions();
      } catch(e) { window.showToast && window.showToast('修复失败: ' + e.message, 'error'); }
    },
    async deleteVersion(v) {
      if (!confirm('确定要删除版本 ' + v.id + ' 吗？\n此操作不可撤销。')) return;
      window.showToast && window.showToast('正在删除 ' + v.id + '...', 'info');
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.delete) {
          await window.MNL.versions.delete(v.id);
        }
        this.installedVersions = this.installedVersions.filter(x => x.id !== v.id);
        window.showToast && window.showToast(v.id + ' 已删除', 'success');
      } catch(e) { window.showToast && window.showToast('删除失败: ' + e.message, 'error'); }
    }
  },
  watch: {
    versionType() { if (this.tab === 'downloadable') { this.page = 1; this.loadVersions(); } }
  },
  mounted() { this.loadVersions(); }
};
