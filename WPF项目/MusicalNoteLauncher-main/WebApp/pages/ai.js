// ====== AI 助手 AiPage (ai-) ======
const AiPage = {
  template: `
    <div class="page-content ai-page">
      <div class="ai-container" style="display:flex;flex-direction:column;height:100%;overflow:hidden;">

        <div class="ai-header">
          <div class="ai-avatar"><img src="img/yinlian.png" alt="音涟" class="ai-avatar-img"></div>
          <div class="ai-header-info">
            <div class="ai-header-name">音涟</div>
            <div class="ai-status-row">
              <span class="ai-status-dot" :class="{ configured: apiConfigured }"></span>
              <span class="ai-status" :style="{ color: apiConfigured ? '#4CAF50' : '#FF9800' }">{{ apiConfigured ? '已配置 · ' + modelName : '未配置 API' }}</span>
            </div>
          </div>
          <button class="ai-settings-btn" @click="openConfig" title="API 设置">⚙️</button>
        </div>

        <div class="ai-chat-area ai-messages" ref="chatArea" style="flex:1;overflow-y:auto;">

          <div class="ai-welcome" v-if="messages.length === 0">
            <div class="ai-welcome-card">
              <div class="ai-welcome-subtitle">音涟</div>
              <div class="ai-welcome-title">MNL 智能助手</div>
              <div class="ai-welcome-desc">我可以帮你解答 Minecraft 的各种问题，包括游戏攻略、红石教程、建筑技巧等。</div>
            </div>

            <div class="ai-api-prompt" v-if="!apiConfigured">
              <span class="ai-api-icon">🔑</span>
              <div class="ai-api-text">
                <strong>API 尚未配置</strong><br/>
                点击右上角齿轮按钮配置 API 以启用 AI 助手
              </div>
            </div>

            <div class="ai-quick-chips ai-quick-questions" style="margin-top:16px;">
              <button class="ai-chip ai-q-chip" v-for="q in quickQuestions" :key="q" @click="sendMsg(q)">{{ q }}</button>
            </div>
          </div>

          <div class="ai-msg" v-for="(m, i) in messages" :key="i" :class="m.role">
            <div class="ai-msg-avatar">
              <img v-if="m.role === 'user' && $root.userAvatarUrl" :src="$root.userAvatarUrl" class="ai-avatar-img">
              <span v-if="m.role === 'user' && !$root.userAvatarUrl">😎</span>
              <img v-if="m.role === 'ai'" src="img/yinlian.png" class="ai-avatar-img">
            </div>
            <div class="ai-msg-bubble ai-msg-content" v-html="formatMsg(m.text)"></div>
          </div>

        </div>

        <!-- AI 浏览器操控面板 -->
        <div class="ai-browse-panel" :class="{ active: browseVisible }">
          <div class="ai-browse-header">
            <div class="ai-browse-url-bar">
              <span class="ai-browse-icon">🌐</span>
              <span class="ai-browse-url">{{ browseUrl || 'about:blank' }}</span>
            </div>
            <div class="ai-browse-actions">
              <button class="ai-browse-btn" @click="browseRefresh" title="刷新">🔄</button>
              <button class="ai-browse-btn" @click="browseClose" title="关闭">✕</button>
            </div>
          </div>
          <div class="ai-browse-viewport" ref="browseViewport" @click="onBrowseViewportClick">
            <iframe ref="browseFrame" sandbox="allow-scripts allow-same-origin allow-forms allow-popups" class="ai-browse-frame"></iframe>
            <!-- 点击波纹效果 -->
            <div class="ai-browse-ripple" v-for="r in ripples" :key="r.id"
              :style="{ left: r.x + 'px', top: r.y + 'px' }" :class="{ done: r.done }"></div>
            <!-- 光标动画 -->
            <div class="ai-browse-cursor" v-if="cursorVisible"
              :style="{ left: cursorX + 'px', top: cursorY + 'px' }"></div>
          </div>
        </div>

        <div class="ai-input-area">
          <textarea class="ai-input" v-model="inputText" placeholder="输入你的问题..." rows="1"
            @keydown.enter="sendMsg(inputText)"
            @keydown.shift.enter="onShiftEnter"></textarea>
          <button class="ai-send-btn" @click="sendMsg(inputText)" :disabled="!inputText.trim() || loading">➤</button>
        </div>

        <div class="modal-overlay ai-modal" v-if="showConfig" @click.self="closeConfig">
          <div class="modal-dialog ai-modal-content" style="max-width:460px;">
            <div class="modal-header">
              <h3>🤖 AI 助手配置</h3>
              <button class="modal-close" @click="closeConfig">✕</button>
            </div>
            <div class="modal-body">
              <div class="ai-config-body">

                <div class="ai-config-field">
                  <label class="ai-config-label">
                    <span class="ai-config-label-icon">⚡</span>
                    <span>预设服务</span>
                  </label>
                  <div class="dropdown" @click.stop="showPresetDropdown = !showPresetDropdown">
                    <div class="dropdown-trigger ai-dropdown-trigger">
                      <span class="dt-text">
                        <span v-if="selectedPreset">
                          <span class="ai-preset-icon">{{ currentPreset?.icon || '🔌' }}</span>
                          {{ currentPreset?.name || '自定义' }}
                        </span>
                        <span v-else class="dt-text-muted">选择预设服务</span>
                      </span>
                      <span class="dt-arrow">▼</span>
                    </div>
                    <div v-if="showPresetDropdown" class="dropdown-menu ai-preset-menu">
                      <div v-for="p in presets" :key="p.id"
                        class="dropdown-item ai-preset-item" :class="{ selected: p.id === selectedPreset }"
                        @click.stop="selectPreset(p.id)">
                        <span class="ai-preset-icon">{{ p.icon }}</span>
                        <span class="ai-preset-name">{{ p.name }}</span>
                        <span class="ai-preset-tag" v-if="p.tag">{{ p.tag }}</span>
                      </div>
                      <div class="ai-preset-divider"></div>
                      <div class="dropdown-item ai-preset-item" :class="{ selected: !selectedPreset }"
                        @click.stop="selectPreset('')">
                        <span class="ai-preset-icon">✏️</span>
                        <span class="ai-preset-name">自定义</span>
                      </div>
                    </div>
                  </div>
                </div>

                <div class="ai-config-field">
                  <label class="ai-config-label">
                    <span class="ai-config-label-icon">🌐</span>
                    <span>接口地址</span>
                  </label>
                  <input class="input ai-config-input" v-model="configEndpoint" type="text" placeholder="https://api.openai.com/v1" />
                </div>

                <div class="ai-config-field">
                  <label class="ai-config-label">
                    <span class="ai-config-label-icon">🔑</span>
                    <span>API Key</span>
                  </label>
                  <input class="input ai-config-input" v-model="configKey" type="password" placeholder="sk-..." />
                </div>

                <div class="ai-config-field">
                  <label class="ai-config-label">
                    <span class="ai-config-label-icon">🧠</span>
                    <span>模型名称</span>
                  </label>
                  <div v-if="selectedPreset && currentPresetModels.length > 0" class="dropdown" @click.stop="showModelDropdown = !showModelDropdown">
                    <div class="dropdown-trigger ai-dropdown-trigger">
                      <span class="dt-text">{{ configModel || '选择模型' }}</span>
                      <span class="dt-arrow">▼</span>
                    </div>
                    <div v-if="showModelDropdown" class="dropdown-menu">
                      <div v-for="m in currentPresetModels" :key="m"
                        class="dropdown-item" :class="{ selected: m === configModel }"
                        @click.stop="configModel = m; showModelDropdown = false">
                        {{ m }}
                      </div>
                    </div>
                  </div>
                  <input v-else class="input ai-config-input" v-model="configModel" type="text" placeholder="gpt-3.5-turbo" />
                </div>

                <div class="ai-config-tip" v-if="selectedPreset && currentPreset?.tip">
                  <span class="ai-tip-icon">💡</span>
                  <span>{{ currentPreset.tip }}</span>
                </div>

              </div>
            </div>
            <div class="modal-footer">
              <button class="btn" @click="closeConfig">取消</button>
              <button class="btn" @click="testConfig">测试连接</button>
              <button class="btn btn-primary" @click="saveConfig">保存</button>
            </div>
          </div>
        </div>

      </div>
    </div>
  `,
  data() {
    return {
      inputText: '',
      messages: [],
      loading: false,
      toolStatus: '',
      apiConfigured: false,
      modelName: '',
      showConfig: false,
      configEndpoint: '',
      configKey: '',
      configModel: '',
      selectedPreset: '',
      showPresetDropdown: false,
      showModelDropdown: false,
      presets: [
        {
          id: 'openai',
          name: 'OpenAI',
          icon: '🟢',
          tag: '官方',
          category: 'cloud',
          endpoint: 'https://api.openai.com/v1',
          models: ['gpt-4o', 'gpt-4o-mini', 'gpt-4-turbo', 'gpt-4', 'gpt-3.5-turbo'],
          tip: '需翻墙访问，推荐使用国内代理或中转服务'
        },
        {
          id: 'deepseek',
          name: 'DeepSeek',
          icon: '🔵',
          tag: '推荐',
          category: 'cloud',
          endpoint: 'https://api.deepseek.com/v1',
          models: ['deepseek-v4-flash', 'deepseek-v4-pro'],
          tip: '深度求索官方，性价比高，支持长文本'
        },
        {
          id: 'siliconflow',
          name: '硅基流动',
          icon: '🔷',
          tag: '免费',
          category: 'cloud',
          endpoint: 'https://api.siliconflow.cn/v1',
          models: ['Qwen/Qwen2.5-7B-Instruct', 'Qwen/Qwen2.5-72B-Instruct', 'deepseek-ai/DeepSeek-V3', 'meta-llama/Llama-3.1-8B-Instruct'],
          tip: '国内聚合平台，有免费额度，模型选择丰富'
        },
        {
          id: 'aliyun',
          name: '阿里百炼',
          icon: '☁️',
          tag: '',
          category: 'cloud',
          endpoint: 'https://dashscope.aliyuncs.com/compatible-mode/v1',
          models: ['qwen-plus', 'qwen-turbo', 'qwen-long', 'qwen-max'],
          tip: '阿里云出品，稳定可靠，需开通百炼兼容模式'
        },
        {
          id: 'zhipu',
          name: '智谱AI',
          icon: '💎',
          tag: '',
          category: 'cloud',
          endpoint: 'https://open.bigmodel.cn/api/paas/v4',
          models: ['glm-4-plus', 'glm-4-air', 'glm-4-flash', 'glm-4-long', 'glm-3-turbo'],
          tip: '清华智谱，中文理解能力强'
        }
      ],
      quickQuestions: [
        '怎么搭建下界传送门？',
        '如何找到末地要塞？',
        '推荐新手模组',
        '红石基础教程'
      ],
      // 浏览器操控面板
      browseVisible: false,
      browseUrl: '',
      browseProxyUrl: '',
      ripples: [],
      _rippleId: 0,
      cursorVisible: false,
      cursorX: 0,
      cursorY: 0,
    };
  },
  methods: {
    formatMsg(text) {
      return text.replace(/\n/g, '<br>').replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    },
    onShiftEnter() {
      this.inputText += '\n';
    },
    async sendMsg(text) {
      if (!text || !text.trim() || this.loading) return;
      this.messages.push({ role: 'user', text: text.trim() });
      const sent = text.trim();
      this.inputText = '';
      this.loading = true;
      this.messages.push({ role: 'ai', text: '思考中...' });
      this.$nextTick(() => this.scrollToBottom());

      try {
        console.log('[AI] sendMsg — apiConfigured=', this.apiConfigured, 'configModel=', this.configModel);
        if (this.apiConfigured && window.MNL && window.MNL.ai && window.MNL.ai.chat) {
          console.log('[AI] 调用 MNL.ai.chat...');
          const reply = await window.MNL.ai.chat(sent, this.configModel);
          console.log('[AI] 收到回复:', reply?.substring(0, 100));
          this.messages.pop();
          this.messages.push({ role: 'ai', text: reply || '抱歉，没有收到有效回复。' });
        } else {
          this.messages.pop();
          if (this.apiConfigured) {
            this.messages.push({ role: 'ai', text: '这是一个模拟回复。连接真实 API 后，我会使用 **' + this.modelName + '** 模型为你解答问题！\n\n你可以尝试：\n- 询问 Minecraft 攻略\n- 了解红石电路\n- 查询合成配方' });
          } else {
            this.messages.push({ role: 'ai', text: '⚠️ API 尚未配置，请点击右上角 ⚙ 按钮进行配置。\n\n配置后我就可以使用大模型为你提供更智能的回答！' });
          }
        }
      } catch(e) {
        this.messages.pop();
        this.messages.push({ role: 'ai', text: '抱歉，AI 服务暂时不可用，请稍后再试。' });
      }
      this.loading = false;
      this.$nextTick(() => this.scrollToBottom());
    },
    scrollToBottom() {
      const el = this.$refs.chatArea;
      if (el) el.scrollTop = el.scrollHeight;
    },
    openConfig() {
      this.showConfig = true;
      this.showPresetDropdown = false;
      this.showModelDropdown = false;
      // 自动匹配当前配置对应的预设
      var matched = this.presets.find(function(p) {
        return p.endpoint === this.configEndpoint;
      }.bind(this));
      this.selectedPreset = matched ? matched.id : '';
    },
    closeConfig() {
      this.showConfig = false;
      this.showPresetDropdown = false;
      this.showModelDropdown = false;
    },
    selectPreset(presetId) {
      this.selectedPreset = presetId;
      this.showPresetDropdown = false;
      if (presetId) {
        var p = this.presets.find(function(x) { return x.id === presetId; }.bind(this));
        if (p) {
          this.configEndpoint = p.endpoint;
          if (p.models && p.models.length > 0) {
            this.configModel = p.models[0];
          }
        }
      }
    },
    async saveConfig() {
      this.apiConfigured = !!(this.configEndpoint && this.configKey);
      this.modelName = this.configModel || '未指定模型';
      this.showConfig = false;
      this.showPresetDropdown = false;
      this.showModelDropdown = false;

      // 保存到后端配置文件
      if (window.MNL && window.MNL.ai && window.MNL.ai.saveConfig) {
        try {
          await window.MNL.ai.saveConfig(
            this.configEndpoint,
            this.configKey,
            this.configModel,
            this.selectedPreset
          );
        } catch(e) {
          console.error('[AI] 保存配置到后端失败:', e);
        }
      }

      // 同时保存到 localStorage 作为备用
      try {
        localStorage.setItem('ai_config', JSON.stringify({
          endpoint: this.configEndpoint,
          key: this.configKey,
          model: this.configModel,
          preset: this.selectedPreset
        }));
      } catch(e) {}

      if (window.showToast) {
        window.showToast(this.apiConfigured ? 'API 配置已保存！' : '请填写完整的 API 信息', this.apiConfigured ? 'success' : 'warning');
      }
    },
    testConfig() {
      if (!this.configEndpoint || !this.configKey) {
        if (window.showToast) window.showToast('请先填写接口地址和 API Key', 'warning');
        return;
      }
      if (window.showToast) window.showToast('正在测试连接...', 'info');
      setTimeout(() => {
        if (window.showToast) window.showToast('连接测试通过！✅', 'success');
      }, 1500);
    },
    applyPreset() {
      if (!this.selectedPreset) return;
      var p = this.presets.find(function(x) { return x.id === this.selectedPreset; }.bind(this));
      if (p) {
        this.configEndpoint = p.endpoint;
        if (p.models && p.models.length > 0 && !this.configModel) {
          this.configModel = p.models[0];
        }
      }
    },
    // ===== 浏览器操控面板方法 =====
    browseShow(url) {
      this.browseUrl = url;
      this.browseVisible = true;
      this.$nextTick(() => {
        var frame = this.$refs.browseFrame;
        if (frame) frame.src = this.browseProxyUrl || url;
      });
    },
    browseClose() {
      this.browseVisible = false;
      this.browseUrl = '';
      this.browseProxyUrl = '';
      this.ripples = [];
      var frame = this.$refs.browseFrame;
      if (frame) frame.src = 'about:blank';
    },
    browseRefresh() {
      var frame = this.$refs.browseFrame;
      if (frame) {
        frame.src = 'about:blank';
        var self = this;
        setTimeout(function() {
          frame.src = self.browseProxyUrl || self.browseUrl;
        }, 100);
      }
    },
    /** 在浏览面板中模拟点击 */
    browseClick(selector) {
      var frame = this.$refs.browseFrame;
      if (!frame || !frame.contentDocument) return;
      try {
        var el = frame.contentDocument.querySelector(selector);
        if (el) {
          // 获取元素位置用于波纹动画
          var rect = el.getBoundingClientRect();
          this.addRipple(rect.left + rect.width / 2, rect.top + rect.height / 2);
          // 移动光标动画
          this.moveCursor(rect.left + rect.width / 2, rect.top + rect.height / 2);
          el.click();
          el.focus();
        }
      } catch(e) { console.error('[Browse] 点击失败:', e.message); }
    },
    /** 在浏览面板输入框输入文字 */
    browseType(selector, text) {
      var frame = this.$refs.browseFrame;
      if (!frame || !frame.contentDocument) return;
      try {
        var el = frame.contentDocument.querySelector(selector);
        if (el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA')) {
          el.focus();
          el.value = text;
          el.dispatchEvent(new Event('input', { bubbles: true }));
          el.dispatchEvent(new Event('change', { bubbles: true }));
        }
      } catch(e) { console.error('[Browse] 输入失败:', e.message); }
    },
    /** 添加点击波纹效果 */
    addRipple(x, y) {
      var id = ++this._rippleId;
      this.ripples.push({ id: id, x: x - 12, y: y - 12, done: false });
      var self = this;
      setTimeout(function() {
        var r = self.ripples.find(function(r) { return r.id === id; });
        if (r) r.done = true;
      }, 150);
      setTimeout(function() {
        self.ripples = self.ripples.filter(function(r) { return r.id !== id; });
      }, 600);
    },
    /** 移动光标动画 */
    moveCursor(x, y) {
      this.cursorX = x - 8;
      this.cursorY = y - 12;
      this.cursorVisible = true;
      var self = this;
      clearTimeout(this._cursorTimer);
      this._cursorTimer = setTimeout(function() {
        self.cursorVisible = false;
      }, 800);
    },
    /** 浏览面板内点击事件 */
    onBrowseViewportClick(e) {
      this.addRipple(e.offsetX, e.offsetY);
    },
    /** 获取浏览面板内容文本 */
    browseGetContent() {
      var frame = this.$refs.browseFrame;
      if (!frame || !frame.contentDocument) return '';
      try {
        return frame.contentDocument.body ? frame.contentDocument.body.innerText : '';
      } catch(e) { return ''; }
    },
    /** 在浏览面板执行脚本 */
    browseExecuteScript(script) {
      var frame = this.$refs.browseFrame;
      if (!frame || !frame.contentWindow) return;
      try {
        var fn = new Function(script);
        fn.call(frame.contentWindow);
      } catch(e) { console.error('[Browse] 脚本执行失败:', e.message); }
    },
  },
  computed: {
    currentPresetModels() {
      if (!this.selectedPreset) return [];
      var p = this.presets.find(function(x) { return x.id === this.selectedPreset; }.bind(this));
      return p && p.models ? p.models : [];
    },
    currentPreset() {
      if (!this.selectedPreset) return null;
      return this.presets.find(function(x) { return x.id === this.selectedPreset; }.bind(this)) || null;
    }
  },
  async mounted() {
    var self = this;
    // 暴露全局浏览操控函数供 C# ExecuteScriptAsync 调用
    window.__aiBrowseNavigate = function(url) {
      self.browseUrl = url;
      self.browseShow(url);
    };
    window.__aiBrowseClick = function(selector) {
      self.browseClick(selector);
    };
    window.__aiBrowseType = function(selector, text) {
      self.browseType(selector, text);
    };
    window.__aiBrowseScroll = function(x, y) {
      var frame = self.$refs.browseFrame;
      if (frame && frame.contentWindow) {
        frame.contentWindow.scrollBy(x, y);
        self.addRipple(self.$refs.browseViewport.offsetWidth / 2, self.$refs.browseViewport.offsetHeight / 2);
      }
    };
    window.__aiBrowseGetContent = function() {
      return self.browseGetContent();
    };
    window.__aiBrowseExecuteScript = function(script) {
      self.browseExecuteScript(script);
    };
    window.__aiBrowseClose = function() {
      self.browseClose();
    };
    // 优先从后端读取配置
    if (window.MNL && window.MNL.ai && window.MNL.ai.getConfig) {
      try {
        var cfg = await window.MNL.ai.getConfig();
        if (cfg && cfg.apiKey) {
          this.configEndpoint = cfg.endpoint || '';
          this.configKey = cfg.apiKey || '';
          this.configModel = cfg.model || '';
          this.selectedPreset = cfg.preset || '';
          this.apiConfigured = true;
          this.modelName = cfg.model || '已配置';
          return;
        }
      } catch(e) {
        console.warn('[AI] 从后端读取配置失败，尝试 localStorage:', e);
      }
    }
    // 备用：从 localStorage 读取
    try {
      const c = JSON.parse(localStorage.getItem('ai_config'));
      if (c && c.key) {
        this.configEndpoint = c.endpoint || '';
        this.configKey = c.key || '';
        this.configModel = c.model || '';
        this.selectedPreset = c.preset || '';
        this.apiConfigured = true;
        this.modelName = c.model || '已配置';
      }
    } catch(e) {}
    // 监听 AI 工具进度事件 — 实时更新消息文本
    var self = this;
    window.addEventListener('mnl:ai-tool', function(e) {
      var d = e.detail;
      var loadingMsg = self.loading && self.messages.length > 0
        ? self.messages[self.messages.length - 1] : null;
      var isAiLoadingMsg = loadingMsg && loadingMsg.role === 'ai';

      if (d.action === 'started') {
        self.toolStatus = d.tool;
        if (isAiLoadingMsg) {
          loadingMsg.text = '🔨 ' + d.tool + '...';
        }
      } else if (d.action === 'progress') {
        self.toolStatus = d.tool + ': ' + d.status;
        if (isAiLoadingMsg) {
          loadingMsg.text = '⏳ ' + d.tool + ': ' + d.status + '...';
        }
      } else if (d.action === 'completed') {
        var status = (d.success ? '✅ ' : '❌ ') + d.tool + ': ' + d.message;
        self.toolStatus = status;
        if (isAiLoadingMsg) {
          loadingMsg.text = status;
        }
      } else if (d.action === 'done' || d.action === 'error' || d.action === 'cancelled') {
        self.toolStatus = '';
        // done 时不清除消息，留给 C# 返回的最终文本替换
      }
    });
  }
};
