// ====== 组件商店 StorePage (str-) ======
const StorePage = {
  template: `
    <div class="page-content store-page">
      <div class="str-header">
        <h2 class="str-title">组件商店</h2>
      </div>

      <div class="str-search-row" v-show="currentCat !== 'bg'">
        <input class="str-search-input" v-model="searchText" type="text" placeholder="搜索组件..." @keydown.enter="doSearch" />
        <button class="str-search-btn" @click="doSearch">搜索</button>
      </div>

      <div class="str-categories">
        <button class="str-cat-btn" :class="{ active: currentCat === 'custom' }" @click="switchCat('custom')">自定义组件</button>
        <button class="str-cat-btn" :class="{ active: currentCat === 'bg' }" @click="switchCat('bg')">背景</button>
      </div>

      <div class="str-toolbar" v-show="currentCat === 'bg'">
        <button class="str-upload-btn" @click="uploadBackground">上传背景</button>
        <span class="str-bg-count">{{ bgCountText }}</span>
      </div>

      <!-- 组件列表 -->
      <div class="str-list-wrap" v-show="currentCat !== 'bg'">
        <div class="str-loading" v-if="loading"><div class="str-loading-spinner"></div><span>加载中...</span></div>
        <div class="str-list" v-if="!loading && components.length > 0">
          <div class="str-item" v-for="c in components" :key="'comp-'+c.id">
            <div class="str-item-icon-wrap">
              <span class="str-item-emoji">{{ c.icon || '🧩' }}</span>
            </div>
            <div class="str-item-info">
              <div class="str-item-name">{{ c.name }}</div>
              <div class="str-item-cat">{{ c.catName }}</div>
              <div class="str-item-desc">{{ c.description }}</div>
              <div class="str-item-meta">
                <span>⭐ {{ c.rating.toFixed(1) }}</span>
                <span class="str-item-dl">下载：<b>{{ c.downloads }}</b></span>
              </div>
            </div>
            <button class="str-dl-btn" @click="downloadComponent(c)">下载</button>
          </div>
        </div>
        <div class="str-empty" v-if="!loading && components.length === 0">
          <span class="str-empty-icon">📦</span><span>暂无组件</span>
        </div>
      </div>

      <!-- 背景列表 -->
      <div class="str-bg-list" v-show="currentCat === 'bg'">
        <div class="str-bg-item" v-for="bg in backgrounds" :key="bg.id">
          <div class="str-bg-thumb">
            <span class="str-bg-emoji">🖼️</span>
          </div>
          <div class="str-bg-info">
            <div class="str-bg-name">{{ bg.name }}</div>
            <div class="str-bg-meta">
              <span>类型：<b style="color:#2ECC71">{{ bg.type || 'Image' }}</b></span>
              <span v-if="bg.source === 'server'" class="str-bg-source">🌐 服务器</span>
              <span v-if="bg.downloadCount" class="str-bg-stat">⬇ {{ bg.downloadCount }}</span>
              <span v-if="bg.uploader" class="str-bg-uploader">👤 {{ bg.uploader }}</span>
              <span class="str-bg-date">添加：{{ bg.date }}</span>
            </div>
          </div>
          <button class="str-use-btn" @click="useBackground(bg.id)">使用</button>
          <button class="str-delete-btn" @click="deleteBackground(bg.id)">删除</button>
        </div>
        <div class="str-empty" v-if="backgrounds.length === 0">
          <span class="str-empty-icon">🖼️</span><span>暂无背景素材，点击「上传背景」添加</span>
        </div>
      </div>
    </div>
  `,
  data: function() {
    return {
      currentCat: 'custom',
      searchText: '',
      components: [],
      backgrounds: [],
      loading: false,
    };
  },
  computed: {
    bgCountText: function() {
      if (this.backgrounds.length === 0) return '暂无背景素材，点击上传添加';
      return '共 ' + this.backgrounds.length + ' 个背景素材';
    }
  },
  methods: {
    toast: function(msg, type) { window.showToast && window.showToast(msg, type); },

    switchCat: function(cat) {
      this.currentCat = cat;
      if (cat === 'bg') {
        this.loadBackgrounds();
      } else {
        this.searchText = '';
        this.loadComponents();
      }
    },

    doSearch: function() {
      this.loadComponents();
    },

    loadComponents: function() {
      var self = this;
      self.loading = true;
      if (window.MNL && window.MNL.store && window.MNL.store.getComponentsJson) {
        window.MNL.store.getComponentsJson(this.currentCat, self.searchText, 1).then(function(data) {
          self.components = (data && data.components) ? data.components : [];
          self.loading = false;
        })["catch"](function() {
          self.loading = false;
        });
      } else {
        self.loading = false;
      }
    },

    downloadComponent: function(c) {
      if (c.downloadUrl) {
        if (window.MNL && window.MNL.store && window.MNL.store.trackDownloadJson) {
          window.MNL.store.trackDownloadJson(c.id);
        }
        this.toast('正在打开 ' + (c.name || '') + ' 的下载页面...', 'info');
      } else {
        this.toast('此组件暂未提供下载链接，请联系作者获取', 'warning');
      }
    },

    loadBackgrounds: function() {
      var self = this;
      if (window.MNL && window.MNL.store && window.MNL.store.getBackgroundsJson) {
        window.MNL.store.getBackgroundsJson().then(function(data) {
          self.backgrounds = (data && data.backgrounds) ? data.backgrounds : [];
        })["catch"](function() {});
      }
    },

    uploadBackground: function() {
      var self = this;
      if (window.MNL && window.MNL.store && window.MNL.store.uploadBackgroundJson) {
        window.MNL.store.uploadBackgroundJson().then(function(r) {
          if (r && r.success) {
            if (r.uploadedToServer) {
              self.toast('背景已上传到素材库，所有用户可查看下载', 'success');
            } else {
              self.toast('背景已保存到本地（服务器同步失败，稍后可重试）', 'warning');
            }
            self.loadBackgrounds();
          } else if (!r || !r.cancelled) {
            self.toast('上传失败: ' + ((r && r.error) || '不支持的文件格式（仅支持 JPG/PNG/WEBP/MP4）'), 'error');
          }
        })["catch"](function(e) {
          self.toast('上传失败: ' + (e.message || '未知错误'), 'error');
        });
      }
    },

    useBackground: function(id) {
      var self = this;
      if (window.MNL && window.MNL.store && window.MNL.store.useBackgroundJson) {
        window.MNL.store.useBackgroundJson(id).then(function(r) {
          if (r && r.success) {
            self.toast('已应用背景', 'success');
          } else {
            self.toast('应用失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        });
      }
    },

    deleteBackground: function(id) {
      var self = this;
      if (!confirm('确定要删除此背景吗？此操作不可撤销。')) return;
      if (window.MNL && window.MNL.store && window.MNL.store.deleteBackgroundJson) {
        window.MNL.store.deleteBackgroundJson(id).then(function(r) {
          if (r && r.success) {
            self.backgrounds = self.backgrounds.filter(function(b) { return b.id !== id; });
            self.toast('背景已删除', 'success');
          } else {
            self.toast('删除失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        });
      }
    },
  },
  mounted: function() { this.loadComponents(); }
};
