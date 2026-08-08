// ====== 交流社区 CommunityPage (cm-) ======
const CommunityPage = {
  template: `
    <div class="page-content community-page">

      <!-- ══════════════════════════ 频道列表 ══════════════════════════ -->
      <div class="cm-panel" v-if="currentView === 'channels'">
        <div class="cm-header">
          <div class="cm-header-left">
            <h2 class="cm-header-title">🎮 交流社区</h2>
            <p class="cm-header-sub">选择一个频道，开始交流</p>
          </div>
          <div class="cm-header-right">
            <div class="cm-user-badge" v-if="communityUser && communityUser.isLoggedIn">
              <span class="cm-avatar cm-avatar-sm" v-if="communityUser.avatarUrl" :style="{ backgroundImage: 'url(' + communityUser.avatarUrl + ')' }"></span>
              <span class="cm-avatar cm-avatar-sm" v-else :style="{ background: communityUser.avatarColor }">{{ communityUser.avatarChar }}</span>
              <span class="cm-user-badge-name">{{ communityUser.nickname }}</span>
            </div>
            <span class="cm-login-hint" v-else>🔒 登录后发帖</span>
            <button class="cm-btn-primary" @click="showCreateChannel" :disabled="!communityUser || !communityUser.isLoggedIn">＋ 创建频道</button>
          </div>
        </div>
        <div class="cm-channel-list">
          <div class="cm-loading" v-if="loading">⏳ 加载中...</div>
          <div class="cm-empty" v-else-if="channels.length === 0">🔍 暂无频道，点击上方按钮创建一个吧</div>
          <div class="cm-channel-card" v-for="ch in channels" :key="ch.id" @click="selectChannel(ch)">
            <div class="cm-channel-icon-box">{{ ch.icon_emoji || '💬' }}</div>
            <div class="cm-channel-info">
              <div class="cm-channel-name">{{ ch.name || '' }}</div>
              <div class="cm-channel-desc">{{ ch.description || '' }}</div>
            </div>
            <div class="cm-channel-meta">
              <span class="cm-channel-count">{{ ch.message_count || 0 }} 帖</span>
              <span class="cm-channel-arrow">→</span>
            </div>
          </div>
        </div>
      </div>

      <!-- ══════════════════════════ 帖子列表 ══════════════════════════ -->
      <div class="cm-panel" v-if="currentView === 'posts'">
        <div class="cm-post-header-bar">
          <button class="cm-btn-secondary" @click="goBackToChannels">← 返回</button>
          <div class="cm-post-header-info">
            <span class="cm-post-header-title">{{ (selectedChannel.icon_emoji || '💬') + ' ' + (selectedChannel.name || '') }}</span>
            <span class="cm-post-header-desc">{{ selectedChannel.description || '' }}</span>
          </div>
          <button class="cm-btn-primary" @click="checkLoginThenPost" :disabled="!communityUser || !communityUser.isLoggedIn">✏️ 发帖</button>
        </div>

        <!-- 未登录发帖提示 -->
        <div class="cm-login-banner" v-if="!communityUser || !communityUser.isLoggedIn">
          <span>🔒 请先登录青鸟账号后再发帖</span>
        </div>

        <!-- 发帖表单（默认隐藏） -->
        <div class="cm-create-post" v-if="showNewPostForm">
          <h3 class="cm-create-title">发布新帖子</h3>
          <label class="cm-form-label">帖子标题</label>
          <span class="cm-form-hint">给你的帖子起一个吸引人的标题（2-50字）</span>
          <input class="cm-input" v-model="newPostTitle" type="text" maxlength="50" placeholder="请输入帖子标题..." />
          <label class="cm-form-label">帖子内容</label>
          <span class="cm-form-hint">分享你的想法、经验或问题（支持换行，最多2000字）</span>
          <textarea class="cm-input cm-textarea" v-model="newPostContent" maxlength="2000" placeholder="请输入帖子内容..."></textarea>
          <div class="cm-form-actions">
            <button class="cm-btn-secondary" @click="toggleNewPostForm">取消</button>
            <button class="cm-btn-primary" @click="submitPost">发布</button>
          </div>
        </div>

        <!-- 帖子列表 -->
        <div class="cm-post-list">
          <div class="cm-loading" v-if="loading">⏳ 加载中...</div>
          <div class="cm-empty" v-else-if="posts.length === 0">🔍 暂无帖子，点击"发帖"发布第一条消息吧</div>
          <div class="cm-post-card" v-for="p in posts" :key="p.id" @click.self="openPostDetail(p)">
            <div class="cm-avatar cm-avatar-40" :style="p.sender_avatar ? {} : { background: getSenderColor(p) }">
              <img v-if="p.sender_avatar" :src="p.sender_avatar" class="cm-avatar-img" alt="" />
              <span v-else>{{ getSenderChar(p) }}</span>
            </div>
            <div class="cm-post-card-body">
              <div class="cm-post-card-title">{{ p.title || (p.content ? p.content.substring(0, 30) + (p.content.length > 30 ? '...' : '') : '') }}</div>
              <div class="cm-post-card-preview">{{ p.content || '' }}</div>
              <div class="cm-post-card-meta">
                <span class="cm-nickname-link" @click="goToUserProfile(p)" :title="'查看 ' + (p.sender_nickname || p.sender_id || '匿名') + ' 的主页'">{{ p.sender_nickname || p.sender_id || '匿名' }}</span>
                <span class="cm-post-card-meta-sep">·</span>
                <span>{{ formatTime(p.created_at) }}</span>
              </div>
              <div class="cm-post-card-actions">
                <button class="cm-btn-icon" @click.stop="togglePostLike(p)">👍 {{ formatNum(p._likes || 0) }}</button>
                <span>💬</span><span>{{ formatNum(p._commentCount || 0) }}</span>
                <span>👁</span><span>{{ formatNum(p._viewCount || 0) }}</span>
                <button class="cm-btn-icon" @click.stop="sharePost(p)">📋 分享</button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- ══════════════════════════ 帖子详情 ══════════════════════════ -->
      <div class="cm-panel" v-if="currentView === 'detail' && selectedPost">
        <div class="cm-detail-header">
          <button class="cm-btn-secondary" @click="goBackToPosts">← 返回</button>
          <div class="cm-detail-author">
            <div class="cm-avatar cm-avatar-36" :style="selectedPost.sender_avatar ? {} : { background: getSenderColor(selectedPost) }">
              <img v-if="selectedPost.sender_avatar" :src="selectedPost.sender_avatar" class="cm-avatar-img" alt="" />
              <span v-else>{{ getSenderChar(selectedPost) }}</span>
            </div>
            <div class="cm-detail-author-info">
              <span class="cm-nickname-link cm-detail-author-name" @click="goToUserProfile(selectedPost)" :title="'查看 ' + (selectedPost.sender_nickname || selectedPost.sender_id || '匿名') + ' 的主页'">{{ selectedPost.sender_nickname || selectedPost.sender_id || '匿名' }}</span>
              <span class="cm-detail-author-time">{{ formatTime(selectedPost.created_at) }}</span>
            </div>
          </div>
          <button class="cm-btn-icon" @click="sharePost(selectedPost)">📋 分享</button>
        </div>

        <div class="cm-detail-scroll">
          <h3 class="cm-detail-title">{{ selectedPost.title || '无标题' }}</h3>
          <p class="cm-detail-content">{{ selectedPost.content || '' }}</p>

          <!-- 操作栏 -->
          <div class="cm-detail-actions">
            <button class="cm-btn-icon" @click="togglePostLike(selectedPost)">
              {{ selectedPost._isLiked ? '👍 已赞' : '👍 点赞' }}
            </button>
            <span class="cm-detail-stats">👍 {{ selectedPost._likes || 0 }}  ·  💬 {{ selectedPost._commentCount || 0 }}  ·  👁 {{ selectedPost._viewCount || 0 }}</span>
          </div>

          <!-- 评论标题 -->
          <h4 class="cm-comments-title">💬 评论 ({{ selectedPost._commentCount || 0 }})</h4>

          <!-- 评论列表 -->
          <div class="cm-comment-list">
            <div class="cm-comment-empty" v-if="postComments.length === 0">暂无评论，来说两句吧</div>
            <div class="cm-comment-item" v-for="c in postComments" :key="c.id">
              <div class="cm-avatar cm-avatar-30" v-if="c.avatarUrl" :style="{ backgroundImage: 'url(' + c.avatarUrl + ')' }"></div>
              <div class="cm-avatar cm-avatar-30" v-else :style="c.avatarColor ? { background: c.avatarColor } : {}">{{ c.avatar || c.avatarChar || '👤' }}</div>
              <div class="cm-comment-body">
                <div class="cm-comment-header">
                  <span class="cm-comment-author">{{ c.author || '匿名' }}</span>
                  <span class="cm-comment-time">{{ c.time || '刚刚' }}</span>
                </div>
                <div class="cm-comment-text">{{ c.text || '' }}</div>
                <button class="cm-btn-icon cm-comment-like" @click="toggleCommentLike(c)" style="font-size:12px;padding:4px 6px;margin-top:4px;">
                  👍 {{ c._likes || 0 }}
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- 评论输入 -->
        <div class="cm-comment-input-bar">
          <div class="cm-comment-input-label">发表评论</div>
          <div class="cm-comment-input-row">
            <input class="cm-input cm-comment-input" v-model="commentInput" type="text" maxlength="500"
              placeholder="写下你的评论...（Enter 发送，Shift+Enter 换行）"
              @keydown="onCommentKeydown" />
            <button class="cm-btn-primary" @click="submitComment">发送</button>
          </div>
        </div>
      </div>

      <!-- ══════════════════════════ 创建频道对话框 ══════════════════════════ -->
      <div class="cm-overlay" v-if="showCreateChannelDialog" @click.self="showCreateChannelDialog = false">
        <div class="cm-dialog cm-dialog-create-channel">
          <div class="cm-dialog-header">
            <h3 class="cm-dialog-title">✨ 创建新频道</h3>
            <button class="cm-dialog-close" @click="showCreateChannelDialog = false" title="关闭">✕</button>
          </div>
          <div class="cm-dialog-body">
            <!-- 频道图标预览 + 选择 -->
            <div class="cm-channel-icon-picker">
              <div class="cm-channel-icon-preview" :style="{ background: channelIconColor }">{{ newChannelIcon || '💬' }}</div>
              <div class="cm-channel-icon-input-row">
                <label class="cm-form-label">频道图标</label>
                <div class="cm-emoji-quick" @click="cycleChannelIcon">🎲 随机</div>
              </div>
              <input class="cm-input" v-model="newChannelIcon" type="text" maxlength="4" placeholder="输入 Emoji，如 💬 🎮" @input="onChannelIconInput" />
            </div>

            <label class="cm-form-label">频道名称 <span class="cm-form-required">*</span></label>
            <input class="cm-input cm-input-lg" v-model="newChannelName" type="text" maxlength="20" placeholder="给你的频道起个名字..." />

            <label class="cm-form-label">频道简介</label>
            <textarea class="cm-input cm-textarea" v-model="newChannelDesc" maxlength="100" placeholder="一句话介绍这个频道是做什么的..." rows="2"></textarea>
            <div class="cm-form-char-count">{{ (newChannelDesc || '').length }}/100</div>
          </div>
          <div class="cm-dialog-footer">
            <button class="cm-btn-secondary" @click="showCreateChannelDialog = false">取消</button>
            <button class="cm-btn-primary cm-btn-lg" @click="createChannel">创建频道</button>
          </div>
        </div>
      </div>

    </div>
  `,
  data() {
    return {
      currentView: 'channels',
      channels: [],
      posts: [],
      selectedChannel: null,
      selectedPost: null,
      comments: [],
      nextCommentId: 1,
      loading: false,
      showNewPostForm: false,
      showCreateChannelDialog: false,
      newChannelName: '',
      newChannelDesc: '',
      newChannelIcon: '💬',
      channelIconColor: '#6C5CE7',
      newPostTitle: '',
      newPostContent: '',
      commentInput: '',
      userName: '',
      /// 当前登录用户的社区身份信息
      communityUser: null,
    };
  },
  computed: {
    postComments() {
      if (!this.selectedPost) return [];
      return this.comments
        .filter(function(c) { return String(c.postId) === String(this.selectedPost.id); }.bind(this))
        .sort(function(a, b) { return a.id - b.id; });
    }
  },
  methods: {
    /* ====== 工具函数 ====== */
    toast: function(msg, type) {
      window.showToast && window.showToast(msg, type);
    },

    /// 根据 sender_id / sender_nickname 派生稳定颜色
    getSenderColor: function(post) {
      if (!post) return '#6C5CE7';
      var key = post.sender_nickname || post.sender_id || '';
      if (!key) return '#6C5CE7';
      var hash = 0;
      for (var i = 0; i < key.length; i++) { hash = key.charCodeAt(i) + ((hash << 5) - hash); }
      var hue = ((hash & 0x7fffffff) % 360);
      return 'hsl(' + hue + ', 55%, 48%)';
    },

    /// 发帖人头像字符
    getSenderChar: function(post) {
      if (!post) return '?';
      var name = post.sender_nickname || post.sender_id || '';
      return name ? name[0].toUpperCase() : '?';
    },

    /// 跳转到用户个人主页
    goToUserProfile: function(post) {
      if (!post) return;
      var userId = post.sender_id || '';
      if (!userId) return;
      console.log('[Community] goToUserProfile:', userId);
      try {
        if (window.navigateTo) {
          window.navigateTo('profile', { userId: userId });
        } else if (this.$root && this.$root.navigateTo) {
          this.$root.navigateTo('profile', { userId: userId });
        } else {
          console.error('[Community] navigateTo not available');
        }
      } catch(e) {
        console.error('[Community] goToUserProfile error:', e);
      }
    },

    formatTime: function(serverTime) {
      if (!serverTime) return '';
      try {
        var dt = new Date(serverTime);
        var now = new Date();
        var diffMs = now - dt;
        if (diffMs < 0) return '';
        if (diffMs < 60000) return '刚刚';
        if (diffMs < 3600000) return Math.floor(diffMs / 60000) + '分钟前';
        if (diffMs < 86400000) return Math.floor(diffMs / 3600000) + '小时前';
        if (diffMs < 604800000) return Math.floor(diffMs / 86400000) + '天前';
        var mm = String(dt.getMonth() + 1).padStart(2, '0');
        var dd = String(dt.getDate()).padStart(2, '0');
        var hh = String(dt.getHours()).padStart(2, '0');
        var min = String(dt.getMinutes()).padStart(2, '0');
        return mm + '-' + dd + ' ' + hh + ':' + min;
      } catch(e) { return serverTime; }
    },

    formatNum: function(n) {
      if (n >= 10000) return (n / 10000).toFixed(1) + 'w';
      if (n >= 1000) return (n / 1000).toFixed(1) + 'k';
      return String(n || 0);
    },

    /* ====== 加载频道 ====== */
    loadChannels: function() {
      if (this.loading) return;
      this.loading = true;
      var self = this;
      if (window.MNL && window.MNL.community && window.MNL.community.getChannelsJson) {
        window.MNL.community.getChannelsJson().then(function(data) {
          self.channels = (data && data.channels) ? data.channels : [];
          self.loading = false;
        })["catch"](function(e) {
          self.channels = [];
          self.loading = false;
        });
      } else {
        self.channels = [];
        self.loading = false;
      }
    },

    /* ====== 选择频道 → 加载消息 ====== */
    selectChannel: function(ch) {
      this.selectedChannel = ch;
      this.showNewPostForm = false;
      this.newPostTitle = '';
      this.newPostContent = '';
      this.currentView = 'posts';
      this.loadMessages(ch);
    },

    /* ====== 加载帖子 ====== */
    loadMessages: function(ch) {
      this.loading = true;
      var self = this;
      if (window.MNL && window.MNL.community && window.MNL.community.getMessagesJson) {
        window.MNL.community.getMessagesJson(ch.id).then(function(data) {
          var raw = (data && data.messages) ? data.messages : [];
          self.posts = raw.map(function(p) {
            p._likes = p._likes || 0;
            p._commentCount = p._commentCount || 0;
            p._viewCount = p._viewCount || 0;
            p._isLiked = false;
            return p;
          });
          self.loading = false;
        })["catch"](function(e) {
          self.posts = [];
          self.loading = false;
        });
      } else {
        self.posts = [];
        self.loading = false;
      }
    },

    /* ====== 打开帖子详情 ====== */
    openPostDetail: function(post) {
      this.selectedPost = post;
      if (!post._viewCount) post._viewCount = 0;
      post._viewCount++;
      this.currentView = 'detail';
      this.commentInput = '';
      // 滚动到顶部
      var self = this;
      this.$nextTick(function() {
        var el = self.$el.querySelector('.cm-detail-scroll');
        if (el) el.scrollTop = 0;
      });
    },

    /* ====== 点赞帖子 ====== */
    togglePostLike: function(post) {
      if (!post) return;
      if (post._isLiked) {
        post._likes = Math.max(0, (post._likes || 0) - 1);
        post._isLiked = false;
      } else {
        post._likes = (post._likes || 0) + 1;
        post._isLiked = true;
      }
      // 同步 selectedPost
      if (this.selectedPost && this.selectedPost.id === post.id) {
        this.selectedPost._likes = post._likes;
        this.selectedPost._isLiked = post._isLiked;
      }
    },

    /* ====== 点赞评论 ====== */
    toggleCommentLike: function(comment) {
      if (!comment) return;
      if (comment._isLiked) {
        comment._likes = Math.max(0, (comment._likes || 0) - 1);
        comment._isLiked = false;
      } else {
        comment._likes = (comment._likes || 0) + 1;
        comment._isLiked = true;
      }
    },

    /* ====== 分享帖子 ====== */
    sharePost: function(post) {
      if (!post) return;
      var content = post.content || '';
      var shortContent = content.length > 100 ? content.substring(0, 100) + '...' : content;
      var shareText = '【MNL 社区】' + (post.title || '') + '\n\n' + shortContent + '\n\n—— ' + (post.sender_id || '匿名') + ' 发布于 MNL 音符盒';
      var self = this;

      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(shareText).then(function() {
          self.toast('帖子内容已复制到剪贴板，可以粘贴分享给朋友！', 'success');
        }).catch(function() {
          self.toast('复制失败，请手动复制内容。', 'warning');
        });
      } else {
        try {
          var ta = document.createElement('textarea');
          ta.value = shareText;
          ta.style.position = 'fixed'; ta.style.opacity = '0';
          document.body.appendChild(ta);
          ta.select();
          document.execCommand('copy');
          document.body.removeChild(ta);
          self.toast('帖子内容已复制到剪贴板，可以粘贴分享给朋友！', 'success');
        } catch(e) {
          self.toast('复制失败，请手动复制内容。', 'warning');
        }
      }
    },

    /* ====== 评论 ====== */
    submitComment: function() {
      var text = (this.commentInput || '').trim();
      if (!text) return;
      if (!this.selectedPost) return;

      var authorName = (this.communityUser && this.communityUser.nickname) || this.userName || '用户';
      var cu = this.communityUser;
      var comment = {
        id: this.nextCommentId++,
        postId: this.selectedPost.id,
        author: authorName,
        avatarUrl: (cu && cu.avatarUrl) || '',
        avatarChar: (cu && cu.avatarChar) || '👤',
        avatarColor: (cu && cu.avatarColor) || '',
        text: text,
        time: '刚刚',
        _likes: 0,
        _isLiked: false,
      };
      this.comments.push(comment);

      this.selectedPost._commentCount = (this.selectedPost._commentCount || 0) + 1;

      // 同步回 posts 数组
      var idx = this.posts.findIndex(function(p) { return p.id === this.selectedPost.id; }.bind(this));
      if (idx >= 0) this.posts[idx]._commentCount = this.selectedPost._commentCount;

      this.commentInput = '';
    },

    onCommentKeydown: function(e) {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        this.submitComment();
      }
    },

    /* ====== 发帖 ====== */
    checkLoginThenPost: function() {
      if (!this.communityUser || !this.communityUser.isLoggedIn) {
        this.toast('请先登录青鸟账号后再发帖', 'warning');
        return;
      }
      this.toggleNewPostForm();
    },

    toggleNewPostForm: function() {
      this.showNewPostForm = !this.showNewPostForm;
      if (this.showNewPostForm) {
        this.newPostTitle = '';
        this.newPostContent = '';
        var self = this;
        this.$nextTick(function() {
          var el = self.$el.querySelector('.cm-create-post input.cm-input');
          if (el) el.focus();
        });
      }
    },

    submitPost: function() {
      var title = (this.newPostTitle || '').trim();
      var content = (this.newPostContent || '').trim();

      if (!title) { this.toast('请输入帖子标题', 'warning'); return; }
      if (title.length < 2) { this.toast('帖子标题至少需要2个字', 'warning'); return; }
      if (!content) { this.toast('请输入帖子内容', 'warning'); return; }
      if (!this.selectedChannel) return;
      if (!this.communityUser || !this.communityUser.isLoggedIn) {
        this.toast('请先登录青鸟账号后再发帖', 'warning');
        return;
      }

      var senderId = this.communityUser.qingniaoId || '';
      var self = this;
      var ch = this.selectedChannel;

      if (window.MNL && window.MNL.community && window.MNL.community.sendMessageJson) {
        window.MNL.community.sendMessageJson(ch.id, senderId, title, content).then(function(result) {
          if (result && result.success) {
            self.posts.unshift({
              id: result.id,
              channel_id: ch.id,
              sender_id: senderId,
              sender_nickname: result.sender_nickname || self.communityUser.nickname || senderId,
              sender_avatar: result.sender_avatar || '',
              title: title,
              content: content,
              created_at: result.created_at || new Date().toISOString(),
              _likes: 0, _commentCount: 0, _viewCount: 0, _isLiked: false
            });
            self.showNewPostForm = false;
            self.newPostTitle = '';
            self.newPostContent = '';
            self.toast('发布成功！', 'success');
          } else {
            self.toast((result && result.error) || '发送失败，请稍后重试。', 'warning');
          }
        })["catch"](function(e) {
          self.toast('发送失败，请稍后重试。', 'warning');
        });
      } else {
        self.toast('服务不可用，请稍后重试', 'warning');
      }
    },

    /* ====== 创建频道 ====== */
    showCreateChannel: function() {
      if (!this.communityUser || !this.communityUser.isLoggedIn) {
        this.toast('请先登录青鸟账号后再创建频道', 'warning');
        return;
      }
      this.showCreateChannelDialog = true;
      this.newChannelName = '';
      this.newChannelDesc = '';
      this.newChannelIcon = '💬';
      this.channelIconColor = '#6C5CE7';
      var self = this;
      this.$nextTick(function() {
        var el = self.$el.querySelector('.cm-dialog-create-channel input.cm-input-lg');
        if (el) el.focus();
      });
    },

    /// 随机切换频道图标 Emoji
    cycleChannelIcon: function() {
      var emojis = ['💬', '🎮', '🏠', '🎵', '📚', '🎨', '⚽', '🍕', '🌟', '🔥', '💡', '🎯', '🛠', '🌍', '📷', '🎬', '🐱', '🚀', '💎', '🌈'];
      var pool = emojis.filter(function(e) { return e !== this.newChannelIcon; }.bind(this));
      this.newChannelIcon = pool[Math.floor(Math.random() * pool.length)];
      this.deriveIconColor();
    },

    /// Emoji 输入时派生颜色
    onChannelIconInput: function() {
      this.deriveIconColor();
    },

    /// 根据 Emoji 字符派生图标背景色
    deriveIconColor: function() {
      var key = this.newChannelIcon || '💬';
      var hash = 0;
      for (var i = 0; i < key.length; i++) { hash = key.charCodeAt(i) + ((hash << 5) - hash); }
      var hue = ((hash & 0x7fffffff) % 360);
      this.channelIconColor = 'hsl(' + hue + ', 50%, 45%)';
    },

    createChannel: function() {
      var name = (this.newChannelName || '').trim();
      var desc = (this.newChannelDesc || '').trim();
      var icon = (this.newChannelIcon || '').trim() || '💬';

      if (!name) { this.toast('请输入频道名称', 'warning'); return; }

      var self = this;
      if (window.MNL && window.MNL.community && window.MNL.community.createChannelJson) {
        window.MNL.community.createChannelJson(name, desc, icon).then(function(data) {
          if (data && data.success) {
            self.showCreateChannelDialog = false;
            self.toast('频道创建成功！', 'success');
            self.loadChannels();
          } else {
            self.toast('频道创建失败，请稍后重试。', 'warning');
          }
        })["catch"](function(e) {
          self.toast('频道创建失败，请稍后重试。', 'warning');
        });
      } else {
        self.toast('服务不可用，请稍后重试', 'warning');
      }
    },

    /* ====== 导航 ====== */
    goBackToChannels: function() {
      this.selectedChannel = null;
      this.posts = [];
      this.showNewPostForm = false;
      this.currentView = 'channels';
    },

    goBackToPosts: function() {
      this.selectedPost = null;
      this.commentInput = '';
      this.currentView = 'posts';
    }
  },
  mounted: function() {
    var self = this;
    // 获取登录状态和用户信息
    if (window.MNL && window.MNL.community && window.MNL.community.getUserInfoJson) {
      window.MNL.community.getUserInfoJson().then(function(info) {
        self.communityUser = info || { isLoggedIn: false };
      })["catch"](function() {
        self.communityUser = { isLoggedIn: false };
      });
    } else {
      self.communityUser = { isLoggedIn: false };
    }
    this.loadChannels();
  }
};
