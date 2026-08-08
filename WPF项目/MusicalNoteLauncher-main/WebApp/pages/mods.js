// ====== 模组管理 ModsPage (md-) ======
const ModsPage = {
  template: `
    <div class="md-content" style="padding:20px;">
      <div class="md-info-banner">
        <div class="md-info-accent"></div>
        <div class="md-info-body">
          <div class="md-info-title">模组管理器</div>
          <div class="md-info-desc">浏览、安装和管理 Minecraft 模组，支持 CurseForge 和 Modrinth 双源搜索</div>
        </div>
      </div>
      <div class="md-filter-panel">
        <div class="md-filter-row">
          <div class="md-filter-field">
            <label class="md-filter-label">名称</label>
            <div class="md-filter-input-wrap">
              <input type="text" v-model="searchText" placeholder="搜索模组名称或作者..." @keyup.enter="searchMods" />
              <button class="md-clear" v-if="searchText" @click="searchText='';searchMods()">✕</button>
            </div>
          </div>
          <div class="md-filter-field">
            <label class="md-filter-label">来源</label>
            <div class="mnl-select" :class="{ open: sourceOpen }" v-click-outside="() => sourceOpen = false">
              <div class="mnl-select-trigger" @click="sourceOpen = !sourceOpen">
                <span class="mnl-select-text">{{ sourceDisplayText }}</span>
                <span class="mnl-select-arrow">▾</span>
              </div>
              <div class="mnl-select-menu" v-if="sourceOpen">
                <div class="mnl-select-item" v-for="o in sourceOptions" :key="o.v"
                  :class="{ active: sourceFilter === o.v }"
                  @click="sourceFilter = o.v; sourceOpen = false">
                  {{ o.t }}
                </div>
              </div>
            </div>
          </div>
          <div class="md-filter-field">
            <label class="md-filter-label">版本</label>
            <div class="mnl-select" :class="{ open: versionOpen }" v-click-outside="() => versionOpen = false">
              <div class="mnl-select-trigger" @click="versionOpen = !versionOpen">
                <span class="mnl-select-text">{{ versionDisplayText }}</span>
                <span class="mnl-select-arrow">▾</span>
              </div>
              <div class="mnl-select-menu" v-if="versionOpen">
                <div class="mnl-select-item" v-for="o in versionOptions" :key="o.v"
                  :class="{ active: versionFilter === o.v }"
                  @click="versionFilter = o.v; versionOpen = false">
                  {{ o.t }}
                </div>
              </div>
            </div>
          </div>
          <div class="md-filter-field">
            <label class="md-filter-label">排序</label>
            <div class="mnl-select" :class="{ open: sortOpen }" v-click-outside="() => sortOpen = false">
              <div class="mnl-select-trigger" @click="sortOpen = !sortOpen">
                <span class="mnl-select-text">{{ sortDisplayText }}</span>
                <span class="mnl-select-arrow">▾</span>
              </div>
              <div class="mnl-select-menu" v-if="sortOpen">
                <div class="mnl-select-item" v-for="o in sortOptions" :key="o.v"
                  :class="{ active: sortBy === o.v }"
                  @click="sortBy = o.v; sortOpen = false">
                  {{ o.t }}
                </div>
              </div>
            </div>
          </div>
        </div>
        <div class="md-filter-btn-row">
          <button class="md-search-btn" @click="searchMods">搜索</button>
          <button class="md-reset-btn" @click="resetFilters">重置</button>
        </div>
      </div>
      <div class="md-mod-grid">
        <div class="md-mod-card" v-for="mod in pagedMods" :key="mod.id">
          <div class="md-mod-card-header">
            <div class="md-mod-icon" :style="{ background: mod.color || '#8E44AD' }"></div>
            <div>
              <div class="md-mod-name">{{ mod.name }}</div>
              <div class="md-mod-author">{{ mod.author }}</div>
            </div>
          </div>
          <div class="md-mod-desc">{{ mod.description }}</div>
          <div class="md-mod-stats">
            <span>⬇ {{ formatNum(mod.downloads) }}</span>
            <span>📦 {{ mod.version }}</span>
            <span>🏷 {{ mod.source }}</span>
          </div>
          <button class="md-mod-dl-btn" @click="installMod(mod)" :disabled="mod.installing">{{ mod.installing ? '安装中...' : '安装' }}</button>
        </div>
        <div class="md-empty" v-if="pagedMods.length === 0">暂无匹配模组</div>
      </div>
      <div class="md-pagination" v-if="totalPages > 1">
        <button @click="page--" :disabled="page <= 1">上一页</button>
        <button v-for="p in totalPages" :key="p" :class="{ active: p === page }" @click="page = p">{{ p }}</button>
        <button @click="page++" :disabled="page >= totalPages">下一页</button>
      </div>
    </div>
  `,
  data() {
    return {
      searchText: '', sourceFilter: '', versionFilter: '', sortBy: 'popular',
      page: 1, pageSize: 6,
      mods: [],
      sourceOpen: false, versionOpen: false, sortOpen: false
    };
  },
  computed: {
    filteredMods() {
      let list = this.mods;
      if (this.searchText) {
        const kw = this.searchText.toLowerCase();
        list = list.filter(m => m.name.toLowerCase().includes(kw) || (m.author && m.author.toLowerCase().includes(kw)));
      }
      if (this.sourceFilter) list = list.filter(m => m.source === this.sourceFilter);
      return list;
    },
    pagedMods() {
      const start = (this.page - 1) * this.pageSize;
      return this.filteredMods.slice(start, start + this.pageSize);
    },
    totalPages() { return Math.ceil(this.filteredMods.length / this.pageSize); },
    sourceOptions() {
      return [
        { v: '', t: '全部' },
        { v: 'CurseForge', t: 'CurseForge' },
        { v: 'Modrinth', t: 'Modrinth' }
      ];
    },
    versionOptions() {
      return [
        { v: '', t: '全部' },
        { v: '1.21.4', t: '1.21.4' },
        { v: '1.21.1', t: '1.21.1' },
        { v: '1.20.4', t: '1.20.4' },
        { v: '1.19.4', t: '1.19.4' }
      ];
    },
    sortOptions() {
      return [
        { v: 'popular', t: '热门' },
        { v: 'newest', t: '最新' },
        { v: 'downloads', t: '下载量' }
      ];
    },
    sourceDisplayText() {
      const found = this.sourceOptions.find(o => o.v === this.sourceFilter);
      return found ? found.t : this.sourceOptions[0].t;
    },
    versionDisplayText() {
      const found = this.versionOptions.find(o => o.v === this.versionFilter);
      return found ? found.t : this.versionOptions[0].t;
    },
    sortDisplayText() {
      const found = this.sortOptions.find(o => o.v === this.sortBy);
      return found ? found.t : this.sortOptions[0].t;
    }
  },
  methods: {
    formatNum(n) { return n > 1000000 ? (n/1000000).toFixed(1)+'M' : n > 1000 ? (n/1000).toFixed(1)+'K' : n; },
    searchMods() { this.page = 1; },
    resetFilters() { this.searchText = ''; this.sourceFilter = ''; this.versionFilter = ''; this.sortBy = 'popular'; this.page = 1; },
    async installMod(mod) {
      mod.installing = true;
      window.showToast && window.showToast('正在安装 ' + mod.name + '...', 'info');
      await new Promise(r => setTimeout(r, 1200));
      window.showToast && window.showToast(mod.name + ' 安装完成', 'success');
      mod.installing = false;
    },
    loadMods() {
      this.mods = [];
    }
  },
  mounted() { this.loadMods(); }
};
