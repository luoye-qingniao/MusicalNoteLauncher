// ====== 个人主页 ProfilePage (pf-) ======
const ProfilePage = {
  template: `
    <div class="page-content profile-page">
      <!-- Panel 1: 卡片选择入口 -->
      <div class="pf-panel" v-show="currentPanel === 'select'">
        <div class="pf-panel-title">个人主页</div>
        <div class="pf-select-grid">
          <div class="pf-select-card" @click="currentPanel = 'qingniao'">
            <div class="pf-select-icon">🔷</div>
            <div class="pf-select-name">青鸟账号</div>
            <div class="pf-select-desc">个人资料、游戏统计与快捷操作</div>
          </div>
          <div class="pf-select-card" @click="currentPanel = 'gameaccount'">
            <div class="pf-select-icon">🎮</div>
            <div class="pf-select-name">游戏账号</div>
            <div class="pf-select-desc">管理离线/微软/外置登录账号</div>
          </div>
        </div>
      </div>

      <!-- Panel 2: 青鸟账号详情 -->
      <div class="pf-panel" v-show="currentPanel === 'qingniao'">
        <div class="pf-panel-header">
          <button class="pf-btn-back" @click="currentPanel = 'select'">← 返回</button>
          <span class="pf-subtitle">青鸟账号</span>
        </div>
        <div class="pf-qingniao-body">
          <div class="pf-qingniao-left">
            <div v-if="isQingniaoLoggedIn">
              <div class="pf-qn-avatar-wrap">
                <div class="pf-qn-avatar" @click="changeAvatar">
                  <div class="pf-qn-avatar-inner">
                    <img v-show="qingniaoAvatar" :src="qingniaoAvatar" alt="" />
                    <span class="pf-qn-avatar-emoji" v-show="!qingniaoAvatar">🐦</span>
                  </div>
                  <div class="pf-qn-avatar-overlay">
                    <span>🖼</span>
                    <span style="font-size:11px;margin-top:4px">更换头像</span>
                  </div>
                </div>
              </div>
              <div class="pf-qn-name-row">
                <span class="pf-qn-display-name">{{ qingniaoName }}</span>
                <button class="pf-qn-edit-btn" title="修改青鸟名字" @click="editQingniaoName">✎</button>
              </div>
              <div class="pf-qn-username-row"><span class="pf-qn-label">游戏账号：</span><span class="pf-qn-value">{{ username }}</span></div>
              <div class="pf-qn-login-mode">{{ loginMode }}</div>
              <div class="pf-qn-id-card">
                <div class="pf-qn-id-label">青鸟ID</div>
                <div class="pf-qn-id-value">{{ qingniaoId }}</div>
              </div>
              <div class="pf-qn-stats">
                <div class="pf-qn-stat">
                  <div class="pf-qn-stat-icon">🕐</div>
                  <div class="pf-qn-stat-num">{{ playTime }}</div>
                  <div class="pf-qn-stat-label">游戏时长</div>
                </div>
                <div class="pf-qn-stat">
                  <div class="pf-qn-stat-icon">🎮</div>
                  <div class="pf-qn-stat-num">{{ launchCount }}</div>
                  <div class="pf-qn-stat-label">启动次数</div>
                </div>
                <div class="pf-qn-stat">
                  <div class="pf-qn-stat-icon">⚙</div>
                  <div class="pf-qn-stat-num">{{ modCount }}</div>
                  <div class="pf-qn-stat-label">模组数</div>
                </div>
              </div>
              <button class="pf-btn-primary pf-btn-full" @click="editProfile">✏️ 编辑个人资料</button>
              <button class="pf-btn-secondary pf-btn-full" @click="switchAccount">🔄 切换账号</button>
              <div class="pf-qn-auth-actions" v-show="isQingniaoLoggedIn">
                <div class="pf-qn-auth-title">青鸟认证</div>
                <div class="pf-qn-auth-server">{{ qingniaoServer || '未设置' }}</div>
                <div class="pf-qn-auth-btns">
                  <button class="pf-btn-orange" @click="refreshQingniaoLogin">🐦 刷新登录</button>
                  <button class="pf-btn-gray" @click="qingniaoLogout">登出</button>
                </div>
              </div>
            </div>
            <div v-else>
              <div class="pf-qn-avatar-wrap">
                <div class="pf-qn-avatar pf-qn-avatar-default"><span class="pf-qn-avatar-emoji">🐦</span></div>
              </div>
              <div class="pf-qn-not-logged-title">未登录青鸟账号</div>
              <div class="pf-qn-not-logged-desc">登录后可查看个人资料、游戏统计等信息</div>
              <button class="pf-btn-orange pf-btn-wide" @click="qingniaoLogin">🐦 登录青鸟账号</button>
            </div>
          </div>
          <div class="pf-qingniao-right">
            <div class="pf-card">
              <div class="pf-card-title">最近游戏</div>
              <div class="pf-card-empty">{{ recentGame || '暂无游戏记录' }}</div>
            </div>
            <div class="pf-card">
              <div class="pf-card-title">游戏统计详情</div>
              <div class="pf-stats-detail-grid">
                <div class="pf-stats-detail-item">
                  <div class="pf-stats-detail-label">最喜欢的版本</div>
                  <div class="pf-stats-detail-value">{{ favoriteVersion || '尚未设置' }}</div>
                </div>
                <div class="pf-stats-detail-item">
                  <div class="pf-stats-detail-label">常用模组数量</div>
                  <div class="pf-stats-detail-value">{{ modCount }} 个</div>
                </div>
                <div class="pf-stats-detail-item">
                  <div class="pf-stats-detail-label">累计游戏时间</div>
                  <div class="pf-stats-detail-value">{{ totalPlayTime || '0 小时' }}</div>
                </div>
                <div class="pf-stats-detail-item">
                  <div class="pf-stats-detail-label">最近登录</div>
                  <div class="pf-stats-detail-value">{{ lastLogin || '今天' }}</div>
                </div>
              </div>
            </div>
            <div class="pf-card">
              <div class="pf-card-title">快捷操作</div>
              <div class="pf-quick-actions-row">
                <button class="pf-btn-quick pf-btn-quick-blue" @click="quickStart">🎮 快速开始</button>
                <button class="pf-btn-quick pf-btn-quick-green" @click="browseMods">⚙ 浏览模组</button>
                <button class="pf-btn-quick pf-btn-quick-red" @click="gameSettings">⚙️ 游戏设置</button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Panel 3: 游戏账号详情 -->
      <div class="pf-panel" v-show="currentPanel === 'gameaccount'">
        <div class="pf-panel-header">
          <button class="pf-btn-back" @click="currentPanel = 'select'">← 返回</button>
          <span class="pf-subtitle">游戏账号</span>
        </div>
        <div class="pf-game-account-body">
          <div class="pf-ga-left">
            <div class="pf-ga-section-header">添加账号</div>
            <div class="pf-ga-add-btns">
              <button class="pf-ga-add-btn pf-ga-add-green" @click="startAddOffline"><span class="pf-ga-add-icon">⬡</span><span>离线登录</span></button>
              <button class="pf-ga-add-btn pf-ga-add-blue" @click="addMicrosoftAccount"><span class="pf-ga-add-icon">⊞</span><span>微软登录</span></button>
              <button class="pf-ga-add-btn pf-ga-add-purple" @click="addAuthlibAccount"><span class="pf-ga-add-icon">🌐</span><span>外置登录</span></button>
            </div>
          </div>
          <div class="pf-ga-right">
            <div class="pf-ga-section-header">已有账号</div>
            <div class="pf-ga-list-wrap">
              <div class="pf-ga-list" v-if="accounts.length > 0">
                <div class="pf-ga-item" v-for="(acc, idx) in accounts" :key="idx" @click="selectAccount(idx)">
                  <div class="pf-ga-item-avatar">
                    <img v-if="acc.headImage" :src="acc.headImage" alt="" />
                    <span v-else class="pf-ga-item-avatar-emoji">{{ acc.avatarEmoji || '👤' }}</span>
                  </div>
                  <div class="pf-ga-item-info">
                    <span class="pf-ga-item-name">{{ acc.name }}</span>
                    <span class="pf-ga-item-type" :class="typeClass(acc.type)">{{ typeLabel(acc.type) }}</span>
                  </div>
                  <div class="pf-ga-item-actions">
                    <div class="pf-ga-item-dot" :class="{ selected: acc.selected }"></div>
                    <div class="pf-ga-item-sep"></div>
                    <button class="pf-ga-item-action" @click.stop="refreshAccount(idx)" title="刷新账号">🔄</button>
                    <button class="pf-ga-item-action" @click.stop="skinAccount(idx)" title="上传皮肤">🎨</button>
                    <button class="pf-ga-item-action" @click.stop="copyUuid(idx)" title="复制 UUID">📋</button>
                    <div class="pf-ga-item-sep"></div>
                    <button class="pf-ga-item-action delete" @click.stop="deleteAccount(idx)" title="删除账号">✕</button>
                  </div>
                </div>
              </div>
              <div class="pf-ga-empty" v-else>暂无账号，请添加</div>
            </div>
          </div>
        </div>
      </div>

      <!-- 内联对话框（替代 prompt/confirm） -->
      <div class="modal-overlay" v-if="dialog.show" @click.self="hideDialog">
        <div class="pf-dialog">
          <div class="pf-dialog-title">{{ dialog.title }}</div>
          <div class="pf-dialog-msg" v-if="dialog.message">{{ dialog.message }}</div>
          <input v-if="dialog.mode === 'input'" class="pf-dialog-input" v-model="dialog.inputValue" :placeholder="dialog.placeholder" @keydown.enter="dialogConfirm" autofocus />
          <textarea v-if="dialog.mode === 'textarea'" class="pf-dialog-input pf-dialog-textarea" v-model="dialog.inputValue" :placeholder="dialog.placeholder" rows="3"></textarea>
          <div class="pf-dialog-warning" v-if="dialog.warning">{{ dialog.warning }}</div>
          <div class="pf-dialog-btns">
            <button class="pf-btn-gray pf-dialog-btn" @click="hideDialog" v-if="dialog.mode !== 'alert'">取消</button>
            <button class="pf-dialog-btn" :class="dialog.confirmClass || 'pf-btn-primary'" @click="dialogConfirm">{{ dialog.confirmText || '确定' }}</button>
          </div>
        </div>
      </div>

      <!-- 青鸟登录弹窗（Web 内联，无 WPF 弹窗） -->
      <div class="modal-overlay" v-if="loginModalShow" @click.self="cancelLogin">
        <div class="pf-dialog pf-login-dialog">
          <div class="pf-dialog-title">🐦 青鸟账号登录</div>
          <div class="pf-dialog-msg">请使用邮箱和密码登录青鸟账号</div>
          <input class="pf-dialog-input" v-model="loginEmail" placeholder="输入邮箱" @keydown.enter="doQingniaoLogin" autofocus :disabled="loginLoading" />
          <input class="pf-dialog-input" v-model="loginPassword" type="password" placeholder="输入密码" style="margin-top:10px" @keydown.enter="doQingniaoLogin" :disabled="loginLoading" />
          <div class="pf-dialog-warning" v-if="loginError" style="text-align:center">{{ loginError }}</div>
          <div class="pf-dialog-btns">
            <button class="pf-btn-gray pf-dialog-btn" @click="cancelLogin" :disabled="loginLoading">取消</button>
            <button class="pf-btn-orange pf-dialog-btn" @click="doQingniaoLogin" :disabled="loginLoading">{{ loginLoading ? '登录中...' : '登  录' }}</button>
          </div>
        </div>
      </div>

      <!-- 皮肤编辑弹窗（3D 预览 + skinview3d） -->
      <div class="modal-overlay" v-if="skinDialog.show" @click.self="skinDialogCancel">
        <div class="pf-dialog pf-skin-3d-dialog">
          <div class="pf-dialog-title">🎨 皮肤编辑 - {{ skinDialog.accountName }}</div>
          <div class="pf-skin-3d-body">
            <!-- 左侧：3D 预览 -->
            <div class="pf-skin-3d-left">
              <div id="skin3d-container" class="pf-skin-3d-view"></div>
              <div class="pf-skin-3d-bg-row">
                <span class="pf-skin-bg-label">背景</span>
                <span class="pf-skin-bg-dot" :class="{ active: skinDialog.bgColor === '#2D2D2D' }" style="background:#2D2D2D" @click="skin3dSetBg('#2D2D2D')"></span>
                <span class="pf-skin-bg-dot" :class="{ active: skinDialog.bgColor === '#808080' }" style="background:#808080" @click="skin3dSetBg('#808080')"></span>
                <span class="pf-skin-bg-dot" :class="{ active: skinDialog.bgColor === '#F0F0F0' }" style="background:#F0F0F0" @click="skin3dSetBg('#F0F0F0')"></span>
                <span class="pf-skin-bg-dot" :class="{ active: skinDialog.bgColor === '#00FF00' }" style="background:#00FF00" @click="skin3dSetBg('#00FF00')"></span>
                <span class="pf-skin-bg-dot" :class="{ active: skinDialog.bgColor === '#4488FF' }" style="background:#4488FF" @click="skin3dSetBg('#4488FF')"></span>
              </div>
            </div>
            <!-- 右侧：操作面板 -->
            <div class="pf-skin-3d-right">
              <div class="pf-skin-3d-section-title">皮肤源</div>
              <label class="pf-skin-3d-radio" :class="{ active: !skinDialog.isDefault }">
                <input type="radio" v-model="skinDialog.isDefault" :value="false" @change="skin3dOnSourceChange" />
                <span>自定义皮肤</span>
              </label>
              <label class="pf-skin-3d-radio" :class="{ active: skinDialog.isDefault }">
                <input type="radio" v-model="skinDialog.isDefault" :value="true" @change="skin3dOnSourceChange" />
                <span>默认皮肤</span>
              </label>
              <div class="pf-skin-3d-section-title" style="margin-top:14px">模型</div>
              <label class="pf-skin-3d-radio" :class="{ active: !skinDialog.isSlim }">
                <input type="radio" v-model="skinDialog.isSlim" :value="false" @change="skin3dOnModelChange" />
                <span>👦 Steve</span>
              </label>
              <label class="pf-skin-3d-radio" :class="{ active: skinDialog.isSlim }">
                <input type="radio" v-model="skinDialog.isSlim" :value="true" @change="skin3dOnModelChange" />
                <span>👧 Alex</span>
              </label>
              <label class="pf-btn-skin-upload pf-skin-3d-upload" v-show="!skinDialog.isDefault">
                <input type="file" accept="image/png" @change="skin3dFileChange" style="display:none" />
                <span>📁 选择皮肤文件</span>
              </label>
              <div class="pf-skin-3d-info" v-if="skinDialog.fileName">{{ skinDialog.fileName }}</div>
            </div>
          </div>
          <div class="pf-dialog-btns">
            <button class="pf-btn-gray pf-dialog-btn" @click="skinDialogCancel">取消</button>
            <button class="pf-btn-primary pf-dialog-btn" @click="skinDialogConfirm" :disabled="skinDialog.saving">{{ skinDialog.saving ? '保存中...' : '完成' }}</button>
          </div>
        </div>
      </div>

      <!-- 微软正版登录弹窗（Device Code Flow，无需粘贴 URL） -->
      <div class="modal-overlay" v-if="msAuth.show" @click.self="cancelMsAuth">
        <div class="pf-dialog pf-msauth-dialog">
          <div class="pf-dialog-title" v-if="msAuth.status === 'code'">⊞ 微软正版登录</div>
          <div class="pf-dialog-title" v-else-if="msAuth.status === 'success'">⊞ 登录成功</div>
          <div class="pf-dialog-title" v-else-if="msAuth.status === 'error'">⊞ 登录失败</div>
          <div class="pf-dialog-title" v-else>⊞ 正在验证...</div>

          <!-- 显示验证码 + 等待提示 -->
          <div class="pf-msauth-code-wrap" v-if="msAuth.status === 'code' || msAuth.status === 'waiting'">
            <div class="pf-msauth-code-label">请在浏览器中输入以下验证码</div>
            <div class="pf-msauth-code" :class="{ faded: msAuth.status === 'waiting' }">{{ msAuth.userCode }}</div>
            <div class="pf-msauth-code-hint">
              <div v-if="msAuth.status === 'code'">浏览器已自动打开 microsoft.com/link</div>
              <div v-else class="pf-msauth-waiting">
                <div class="pf-msauth-spinner"></div>
                <span>正在等待你在浏览器中完成验证...</span>
              </div>
            </div>
          </div>
          <div class="pf-msauth-success" v-else-if="msAuth.status === 'success'">
            <div class="pf-msauth-success-icon">✓</div>
            <div>登录成功！正在刷新账号列表...</div>
          </div>
          <div class="pf-dialog-warning" v-if="msAuth.error" style="text-align:center">{{ msAuth.error }}</div>
          <div class="pf-msauth-actions" v-if="msAuth.status === 'code'">
            <button class="pf-btn-gray pf-btn-sm" @click="retryMsBrowser">🔄 重新打开浏览器</button>
          </div>
          <div class="pf-dialog-btns">
            <button class="pf-btn-gray pf-dialog-btn" @click="cancelMsAuth" v-if="msAuth.status === 'code' || msAuth.status === 'waiting'">取消登录</button>
            <button class="pf-btn-primary pf-dialog-btn" @click="cancelMsAuth" v-if="msAuth.status === 'error' || msAuth.status === 'success'">完成</button>
          </div>
        </div>
      </div>
    </div>
  `,
  data: function() {
    return {
      currentPanel: 'select',
      serverOrigin: window.location.origin,
      profileLoaded: false,
      username: '',
      qingniaoName: '',
      qingniaoId: '--',
      qingniaoAvatar: '',
      qingniaoServer: '',
      loginMode: '',
      isQingniaoLoggedIn: false,
      /// 查看其他用户主页时的目标用户 ID
      viewingUserId: '',
      playTime: 0,
      launchCount: 0,
      modCount: 0,
      favoriteVersion: '',
      totalPlayTime: '',
      lastLogin: '',
      recentGame: '',
      accounts: [],
      dialog: {
        show: false,
        mode: 'input',   // 'input' | 'confirm' | 'alert' | 'textarea'
        title: '',
        message: '',
        placeholder: '',
        inputValue: '',
        warning: '',
        confirmText: '确定',
        confirmClass: 'pf-btn-primary',
        callback: null,
      },
      // 青鸟登录弹窗
      loginModalShow: false,
      loginEmail: '',
      loginPassword: '',
      loginLoading: false,
      loginError: '',
      // 皮肤编辑弹窗
      skinDialog: {
        show: false,
        accountName: '',
        accountIdx: -1,
        uuid: '',
        previewUrl: '',
        fileName: '',
        base64Data: '',
        isDefault: false,
        isSlim: false,
        bgColor: '#2D2D2D',
        saving: false,
      },
      // 微软登录弹窗（Device Code Flow）
      msAuth: {
        show: false,
        userCode: '',
        verificationUri: '',
        message: '',
        submitting: false,
        error: '',
        status: 'code', // code | waiting | success | error
      },
      // 防止微软登录按钮重复点击
      msAuthLocked: false,
    };
  },
  methods: {
    toast: function(msg, type) {
      if (window.showToast) { window.showToast(msg, type); }
      else { console.log('[Profile Toast]', type, msg); }
    },

    // ===== 内联对话框 =====
    showDialog: function(opts) {
      this.dialog.show = true;
      this.dialog.mode = opts.mode || 'input';
      this.dialog.title = opts.title || '';
      this.dialog.message = opts.message || '';
      this.dialog.placeholder = opts.placeholder || '';
      this.dialog.inputValue = opts.defaultValue || '';
      this.dialog.warning = '';
      this.dialog.confirmText = opts.confirmText || '确定';
      this.dialog.confirmClass = opts.confirmClass || 'pf-btn-primary';
      this.dialog.callback = opts.callback || null;
    },
    hideDialog: function() {
      this.dialog.show = false;
      this.dialog.callback = null;
    },
    dialogConfirm: function() {
      var cb = this.dialog.callback;
      var val = this.dialog.inputValue;
      this.hideDialog();
      if (cb) { cb(val); }
    },

    // ===== 数据加载 =====
    loadProfile: function() {
      var self = this;
      if (!window.MNL || !window.MNL.profile || !window.MNL.profile.getProfileJson) {
        console.warn('[Profile] Bridge 不可用，使用默认数据');
        self.profileLoaded = true;
        return;
      }
      window.MNL.profile.getProfileJson().then(function(p) {
        self.profileLoaded = true;
        if (!p || Object.keys(p).length === 0) {
          console.warn('[Profile] 后端返回空数据');
          return;
        }
        self.username = p.username || self.username;
        self.qingniaoName = p.qingniaoName || self.username || self.qingniaoName;
        self.qingniaoId = p.qingniaoId || '--';
        self.qingniaoAvatar = p.qingniaoAvatar || '';
        self.qingniaoServer = p.qingniaoServer || '';
        // 通知侧边栏刷新（含头像）
        window.dispatchEvent(new CustomEvent('mnl:user-update', {
          detail: {
            username: p.qingniaoName || p.username,
            mode: p.isQingniaoLoggedIn ? '青鸟模式' : (p.loginMode || '离线模式'),
            avatarUrl: p.qingniaoAvatar || ''
          }
        }));
        // 使用后端判定的登录状态
        self.isQingniaoLoggedIn = !!p.isQingniaoLoggedIn;
        self.loginMode = p.loginMode || (p.isOffline ? '离线模式' : '正版模式');
        self.playTime = p.playTime || 0;
        self.launchCount = p.launchCount || 0;
        self.modCount = p.modCount || 0;
        self.favoriteVersion = p.favoriteVersion || '尚未设置';
        self.totalPlayTime = p.totalPlayTime || '0 小时';
        self.lastLogin = p.lastLogin || '今天';
        self.recentGame = p.recentGame || '';
        self.accounts = (p.accounts || []).map(function(a) { return Object.assign({}, a); });
        if (p.currentAccount) {
          self.accounts.forEach(function(a) { a.selected = (a.name === p.currentAccount.name); });
        }
      })["catch"](function(err) {
        self.profileLoaded = true;
        console.warn('[Profile] loadProfile 失败:', err);
      });
    },

    typeLabel: function(t) {
      if (t === 'offline') return '离线';
      if (t === 'microsoft' || t === 'Ms') return '微软';
      if (t === 'authlib' || t === 'AuthlibInjector' || t === 'Auth') return '外置';
      if (t === 'qingniao' || t === 'QingNiao') return '青鸟';
      return t || '未知';
    },

    typeClass: function(t) {
      if (t === 'offline') return 'offline';
      if (t === 'microsoft' || t === 'Ms') return 'microsoft';
      if (t === 'authlib' || t === 'AuthlibInjector' || t === 'Auth') return 'authlib';
      if (t === 'qingniao' || t === 'QingNiao') return 'qingniao';
      return '';
    },

    // ===== 青鸟账号操作 =====
    editQingniaoName: function() {
      var self = this;
      this.showDialog({
        mode: 'input',
        title: '修改青鸟名字',
        message: '名称长度应在2-24个字符之间',
        placeholder: '输入新的显示名称',
        defaultValue: this.qingniaoName,
        confirmText: '保存',
        callback: function(val) {
          if (!val || !val.trim()) return;
          var trimmed = val.trim();
          if (trimmed.length < 2 || trimmed.length > 24) {
            self.toast('名字长度应在2-24个字符之间', 'warning');
            return;
          }
          // 同步到后端
          if (window.MNL && window.MNL.profile && window.MNL.profile.setQingniaoNameJson) {
            window.MNL.profile.setQingniaoNameJson(trimmed).then(function(r) {
              if (r && r.success) {
                self.qingniaoName = trimmed;
                self.toast('青鸟名字已更新', 'success');
              } else {
                self.toast('保存失败: ' + ((r && r.error) || '未知错误'), 'error');
              }
            })["catch"](function(e) {
              self.toast('保存失败: ' + (e.message || '未知错误'), 'error');
            });
          } else {
            // 无后端时本地更新
            self.qingniaoName = trimmed;
            self.toast('青鸟名字已更新', 'success');
          }
        }
      });
    },

    // ===== 头像切换 =====
    changeAvatar: function() {
      if (!window.MNL || !window.MNL.profile || !window.MNL.profile.uploadQingniaoAvatarJson) {
        this.toast('头像上传功能不可用', 'warning');
        return;
      }
      var self = this;
      window.MNL.profile.uploadQingniaoAvatarJson().then(function(r) {
        if (r && r.success) {
          self.qingniaoAvatar = r.avatar;
          self.toast('头像已更新', 'success');
        } else if (r && r.cancelled) {
          // 用户取消了选择
        } else {
          self.toast('头像更新失败: ' + ((r && r.error) || '未知错误'), 'error');
        }
      })["catch"](function(e) {
        self.toast('头像更新失败: ' + (e.message || '未知错误'), 'error');
      });
    },

    // ===== 青鸟登录弹窗 =====
    openLoginModal: function() {
      this.loginModalShow = true;
      this.loginEmail = '';
      this.loginPassword = '';
      this.loginError = '';
      this.loginLoading = false;
      // 预填缓存的邮箱
      var self = this;
      if (window.MNL && window.MNL.profile && window.MNL.profile.getProfileJson) {
        window.MNL.profile.getProfileJson().then(function(p) {
          self.loginEmail = p.qingniaoEmail || '';
        })["catch"](function(){});
      }
    },
    cancelLogin: function() {
      if (this.loginLoading) return;
      this.loginModalShow = false;
      this.loginError = '';
    },
    doQingniaoLogin: function() {
      if (this.loginLoading) return;
      var email = this.loginEmail.trim();
      var password = this.loginPassword;
      if (!email) { this.loginError = '请输入邮箱'; return; }
      if (!password) { this.loginError = '请输入密码'; return; }
      if (!window.MNL || !window.MNL.profile || !window.MNL.profile.qingniaoAuthJson) {
        this.loginError = '后端服务不可用';
        return;
      }
      this.loginError = '';
      this.loginLoading = true;
      var self = this;
      window.MNL.profile.qingniaoAuthJson(email, password).then(function(r) {
        self.loginLoading = false;
        if (r && r.success) {
          self.loginModalShow = false;
          self.loginEmail = '';
          self.loginPassword = '';
          // 立即更新本地数据（不等待 loadProfile）
          if (r.userName) self.qingniaoName = r.userName;
          if (r.uuid) self.qingniaoId = r.uuid;
          self.isQingniaoLoggedIn = true;
          self.loginMode = '青鸟模式';
          self.toast('青鸟账号 ' + (r.userName || email) + ' 登录成功', 'success');
          // 通知侧边栏更新用户名和模式
          window.dispatchEvent(new CustomEvent('mnl:user-update', {
            detail: {
              username: r.userName || email,
              mode: '青鸟模式'
            }
          }));
          // 再从后端同步完整数据（含头像、账号列表等）
          self.loadProfile();
        } else {
          self.loginError = (r && r.error) || '登录失败';
        }
      })["catch"](function(e) {
        self.loginLoading = false;
        self.loginError = e.message || '网络错误';
      });
    },

    editProfile: function() {
      this.showDialog({
        mode: 'alert',
        title: '编辑个人资料',
        message: '个人资料编辑功能正在开发中，敬请期待！\n\n当前可修改的信息：\n• 青鸟显示名称（点击 ✎ 按钮）\n• 头像（通过青鸟服务器自动同步）',
        confirmText: '知道了',
      });
    },
    switchAccount: function() { this.openLoginModal(); },

    qingniaoLogin: function() { this.openLoginModal(); },
    refreshQingniaoLogin: function() { this.openLoginModal(); },

    qingniaoLogout: function() {
      var self = this;
      this.showDialog({
        mode: 'confirm',
        title: '登出青鸟账号',
        message: '确定要登出青鸟账号吗？登出后需重新登录才能使用。',
        confirmText: '确定登出',
        confirmClass: 'pf-btn-danger',
        callback: function() {
          if (window.MNL && window.MNL.profile && window.MNL.profile.logout) {
            window.MNL.profile.logout();
          }
          self.isQingniaoLoggedIn = false;
          self.qingniaoId = '--';
          self.qingniaoName = '';
          self.qingniaoAvatar = '';
          self.toast('已登出青鸟账号', 'success');
        }
      });
    },

    quickStart: function() { this.toast('快速开始功能开发中...', 'info'); },
    browseMods: function() {
      this.toast('正在跳转到模组浏览...', 'info');
      if (window.navigateTo) { window.navigateTo('mods'); }
    },
    gameSettings: function() { this.toast('游戏设置功能开发中...', 'info'); },

    // ===== 游戏账号操作 =====
    startAddOffline: function() {
      var self = this;
      this.showDialog({
        mode: 'input',
        title: '创建离线账号',
        message: '玩家名称（3-16个字符）',
        placeholder: '输入玩家名称',
        defaultValue: 'Steve',
        confirmText: '创建',
        callback: function(val) {
          if (!val || !val.trim()) {
            self.toast('玩家名称不能为空', 'warning');
            return;
          }
          var trimmed = val.trim();
          if (trimmed.length < 3 || trimmed.length > 16) {
            self.toast('用户名长度需要在3-16个字符之间', 'warning');
            return;
          }
          // 检查是否已存在同名账号
          if (self.accounts.some(function(a) { return a.type === 'offline' && a.name.toLowerCase() === trimmed.toLowerCase(); })) {
            self.toast('已存在同名离线账号', 'warning');
            return;
          }
          if (!window.MNL || !window.MNL.profile || !window.MNL.profile.addOfflineAccountJson) {
            self.toast('后端服务不可用，请检查启动器状态', 'warning');
            return;
          }
          self.toast('正在添加离线账号 ' + trimmed + '...', 'info');
          window.MNL.profile.addOfflineAccountJson(trimmed).then(function(r) {
            if (r && r.success) {
              self.loadProfile();
              self.toast('离线账号 ' + trimmed + ' 已添加', 'success');
            } else {
              self.toast('添加失败: ' + ((r && r.error) || '未知错误'), 'error');
            }
          })["catch"](function(e) {
            self.toast('添加失败: ' + (e.message || '未知错误'), 'error');
          });
        }
      });
    },

    addMicrosoftAccount: function() {
      if (this.msAuthLocked) return;
      this.msAuthLocked = true;
      this.loginAccount('microsoft');
    },

    addAuthlibAccount: function() {
      this.showDialog({
        mode: 'alert',
        title: '外置登录',
        message: '外置登录功能正在开发中，敬请期待。',
        confirmText: '知道了',
        confirmClass: 'pf-btn-primary',
        callback: function() {}
      });
    },

    loginAccount: function(type) {
      var self = this;
      if (!window.MNL || !window.MNL.account || !window.MNL.account.login) {
        this.msAuthLocked = false;
        this.showDialog({
          mode: 'alert',
          title: '功能不可用',
          message: '登录功能不可用，请检查启动器状态',
          confirmText: '确定',
          callback: function() {}
        });
        return;
      }
      // 微软登录：C# 端会通过 ms-device-code 消息打开 web 对话框（Device Code Flow）
      if (type === 'microsoft') {
        this.toast('正在启动微软认证...', 'info');
      }
      window.MNL.account.login(type).then(function(r) {
        if (r && r.success) {
          if (type === 'microsoft') {
            // 不在这里显示 toast，由 ms-device-code 事件处理弹窗
          } else {
            self.toast('登录成功，正在刷新...', 'success');
          }
        }
        if (r && r.error) {
          self.toast(r.error, 'warning');
          self.msAuthLocked = false;
        }
        if (type !== 'microsoft') {
          self.loadProfile();
        }
      })["catch"](function(e) {
        self.toast('登录失败: ' + (e.message || '未知错误'), 'error');
        self.msAuthLocked = false;
      });
    },

    // ===== 微软正版登录 Web 对话框（Device Code Flow）=====
    openMsAuthDialog: function(userCode, verificationUri, message) {
      this.msAuth.show = true;
      this.msAuth.userCode = userCode || '';
      this.msAuth.verificationUri = verificationUri || '';
      this.msAuth.message = message || '请在浏览器中输入验证码完成登录';
      this.msAuth.submitting = false;
      this.msAuth.error = '';
      this.msAuth.status = 'code';
    },

    cancelMsAuth: function() {
      // 通知 C# 端停止后台轮询
      try {
        if (window.MNL && window.MNL.account && window.MNL.account.cancelMicrosoftAuth) {
          window.MNL.account.cancelMicrosoftAuth();
        }
      } catch (e) {}
      this.msAuth.show = false;
      this.msAuth.submitting = false;
      this.msAuthLocked = false;
    },

    retryMsBrowser: function() {
      if (this.msAuth.verificationUri && window.MNL && window.MNL.utils && window.MNL.utils.openUrl) {
        window.MNL.utils.openUrl(this.msAuth.verificationUri);
        this.toast('已重新打开浏览器', 'info');
      }
    },

    onMsLoginSuccess: function() {
      this.msAuth.status = 'success';
      this.msAuth.submitting = false;
      this.msAuthLocked = false;
      this.loadProfile();
    },

    onMsLoginError: function(error) {
      this.msAuth.status = 'error';
      this.msAuth.submitting = false;
      this.msAuth.error = error || '登录失败';
      this.msAuthLocked = false;
    },

    selectAccount: function(idx) {
      this.accounts.forEach(function(a, i) { a.selected = (i === idx); });
      var acc = this.accounts[idx];
      if (acc) { this.toast('已选择账号: ' + acc.name, 'info'); }
    },

    refreshAccount: function(idx) {
      var acc = this.accounts[idx];
      if (!acc) return;
      if (!window.MNL || !window.MNL.profile || !window.MNL.profile.refreshAccountHeadJson) {
        this.toast('刷新功能不可用', 'warning');
        return;
      }
      var self = this;
      this.toast('正在刷新账号: ' + acc.name + '...', 'info');
      window.MNL.profile.refreshAccountHeadJson(acc.uuid, acc.name).then(function(r) {
        if (r && r.success && r.headImage) {
          self.$set(self.accounts[idx], 'headImage', r.headImage);
          self.toast('账号 ' + acc.name + ' 已刷新', 'success');
        } else {
          self.loadProfile();
        }
      })["catch"](function() { self.loadProfile(); });
    },

    skinAccount: function(idx) {
      var acc = this.accounts[idx];
      if (!acc) return;
      this.skinDialog.show = true;
      this.skinDialog.accountName = acc.name;
      this.skinDialog.accountIdx = idx;
      this.skinDialog.uuid = acc.uuid;
      this.skinDialog.previewUrl = '';
      this.skinDialog.fileName = '';
      this.skinDialog.base64Data = '';
      this.skinDialog.isDefault = false;
      this.skinDialog.isSlim = false;
      this.skinDialog.bgColor = '#2D2D2D';
      this.skinDialog.saving = false;
      // 延迟初始化 3D 预览（WebView2 需要额外时间完成 DOM 布局 + GPU 管线初始化）
      var self = this;
      setTimeout(function() { self.skin3dInit(); }, 150);
    },

    // ===== 3D 皮肤预览 (skinview3d) =====
    _skin3dViewer: null,

    skin3dInit: function() {
      this.skin3dDestroy();
      var container = document.getElementById('skin3d-container');
      if (!container) return;
      // 检测 WebGL 可用性
      var testCanvas = document.createElement('canvas');
      var gl = testCanvas.getContext('webgl') || testCanvas.getContext('experimental-webgl');
      if (!gl) {
        console.error('[skin3d] WebGL 不可用，无法渲染3D预览');
        container.innerHTML = '<div style="display:flex;align-items:center;justify-content:center;height:100%;color:#999;font-size:13px">WebGL 不可用<br/>无法加载3D预览</div>';
        return;
      }
      // 清理旧 canvas
      container.innerHTML = '';
      var canvas = document.createElement('canvas');
      canvas.style.width = '100%';
      canvas.style.height = '100%';
      container.appendChild(canvas);
      var w = container.clientWidth || 300;
      var h = container.clientHeight || 300;
      try {
        var skinUrl = null;
        if (this.skinDialog.isDefault) {
          // 默认皮肤：用本地 Steve/Alex PNG
          skinUrl = this.serverOrigin + '/img/' + (this.skinDialog.isSlim ? 'skin_alex' : 'steve_skin') + '.png';
        } else if (this.skinDialog.base64Data) {
          skinUrl = this.skinDialog.base64Data;
        } else {
          // 已有皮肤文件
          var uuidSkin = this.serverOrigin + '/api/skin/' + this.skinDialog.uuid + '?_=' + Date.now();
          // 如果皮肤文件不存在则降级到默认皮肤
          skinUrl = uuidSkin;
        }
        this._skin3dViewer = new skinview3d.SkinViewer({
          canvas: canvas,
          width: w,
          height: h,
          skin: skinUrl,
          model: this.skinDialog.isSlim ? 'slim' : 'default'
        });
        this._skin3dViewer.animation = new skinview3d.IdleAnimation();
        this._skin3dViewer.animation.speed = 0.8;
        this._skin3dViewer.cameraLight.intensity = 1.2;
        this._skin3dViewer.globalLight.intensity = 2.5;
        this._skin3dViewer.fov = 30;
        this._skin3dViewer.zoom = 0.85;
        this._skin3dViewer.autoRotate = true;
        this._skin3dViewer.autoRotateSpeed = 0.5;
        this.skin3dApplyBg();
      } catch(e) {
        console.error('[skin3d] init error:', e);
        container.innerHTML = '<div style="display:flex;align-items:center;justify-content:center;height:100%;color:#d32f2f;font-size:13px">3D 初始化失败<br/>' + (e.message || '未知错误') + '</div>';
      }
    },

    skin3dOnSourceChange: function() {
      var self = this;
      if (this.skinDialog.isDefault) {
        this.skinDialog.base64Data = '';
        this.skinDialog.fileName = '';
        this.skinDialog.previewUrl = '';
      }
      setTimeout(function() { self.skin3dInit(); }, 50);
    },

    skin3dOnModelChange: function() {
      if (!this._skin3dViewer) return;
      var self = this;
      setTimeout(function() { self.skin3dInit(); }, 50);
    },

    skin3dDestroy: function() {
      if (this._skin3dViewer) {
        try { this._skin3dViewer.dispose(); } catch(e) {}
        this._skin3dViewer = null;
      }
      var container = document.getElementById('skin3d-container');
      if (container) container.innerHTML = '';
    },

    skin3dApplyBg: function() {
      if (!this._skin3dViewer) return;
      var hex = this.skinDialog.bgColor.replace('#', '');
      this._skin3dViewer.background = parseInt(hex, 16);
    },

    skin3dSetBg: function(color) {
      this.skinDialog.bgColor = color;
      this.skin3dApplyBg();
    },

    skin3dFileChange: function(e) {
      var file = e.target.files && e.target.files[0];
      if (!file) return;
      if (file.size > 1024 * 1024) {
        this.toast('文件不能超过 1MB', 'warning');
        return;
      }
      var self = this;
      var reader = new FileReader();
      reader.onload = function(ev) {
        self.skinDialog.previewUrl = ev.target.result;
        self.skinDialog.base64Data = ev.target.result;
        self.skinDialog.fileName = file.name;
        self.skinDialog.isDefault = false;
        // 直接加载到 3D 查看器
        if (self._skin3dViewer) {
          self._skin3dViewer.loadSkin(ev.target.result);
        }
      };
      reader.readAsDataURL(file);
    },

    skinDialogCancel: function() {
      this.skin3dDestroy();
      this.skinDialog.show = false;
    },

    skinDialogConfirm: function() {
      var self = this;
      if (!window.MNL || !window.MNL.profile || !window.MNL.profile.saveSkinBase64Json) {
        this.toast('皮肤保存功能不可用', 'warning');
        return;
      }
      var data = this.skinDialog.isDefault ? 'default' : (this.skinDialog.base64Data || '');
      this.skinDialog.saving = true;
      window.MNL.profile.saveSkinBase64Json(this.skinDialog.uuid, this.skinDialog.accountName, data, this.skinDialog.isSlim).then(function(r) {
        self.skinDialog.saving = false;
        self.skin3dDestroy();
        self.skinDialog.show = false;
        if (r && r.success && r.headImage) {
          self.accounts[self.skinDialog.accountIdx].headImage = r.headImage;
        } else if (!r || !r.success) {
          self.toast('保存失败: ' + ((r && r.error) || '未知错误'), 'error');
        }
      })["catch"](function(e) {
        self.skinDialog.saving = false;
        self.skin3dDestroy();
        self.skinDialog.show = false;
        self.toast('保存失败: ' + (e.message || '未知错误'), 'error');
      });
    },

    copyUuid: function(idx) {
      var acc = this.accounts[idx];
      if (!acc || !acc.uuid) {
        this.toast('该账号没有 UUID', 'warning');
        return;
      }
      var uuid = acc.uuid;
      // 尝试 navigator.clipboard（需安全上下文）
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(uuid).then(function() {
          window.showToast && window.showToast('UUID 已复制: ' + uuid.substring(0, 12) + '...', 'success');
        })["catch"](function() {
          // clipboard 失败，用 textarea 兜底
          fallbackCopy(uuid);
        });
      } else {
        fallbackCopy(uuid);
      }
      function fallbackCopy(text) {
        try {
          var ta = document.createElement('textarea');
          ta.value = text;
          ta.style.position = 'fixed';
          ta.style.left = '-9999px';
          document.body.appendChild(ta);
          ta.select();
          document.execCommand('copy');
          document.body.removeChild(ta);
          window.showToast && window.showToast('UUID 已复制', 'success');
        } catch(e) {
          window.showToast && window.showToast('复制失败，UUID: ' + text, 'warning');
        }
      }
    },

    deleteAccount: function(idx) {
      var acc = this.accounts[idx];
      if (!acc) return;
      var self = this;
      this.showDialog({
        mode: 'confirm',
        title: '删除账号',
        message: '确定删除账号 "' + acc.name + '" 吗？此操作不可恢复。',
        confirmText: '确认删除',
        confirmClass: 'pf-btn-danger',
        callback: function() {
          if (window.MNL && window.MNL.profile && window.MNL.profile.deleteAccountJson) {
            self.toast('正在删除...', 'info');
            window.MNL.profile.deleteAccountJson(acc.type, acc.name).then(function(r) {
              if (r && r.success) {
                self.accounts.splice(idx, 1);
                self.toast('账号 ' + acc.name + ' 已删除', 'success');
              } else {
                self.toast('删除失败: ' + ((r && r.error) || '未知错误'), 'error');
              }
            })["catch"](function(e) {
              self.toast('删除失败: ' + (e.message || '未知错误'), 'error');
            });
          } else {
            // 无后端时直接从列表移除
            self.accounts.splice(idx, 1);
            self.toast('账号 ' + acc.name + ' 已删除', 'success');
          }
        }
      });
    },
  },
  watch: {
    currentPanel: function(val) {
      if (val === 'qingniao' || val === 'gameaccount') {
        this.loadProfile();
      }
    }
  },
  mounted: function() {
    // 检查是否从社区帖子跳转查看其他用户主页
    if (this.$root && this.$root.pageParams && this.$root.pageParams.userId) {
      this.viewingUserId = this.$root.pageParams.userId;
      this.currentPanel = 'qingniao';
      this.$root.pageParams = {};
    } else {
      this.loadProfile();
    }
    var self = this;
    // 监听页面导航事件（处理 v-show 下重复导航到同一页面）
    this._onPageNavigate = function(e) {
      var d = e.detail || {};
      if (d.page === 'profile' && d.params && d.params.userId) {
        self.viewingUserId = d.params.userId;
        self.currentPanel = 'qingniao';
      }
    };
    window.addEventListener('mnl:page-navigate', this._onPageNavigate);
    // 监听外部触发的账号列表刷新（如微软登录成功后）
    this._onProfileReload = function() { self.loadProfile(); };
    window.addEventListener('mnl:profile-reload-accounts', this._onProfileReload);
    // 监听微软 Device Code：显示验证码弹窗（保持验证码可见直到成功/失败）
    this._onMsDeviceCode = function(e) {
      var detail = e.detail || {};
      self.openMsAuthDialog(detail.userCode, detail.verificationUri, detail.message || '请在浏览器中输入验证码完成登录');
    };
    window.addEventListener('mnl:ms-device-code', this._onMsDeviceCode);
    // 监听微软登录成功
    this._onMsLoginSuccess = function() { self.onMsLoginSuccess(); };
    window.addEventListener('mnl:ms-login-success', this._onMsLoginSuccess);
    // 监听微软登录失败
    this._onMsLoginError = function(e) {
      var detail = e.detail || {};
      self.onMsLoginError(detail.error || '登录失败');
    };
    window.addEventListener('mnl:ms-login-error', this._onMsLoginError);
  },
  beforeUnmount: function() {
    if (this._onPageNavigate) {
      window.removeEventListener('mnl:page-navigate', this._onPageNavigate);
    }
    if (this._onProfileReload) {
      window.removeEventListener('mnl:profile-reload-accounts', this._onProfileReload);
    }
    if (this._onMsDeviceCode) {
      window.removeEventListener('mnl:ms-device-code', this._onMsDeviceCode);
    }
    if (this._onMsLoginSuccess) {
      window.removeEventListener('mnl:ms-login-success', this._onMsLoginSuccess);
    }
    if (this._onMsLoginError) {
      window.removeEventListener('mnl:ms-login-error', this._onMsLoginError);
    }
  }
};
