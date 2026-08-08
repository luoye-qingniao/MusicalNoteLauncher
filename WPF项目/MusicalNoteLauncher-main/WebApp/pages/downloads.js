// ====== 下载任务 DownloadsPage (dp-) ======
const DownloadsPage = {
  template: `
    <div style="padding:20px;display:flex;flex-direction:column;height:100%;">
      <div class="dp-header">
        <span class="dp-header-title">下载任务</span>
        <div style="display:flex;gap:8px;">
          <button v-if="hasRunning" style="padding:6px 16px;border:1px solid var(--border);border-radius:6px;background:transparent;color:var(--text-primary);cursor:pointer;font-size:13px;" @click="pauseAll">暂停全部</button>
          <button style="padding:6px 16px;border:1px solid var(--border);border-radius:6px;background:transparent;color:var(--text-secondary);cursor:pointer;font-size:13px;" @click="clearDone">清除已完成</button>
        </div>
      </div>
      <div class="dp-card" style="flex:1;">
        <div class="dp-table-header"><span>名称</span><span>大小</span><span>进度</span><span>速度</span><span>状态</span><span>操作</span></div>
        <div v-if="tasks.length > 0">
          <div class="dp-task" v-for="t in tasks" :key="t.id">
            <div class="dp-task-name"><span style="margin-right:6px;">📥</span>{{ t.name || t.fileName || t.id }}</div>
            <div style="text-align:center;font-size:12px;color:var(--text-secondary);">{{ t.size || '-' }}</div>
            <div class="dp-task-progress">
              <span style="font-size:11px;min-width:32px;text-align:right;">{{ t.progress }}%</span>
              <div class="dp-progress-bar"><div class="dp-progress-fill" :style="{ width: t.progress + '%' }"></div></div>
            </div>
            <div style="text-align:center;font-size:12px;color:var(--text-secondary);">{{ t.speed || '-' }}</div>
            <div style="text-align:center;font-size:12px;">
              <span :style="{ color: statusColor(t.status) }">{{ t.status }}</span>
            </div>
            <div class="dp-task-actions">
              <button v-if="t.status === '下载中'" @click="pauseTask(t)">暂停</button>
              <button v-if="t.status === '暂停'" @click="resumeTask(t)">继续</button>
              <button class="dp-btn-delete" @click="removeTask(t)">删除</button>
            </div>
          </div>
        </div>
        <div class="dp-empty" v-if="tasks.length === 0">暂无下载任务</div>
      </div>
    </div>
  `,
  data() { return { tasks: [], progressTimers: {} }; },
  computed: {
    hasRunning() { return this.tasks.some(t => t.status === '下载中' || t.status === 'downloading'); }
  },
  methods: {
    toast(msg, type) { window.showToast && window.showToast(msg, type); },
    statusColor(s) { return s === '下载中' || s === 'downloading' ? '#2196F3' : s === '已完成' || s === 'completed' ? '#27AE60' : s === '暂停' || s === 'paused' ? '#FF9800' : '#E74C3C'; },
    statusLabel(s) { const m = { downloading:'下载中', completed:'已完成', paused:'暂停', failed:'失败', pending:'等待中' }; return m[s] || s; },
    async loadTasks() {
      try {
        if (window.MNL && window.MNL.downloads && window.MNL.downloads.getTasks) {
          const list = await window.MNL.downloads.getTasks();
          if (list && list.length) {
            this.tasks = list.map(t => ({ ...t, status: t.status, name: t.name || t.fileName || t.id, progress: t.progress || 0, speed: t.speed || '-' }));
            this.tasks.forEach(t => { if (t.status === 'downloading') this.startProgress(t); });
            return;
          }
        }
      } catch(e) {}
    },
    startProgress(t) {
      const iv = setInterval(() => {
        if (t.progress >= 100 || (t.status !== '下载中' && t.status !== 'downloading')) {
          clearInterval(this.progressTimers[t.id]);
          if (t.progress >= 100) { t.status = '已完成'; t.speed = '-'; }
          return;
        }
        t.progress += Math.floor(Math.random() * 5) + 1;
        t.speed = (Math.random() * 4 + 0.5).toFixed(1) + ' MB/s';
      }, 800);
      this.progressTimers[t.id] = iv;
    },
    pauseTask(t) { t.status = '暂停'; t.speed = '-'; clearInterval(this.progressTimers[t.id]); try { window.MNL && window.MNL.downloads && window.MNL.downloads.pause(t.id); } catch(e) {} },
    resumeTask(t) {
      t.status = '下载中'; this.startProgress(t);
      try { window.MNL && window.MNL.downloads && window.MNL.downloads.resume(t.id); } catch(e) {}
      this.toast('已恢复下载: ' + (t.name || t.id), 'info');
    },
    removeTask(t) {
      if (!confirm('确定删除 "' + (t.name || t.id) + '" 吗？')) return;
      clearInterval(this.progressTimers[t.id]);
      try { window.MNL && window.MNL.downloads && window.MNL.downloads.cancel(t.id); } catch(e) {}
      this.tasks = this.tasks.filter(x => x.id !== t.id);
      this.toast('已删除下载任务', 'warning');
    },
    pauseAll() { this.tasks.forEach(t => { if (t.status === '下载中') { t.status = '暂停'; t.speed = '-'; clearInterval(this.progressTimers[t.id]); } }); },
    clearDone() { this.tasks = this.tasks.filter(t => t.status !== '已完成'); },
  },
  mounted() {
    this.loadTasks();
    var self = this;
    this._onTaskUpdate = function(e) {
      var d = e.detail;
      if (!d || !d.versionId) return;
      var task = self.tasks.find(function(t) { return t.id === d.versionId; });
      if (task) {
        task.progress = d.progress || 0;
        task.status = d.status || '下载中';
        if (d.speed) task.speed = d.speed;
        if (d.size && d.size !== '-- / --') task.size = d.size;
      } else {
        // 新任务：添加到列表
        self.tasks.push({
          id: d.versionId,
          name: d.versionId,
          progress: d.progress || 0,
          speed: d.speed || '-',
          status: d.status || '下载中',
          size: (d.size && d.size !== '-- / --') ? d.size : '计算中...'
        });
      }
    };
    window.addEventListener('mnl:download-task-update', this._onTaskUpdate);
  },
  unmounted() {
    Object.values(this.progressTimers).forEach(iv => clearInterval(iv));
    if (this._onTaskUpdate) window.removeEventListener('mnl:download-task-update', this._onTaskUpdate);
  }
};
