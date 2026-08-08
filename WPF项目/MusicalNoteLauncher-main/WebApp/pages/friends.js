// ====== 好友列表 FriendsPage (fd-) —— 端到端加密聊天 ======
const FriendsPage = {
  template: `
    <div class="page-content friends-page">
      <div class="fd-page">

        <!-- ═══ 顶部标题栏 ═══ -->
        <div class="fd-header">
          <div class="fd-header-left">
            <span class="fd-header-title">好友列表</span>
            <span class="fd-count-badge">{{ friends.length }} 位好友</span>
          </div>
          <button class="fd-btn-primary" @click="addFriend">＋ 添加好友</button>
        </div>

        <!-- ═══ 主体：好友列表 + 聊天面板 ═══ -->
        <div class="fd-main">
          <!-- 左侧：好友列表 -->
          <div class="fd-left">
            <!-- 搜索框 -->
            <div class="fd-search-box">
              <span class="fd-search-icon">🔍</span>
              <input type="text" class="fd-search-input" v-model="searchQuery" placeholder="搜索好友..." />
              <button class="fd-search-clear" v-show="searchQuery" @click="searchQuery=''">✕</button>
            </div>

            <!-- 筛选按钮 -->
            <div class="fd-filter-row">
              <button class="fd-filter-btn" :class="{ active: filter === 'all' }" @click="filter='all'">全部</button>
              <button class="fd-filter-btn" :class="{ active: filter === 'online' }" @click="filter='online'">在线</button>
              <button class="fd-filter-btn" :class="{ active: filter === 'offline' }" @click="filter='offline'">离线</button>
            </div>

            <!-- 好友列表 -->
            <div class="fd-list">
              <div class="fd-empty" v-if="loading">{{ loadingText }}</div>
              <div class="fd-empty" v-else-if="filteredFriends.length === 0">
                <template v-if="friends.length === 0">
                  <span>👥</span>
                  <span>暂无好友</span>
                  <span style="font-size:11px;color:var(--text-muted)">点击「＋ 添加好友」开始添加</span>
                </template>
                <span v-else>没有找到好友</span>
              </div>
              <div class="fd-item" v-for="f in filteredFriends" :key="f.id"
                :class="{ active: selectedId === String(f.id) }" @click="selectFriend(f)">
                <!-- 头像 + 状态点 -->
                <div class="fd-avatar-wrap">
                  <div class="fd-avatar" :style="{ background: f.color || '#6C5CE7' }">{{ (f.name || f.id || '').charAt(0).toUpperCase() }}</div>
                  <span class="fd-status-dot" :class="f.status === 'online' ? 'fd-dot-online' : 'fd-dot-offline'"></span>
                </div>
                <!-- 信息区 -->
                <div class="fd-item-info">
                  <div class="fd-item-name">{{ f.name || f.id }}</div>
                  <div class="fd-item-status" :class="f.status === 'online' ? 'fd-status-online' : 'fd-status-offline'">
                    <template v-if="f.status === 'online'">在线</template>
                    <template v-else>离线</template>
                  </div>
                </div>
                <!-- 操作按钮 -->
                <div class="fd-item-actions">
                  <button class="fd-invite-btn" @click.stop="inviteFriend(f)">邀请</button>
                  <button class="fd-chat-btn" @click.stop="selectFriend(f)">聊天</button>
                  <button class="fd-delete-btn" @click.stop="confirmDeleteFriend(f)" title="删除好友">🗑</button>
                </div>
              </div>
            </div>
          </div>

          <!-- 右侧：聊天面板 -->
          <div class="fd-right">
            <div class="fd-chat-header">
              <span class="fd-chat-title">{{ chatFriend ? (chatFriend.status === 'online' ? '● ' : '⚫ ') + (chatFriend.name || chatFriend.id) : '聊天' }}</span>
              <button class="fd-chat-close" v-show="selectedId" @click="closeChat" title="关闭聊天">✕</button>
            </div>

            <div class="fd-chat-placeholder" v-if="!selectedId">
              <span class="fd-chat-placeholder-icon">💬</span>
              <span class="fd-chat-placeholder-text">选择一位好友开始聊天</span>
            </div>

            <div class="fd-chat-messages" v-show="selectedId" ref="chatMessages">
              <div class="fd-chat-placeholder" style="padding:20px" v-if="currentMessages.length === 0 && !loadingHistory">
                <span style="font-size:24px">💬</span>
                <span style="font-size:12px;color:var(--text-muted)">开始聊天吧（消息已端到端加密 🔐）</span>
              </div>
              <div class="fd-chat-placeholder" style="padding:20px" v-if="loadingHistory">
                <span style="font-size:12px;color:var(--text-muted)">⏳ 加载历史消息...</span>
              </div>
              <div class="fd-msg" v-for="(m, i) in currentMessages" :key="m.id || i"
                :class="m.from === 'self' ? 'fd-msg-self' : 'fd-msg-friend'">
                <div class="fd-msg-sender">{{ m.from === 'self' ? '我' : (chatFriend ? (chatFriend.name || chatFriend.id) : '好友') }} · {{ formatTime(m.time) }}</div>
                <div class="fd-msg-bubble">{{ m.text }}</div>
              </div>
            </div>

            <div class="fd-chat-input" v-show="selectedId">
              <input type="text" class="fd-input" v-model="newMsg" placeholder="输入消息...（端到端加密 🔐）"
                @keydown.enter="sendMsg" :disabled="sending" />
              <button class="fd-btn-primary fd-send-btn" @click="sendMsg" :disabled="sending || !newMsg.trim()">
                {{ sending ? '...' : '发送' }}
              </button>
            </div>
          </div>
        </div>

      </div>

      <!-- ═══ 添加好友对话框 ═══ -->
      <div class="modal-overlay" v-if="showAddDialog" @click.self="closeAddDialog">
        <div class="modal-dialog" style="max-width:400px;">
          <div class="modal-header">
            <h3>👤 添加好友</h3>
            <button class="modal-close" @click="closeAddDialog" :disabled="addFriendLoading">✕</button>
          </div>
          <div class="modal-body">
            <div style="margin-bottom:12px;">
              <label style="display:block;font-size:13px;color:var(--text-secondary);margin-bottom:4px;">好友的青鸟账号 ID</label>
              <input type="text" class="sp-input" v-model="addFriendId" placeholder="输入好友的青鸟ID（纯数字）..."
                style="width:100%;box-sizing:border-box;" @keydown.enter="confirmAddFriend" :disabled="addFriendLoading"
                @input="onAddFriendIdInput" maxlength="12" />
              <div v-if="addFriendIdInputError" style="color:#FF9800;font-size:11px;margin-top:4px;">{{ addFriendIdInputError }}</div>
            </div>
            <div v-if="addFriendError" style="color:#F44336;font-size:12px;margin-bottom:8px;">{{ addFriendError }}</div>
            <div style="font-size:11px;color:var(--text-muted);margin-bottom:14px;">💡 好友可以在「个人资料 → 青鸟账号」中查看自己的青鸟ID</div>
          </div>
          <div class="modal-footer">
            <button class="sp-btn sp-btn-secondary" @click="closeAddDialog" :disabled="addFriendLoading">取消</button>
            <button class="sp-btn sp-btn-primary" @click="confirmAddFriend"
              :disabled="addFriendLoading || !isAddFriendIdValid">
              {{ addFriendLoading ? '验证中...' : '确认添加' }}
            </button>
          </div>
        </div>
      </div>

      <!-- ═══ 删除好友确认对话框 ═══ -->
      <div class="modal-overlay" v-if="showDeleteDialog" @click.self="closeDeleteDialog">
        <div class="modal-dialog" style="max-width:380px;">
          <div class="modal-header">
            <h3>🗑 删除好友</h3>
            <button class="modal-close" @click="closeDeleteDialog">✕</button>
          </div>
          <div class="modal-body">
            <p style="font-size:13px;color:var(--text-primary);margin-bottom:4px;">
              确定要删除好友 <strong>{{ deleteTargetName }}</strong> 吗？
            </p>
            <p style="font-size:11px;color:var(--text-muted);">此操作不可撤销，聊天记录将被清除。</p>
            <div v-if="deleteError" style="color:#F44336;font-size:12px;margin-top:8px;">{{ deleteError }}</div>
          </div>
          <div class="modal-footer">
            <button class="sp-btn sp-btn-secondary" @click="closeDeleteDialog" :disabled="deleteLoading">取消</button>
            <button class="sp-btn sp-btn-danger" @click="doDeleteFriend" :disabled="deleteLoading">
              {{ deleteLoading ? '删除中...' : '确认删除' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  data() {
    return {
      loading: true,
      loadingText: '⏳ 加载中...',
      searchQuery: '',
      filter: 'all',
      selectedId: null,
      chatFriend: null,
      newMsg: '',
      sending: false,
      friends: [],
      // 聊天消息: { [friendId]: [ {id, from, text, time}, ... ] }
      chats: {},
      // 每个好友的消息已拉取到的最大ID，用于增量轮询
      lastMsgIds: {},
      loadingHistory: false,
      pollTimer: null,
      // 添加好友对话框
      showAddDialog: false,
      addFriendId: '',
      addFriendIdInputError: '',
      addFriendError: '',
      addFriendLoading: false,
      // 删除好友对话框
      showDeleteDialog: false,
      deleteTargetId: null,
      deleteTargetName: '',
      deleteError: '',
      deleteLoading: false,
    };
  },
  computed: {
    filteredFriends() {
      let list = this.friends;
      if (this.filter === 'online') {
        list = list.filter(f => f.status === 'online');
      } else if (this.filter === 'offline') {
        list = list.filter(f => f.status !== 'online');
      }
      const q = (this.searchQuery || '').trim().toLowerCase();
      if (q) {
        list = list.filter(f => (f.name || '').toLowerCase().indexOf(q) !== -1);
      }
      return list;
    },
    currentMessages() {
      return this.chats[this.selectedId] || [];
    },
    /// 添加好友 ID 有效性校验：纯数字 + 5~12 位
    isAddFriendIdValid() {
      const v = (this.addFriendId || '').trim();
      return /^\d{5,12}$/.test(v);
    }
  },
  methods: {
    // ====== 加载好友 ======
    async loadFriends() {
      this.loading = true;
      this.loadingText = '⏳ 加载中...';
      try {
        const list = await window.MNL.friends.list();
        this.friends = (list && Array.isArray(list)) ? list : [];
      } catch (e) {
        console.error('[Friends] 加载好友列表失败:', e);
        this.friends = [];
      }
      this.loading = false;
    },

    // ====== 选择好友 / 打开聊天 ======
    async selectFriend(f) {
      const id = String(f.id);
      // 如果已经在跟该好友聊天，不重复加载
      if (this.selectedId === id) return;

      this.selectedId = id;
      this.chatFriend = f;
      if (!this.chats[id]) {
        this.chats[id] = [];
      }
      this.newMsg = '';

      // 从服务器加载历史消息
      await this.loadChatHistory(id);
      this.$nextTick(() => this.scrollMessages());
    },

    // ====== 关闭聊天 ======
    closeChat() {
      this.selectedId = null;
      this.chatFriend = null;
      this.newMsg = '';
    },

    // ====== 加载聊天历史（从服务器） ======
    async loadChatHistory(friendId) {
      this.loadingHistory = true;
      try {
        const sinceId = this.lastMsgIds[friendId] || 0;
        const msgs = await window.MNL.chat.history(friendId, sinceId);
        if (msgs && Array.isArray(msgs) && msgs.length > 0) {
          // 转换服务端格式到本地格式
          const myId = (window._mnlProfile && window._mnlProfile.qingniaoId) || '';
          const local = msgs.map(m => ({
            id: m.id,
            from: m.from === myId ? 'self' : 'friend',
            text: m.text,
            time: m.time
          }));

          if (!this.chats[friendId]) this.chats[friendId] = [];
          // 合并去重
          const existingIds = new Set(this.chats[friendId].map(m => m.id));
          for (const m of local) {
            if (!existingIds.has(m.id)) {
              this.chats[friendId].push(m);
            }
          }
          // 排序
          this.chats[friendId].sort((a, b) => (a.id || 0) - (b.id || 0));

          // 更新该好友的最新消息ID
          const lastMsg = msgs[msgs.length - 1];
          this.lastMsgIds[friendId] = Math.max(this.lastMsgIds[friendId] || 0, lastMsg.id || 0);
        }
      } catch (e) {
        console.error('[Friends] 加载聊天历史失败:', e);
      }
      this.loadingHistory = false;
    },

    // ====== 发送消息（端到端加密） ======
    async sendMsg() {
      if (!this.selectedId || this.sending) return;
      const text = this.newMsg.trim();
      if (!text) return;

      this.sending = true;
      try {
        const r = await window.MNL.chat.send(this.selectedId, text);
        if (r && r.success) {
          // 消息发送成功，添加到本地显示（用临时ID，下次拉取会被服务器ID替换）
          const now = new Date().toISOString();
          if (!this.chats[this.selectedId]) this.chats[this.selectedId] = [];
          this.chats[this.selectedId].push({
            id: Date.now(), // 临时ID
            from: 'self',
            text: text,
            time: now
          });
          this.newMsg = '';
          this.$nextTick(() => this.scrollMessages());

          // 拉取最新消息以获取服务器ID
          setTimeout(() => {
            if (this.selectedId) this.loadChatHistory(this.selectedId);
          }, 500);
        } else {
          window.showToast('发送失败: ' + (r?.error || '未知错误'), 'error');
        }
      } catch (e) {
        window.showToast('发送失败: ' + (e.message || '网络错误'), 'error');
      }
      this.sending = false;
    },

    // ====== 滚动消息到底部 ======
    scrollMessages() {
      const el = this.$refs.chatMessages;
      if (el) el.scrollTop = el.scrollHeight;
    },

    // ====== 格式化时间 ======
    formatTime(iso) {
      if (!iso) return '';
      try {
        const d = new Date(iso);
        return ('0' + d.getHours()).slice(-2) + ':' + ('0' + d.getMinutes()).slice(-2);
      } catch (e) { return ''; }
    },

    // ====== 轮询新消息 ======
    startPolling() {
      if (this.pollTimer) return;
      this.pollTimer = setInterval(async () => {
        if (this.friends.length === 0) return;
        try {
          const globalSince = Math.max(0, ...Object.values(this.lastMsgIds)) || 0;
          const msgs = await window.MNL.chat.poll(globalSince);
          if (!msgs || !Array.isArray(msgs) || msgs.length === 0) return;

          const myId = (window._mnlProfile && window._mnlProfile.qingniaoId) || '';
          for (const m of msgs) {
            const friendId = m.sender === myId ? m.receiver : m.sender;
            if (!this.chats[friendId]) this.chats[friendId] = [];

            // 去重
            const exists = this.chats[friendId].some(ex => ex.id === m.id);
            if (!exists) {
              this.chats[friendId].push({
                id: m.id,
                from: m.sender === myId ? 'self' : 'friend',
                text: m.text,
                time: m.time
              });
            }

            // 更新最新ID
            this.lastMsgIds[friendId] = Math.max(this.lastMsgIds[friendId] || 0, m.id || 0);
          }

          // 如果当前在聊天中，滚动到底部
          if (this.selectedId) {
            this.$nextTick(() => this.scrollMessages());
          }
        } catch (e) {
          // 轮询失败静默忽略
        }
      }, 3000); // 3秒轮询
    },

    stopPolling() {
      if (this.pollTimer) {
        clearInterval(this.pollTimer);
        this.pollTimer = null;
      }
    },

    // ====== 添加好友 ======
    /// 输入框实时过滤：只允许数字
    onAddFriendIdInput() {
      const raw = this.addFriendId || '';
      // 过滤非数字字符
      const filtered = raw.replace(/\D/g, '');
      if (filtered !== raw) {
        this.addFriendId = filtered;
      }
      // 实时校验提示
      if (filtered.length > 0 && filtered.length < 5) {
        this.addFriendIdInputError = '青鸟ID至少5位数字';
      } else if (filtered.length > 12) {
        this.addFriendIdInputError = '青鸟ID最多12位数字';
      } else {
        this.addFriendIdInputError = '';
      }
    },
    addFriend() {
      this.showAddDialog = true;
      this.addFriendId = '';
      this.addFriendError = '';
      this.addFriendLoading = false;
    },
    closeAddDialog() {
      if (this.addFriendLoading) return;
      this.showAddDialog = false;
    },
    async confirmAddFriend() {
      const friendId = this.addFriendId.trim();
      if (!friendId) return;

      this.addFriendLoading = true;
      this.addFriendError = '';
      try {
        const r = await window.MNL.friends.add(friendId);
        if (r && r.success) {
          this.showAddDialog = false;
          window.showToast('好友添加成功', 'success');
          this.loadFriends();
        } else {
          this.addFriendError = r?.error || '添加失败，请检查网络';
        }
      } catch (e) {
        this.addFriendError = '添加失败: ' + (e.message || '网络错误');
      }
      this.addFriendLoading = false;
    },

    // ====== 删除好友 ======
    confirmDeleteFriend(f) {
      this.deleteTargetId = String(f.id);
      this.deleteTargetName = f.name || f.id;
      this.deleteError = '';
      this.deleteLoading = false;
      this.showDeleteDialog = true;
    },
    closeDeleteDialog() {
      if (this.deleteLoading) return;
      this.showDeleteDialog = false;
    },
    async doDeleteFriend() {
      if (!this.deleteTargetId) return;
      this.deleteLoading = true;
      this.deleteError = '';
      try {
        const r = await window.MNL.friends.remove(this.deleteTargetId);
        if (r && r.success) {
          this.showDeleteDialog = false;
          if (this.selectedId === this.deleteTargetId) {
            this.closeChat();
          }
          delete this.chats[this.deleteTargetId];
          delete this.lastMsgIds[this.deleteTargetId];
          window.showToast('好友已删除', 'success');
          this.loadFriends();
        } else {
          this.deleteError = r?.error || '删除失败，请检查网络';
        }
      } catch (e) {
        this.deleteError = '删除失败: ' + (e.message || '网络错误');
      }
      this.deleteLoading = false;
    },

    // ====== 邀请好友 ======
    inviteFriend(f) {
      window.showToast('邀请已发送', 'info');
    }
  },
  async mounted() {
    // 初始化加密密钥
    try { await window.MNL.chat.getMyPublicKey(); } catch (e) {}

    await this.loadFriends();
    this.startPolling();
  },
  beforeUnmount() {
    this.stopPolling();
  }
};
