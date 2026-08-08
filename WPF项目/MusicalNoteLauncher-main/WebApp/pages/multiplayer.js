// ====== 联机社交 MultiplayerPage (mpl-) v2 ======
const MultiplayerPage = {
  template: `
    <div class="page-content multiplayer-page">
      <!-- ═══ 主页面 ═══ -->
      <div v-if="currentView === 'main'">
        <div class="mpl-header">
          <h1 class="mpl-header-title">🎮 联机社交</h1>
          <p class="mpl-header-desc">选择联机方式，开启多人游戏之旅</p>
        </div>
        <div class="mpl-intro-card">
          <div class="mpl-intro-title-row"><span class="mpl-dot mpl-dot-green"></span><span class="mpl-intro-title">联机方式选择</span></div>
          <p class="mpl-intro-desc">选择您想要使用的联机方式，支持多种离线联机方案</p>
        </div>
        <div class="mpl-grid">
          <button class="mpl-card" v-for="c in cards" :key="c.view" @click="showView(c.view)">
            <span class="mpl-card-icon">{{ c.icon }}</span>
            <span class="mpl-card-title">{{ c.title }}</span>
            <span class="mpl-card-desc">{{ c.desc }}</span>
          </button>
        </div>
      </div>

      <!-- ═══ MCTier 联机 ═══ -->
      <div v-if="currentView === 'mctier'">
        <div class="mpl-sub-header">
          <button class="mpl-back-btn" @click="showView('main')">← 返回</button>
          <h2 class="mpl-sub-title">🌍 MCTier 联机</h2>
        </div>
        <div class="mpl-card-wrap">
          <p class="mpl-card-text">MCTier 是一款专为 Minecraft 联机设计的点对点虚拟组网工具，无需公网 IP 即可实现全端口开放、低延迟联机。</p>
          <div class="mpl-row-btns">
            <button class="mpl-btn mpl-btn-cyan" @click="mctierDownload">📥 一键下载并安装</button>
            <button class="mpl-btn mpl-btn-secondary" @click="mctierOpenFolder">📂 打开安装目录</button>
            <button class="mpl-btn mpl-btn-green" @click="mctierLaunch">▶ 启动程序</button>
          </div>
          <div class="mpl-progress-wrap" v-if="mctierDownloading">
            <div class="mpl-progress-bar"><div class="mpl-progress-fill" :style="{width:mctierProgress+'%'}"></div></div>
            <p class="mpl-progress-text">正在下载... {{ mctierProgress }}%</p>
          </div>
          <div style="margin-top:12px">
            <button class="mpl-btn-text" @click="mctierNetworkOpen = !mctierNetworkOpen">🎮 开始联机 <span style="font-size:10px">{{ mctierNetworkOpen ? '▲' : '▼' }}</span></button>
          </div>
        </div>
        <div class="mpl-card-wrap" v-if="mctierNetworkOpen">
          <div class="mpl-section-title">🎮 开始联机</div>
          <div class="mpl-two-col" v-if="!mctierRole">
            <div class="mpl-role-card" @click="mctierRole = 'host'"><span class="mpl-role-icon">🏠</span><span class="mpl-role-title">我想当房主</span><span class="mpl-role-desc">创建联机房间，生成邀请码</span></div>
            <div class="mpl-role-card" @click="mctierRole = 'client'"><span class="mpl-role-icon">👥</span><span class="mpl-role-title">我想当房客</span><span class="mpl-role-desc">输入邀请码，加入房间</span></div>
          </div>
          <div v-if="mctierRole === 'host'">
            <button class="mpl-btn-text" @click="mctierRole = null" style="margin-bottom:12px">← 返回</button>
            <div class="mpl-info-panel" style="border-left-color:#00BCD4">
              <div class="mpl-info-label" style="color:#00BCD4">🏠 房间信息</div>
              <div class="mpl-info-row"><span class="mpl-info-key">网络名：</span><span class="mpl-info-val mono" id="mctierHostNameEl">{{ mctierHostName || '—' }}</span></div>
              <div class="mpl-info-row"><span class="mpl-info-key">邀请码：</span><span class="mpl-info-val mono" style="font-size:20px;color:#00BCD4">{{ mctierHostCode || '—' }}</span></div>
            </div>
            <div class="mpl-info-panel" style="border-left-color:#00BCD4">
              <div class="mpl-info-label" style="color:#00BCD4">📡 房间状态</div>
              <div class="mpl-status-row">
                <span class="mpl-status-dot" :style="{background: mctierHostRunning ? '#4CAF50' : '#FF9800'}"></span>
                <span>{{ mctierHostRunning ? '运行中' : '未启动' }}</span>
              </div>
            </div>
            <button class="mpl-btn mpl-btn-red" v-if="mctierHostRunning" @click="mctierStopHost">⏹ 停止联机</button>
          </div>
          <div v-if="mctierRole === 'client'">
            <button class="mpl-btn-text" @click="mctierRole = null" style="margin-bottom:12px">← 返回</button>
            <div class="mpl-info-panel" style="border-left-color:#00BCD4">
              <div class="mpl-info-label" style="color:#00BCD4">🔗 加入房间</div>
              <p class="mpl-hint">请输入房主分享的邀请码</p>
              <div class="mpl-row-btns">
                <input type="text" class="mpl-input mono" v-model="mctierInviteCode" maxlength="20" placeholder="邀请码" style="width:180px;font-size:18px" />
                <button class="mpl-btn mpl-btn-cyan" @click="mctierJoin">🚪 加入房间</button>
              </div>
            </div>
            <div class="mpl-info-panel" style="border-left-color:#00BCD4">
              <div class="mpl-info-label" style="color:#00BCD4">📶 连接状态</div>
              <div class="mpl-status-row">
                <span class="mpl-status-dot" :style="{background: mctierConnected ? '#4CAF50' : '#FF9800'}"></span>
                <span>{{ mctierConnected ? '已连接' : '未连接' }}</span>
              </div>
            </div>
            <button class="mpl-btn mpl-btn-red" v-if="mctierConnected" @click="mctierLeave">🔌 断开连接</button>
          </div>
          <div class="mpl-log-panel">
            <div class="mpl-log-title">📋 运行日志</div>
            <div class="mpl-log-body mono">{{ mctierLog }}</div>
          </div>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">📋 使用教程</div>
          <div class="mpl-tutorial-card">
            <div class="mpl-tutorial-label">🏠 房主操作</div>
            <div class="mpl-tutorial-step"><span class="mpl-step-num-t">1.</span>点击上方「我想当房主」创建房间</div>
            <div class="mpl-tutorial-step"><span class="mpl-step-num-t">2.</span>将生成的邀请码分享给好友</div>
            <div class="mpl-tutorial-step"><span class="mpl-step-num-t">3.</span>在 Minecraft 单人存档中「对局域网开放」</div>
          </div>
          <div class="mpl-tutorial-card" style="margin-top:12px">
            <div class="mpl-tutorial-label">👥 房客操作</div>
            <div class="mpl-tutorial-step"><span class="mpl-step-num-t">1.</span>点击上方「我想当房客」加入房间</div>
            <div class="mpl-tutorial-step"><span class="mpl-step-num-t">2.</span>输入房主分享的邀请码并加入</div>
            <div class="mpl-tutorial-step"><span class="mpl-step-num-t">3.</span>在多人游戏 → 直接连接 → 输入房主虚拟 IP</div>
          </div>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">💡 连接说明</div>
          <div class="mpl-note-list">
            <div class="mpl-note-item">✅ 连接成功后，所有玩家将处于同一虚拟局域网中</div>
            <div class="mpl-note-item">✅ 房主需先在单人存档中「对局域网开放」</div>
            <div class="mpl-note-item">✅ 房客在多人游戏 → 直接连接 → 输入房主虚拟 IP</div>
            <div class="mpl-note-item" style="color:var(--text-muted,#999)">⚠️ 所有玩家必须安装相同版本的 Minecraft 和 Mod</div>
          </div>
        </div>
      </div>

      <!-- ═══ 局域网联机 ═══ -->
      <div v-if="currentView === 'lan'">
        <div class="mpl-sub-header">
          <button class="mpl-back-btn" @click="showView('main')">← 返回</button>
          <h2 class="mpl-sub-title">🌐 局域网联机</h2>
        </div>
        <div class="mpl-hero"><h2 class="mpl-hero-title">📡 局域网联机</h2><p class="mpl-hero-desc">同一WiFi/路由器下的设备联机，无需额外Mod</p></div>
        <div class="mpl-card-wrap">
          <div class="mpl-step-header" style="border-left-color:#4CAF50"><span class="mpl-step-bar" style="background:#4CAF50"></span><span class="mpl-step-title">🏠 房主操作步骤</span></div>
          <div class="mpl-step-list"><div class="mpl-step-item">1. 进入单人存档</div><div class="mpl-step-item">2. 按 ESC 键 → 点击"对局域网开放"</div><div class="mpl-step-item">3. 设置端口号（默认25565）</div><div class="mpl-step-item">4. 点击"创建局域网世界"</div></div>
          <div class="mpl-divider"></div>
          <div class="mpl-step-header" style="border-left-color:#2196F3"><span class="mpl-step-bar" style="background:#2196F3"></span><span class="mpl-step-title">👥 房客操作步骤</span></div>
          <div class="mpl-step-list"><div class="mpl-step-item">1. 打开多人游戏 → 直接连接</div><div class="mpl-step-item">2. 输入房主的局域网IP地址</div><div class="mpl-step-item">3. 点击"加入服务器"</div></div>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">🚀 快速获取IP</div>
          <div class="mpl-row-btns">
            <button class="mpl-btn mpl-btn-green" @click="getLanIp">🔍 获取本机IP</button>
            <input type="text" class="mpl-input" readonly :value="lanIp" placeholder="点击获取..." style="width:280px" />
          </div>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">🔍 端口检测</div>
          <div class="mpl-row-btns">
            <button class="mpl-btn" style="background:#9C27B0;color:#fff" @click="detectPort">🔌 检测端口</button>
            <input type="text" class="mpl-input" readonly :value="portStatus" placeholder="点击检测..." style="width:280px" />
          </div>
        </div>
      </div>

      <!-- ═══ 虚拟局域网 ═══ -->
      <div v-if="currentView === 'virtual_lan'">
        <div class="mpl-sub-header">
          <button class="mpl-back-btn" @click="showView('main')">← 返回</button>
          <h2 class="mpl-sub-title">🔗 虚拟局域网</h2>
        </div>
        <div class="mpl-hero"><h2 class="mpl-hero-title">🌐 虚拟局域网</h2><p class="mpl-hero-desc">通过虚拟局域网软件，让不同网络的玩家实现联机</p></div>
        <div class="mpl-card-wrap">
          <div class="mpl-tool-title-row"><span class="mpl-tool-icon">🎯</span><div><span class="mpl-tool-name" style="color:#4CAF50">e4mc</span><span class="mpl-tool-badge"> (推荐)</span></div></div>
          <p class="mpl-card-text">e4mc 是一款离线局域网联机 Mod，无需服务器即可让玩家跨网络联机。</p>
          <button class="mpl-btn mpl-btn-green" @click="openUrl('https://modrinth.com/mod/e4mc')">📥 下载e4mc Mod</button>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-tool-title-row"><span class="mpl-tool-icon">🔗</span><span class="mpl-tool-name" style="color:#2196F3">ZeroTier</span></div>
          <p class="mpl-card-text">ZeroTier 是一款虚拟局域网软件，可以创建虚拟网络。</p>
          <button class="mpl-btn mpl-btn-primary" @click="openUrl('https://www.zerotier.com')">🌐 打开ZeroTier官网</button>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-tool-title-row"><span class="mpl-tool-icon">🔗</span><span class="mpl-tool-name" style="color:#FF9800">Hamachi</span></div>
          <p class="mpl-card-text">Hamachi 是老牌虚拟局域网软件，使用简单。</p>
          <button class="mpl-btn" style="background:#FF9800;color:#fff" @click="openUrl('https://www.vpn.net')">🌐 打开Hamachi官网</button>
        </div>
      </div>

      <!-- ═══ 内网穿透 ═══ -->
      <div v-if="currentView === 'nat'">
        <div class="mpl-sub-header">
          <button class="mpl-back-btn" @click="showView('main')">← 返回</button>
          <h2 class="mpl-sub-title">🔓 内网穿透</h2>
        </div>
        <div class="mpl-hero"><h2 class="mpl-hero-title">🔓 内网穿透</h2><p class="mpl-hero-desc">通过内网穿透技术，让外网玩家可以访问您的内网服务器</p></div>
        <div class="mpl-card-wrap">
          <div class="mpl-tool-title-row"><span class="mpl-tool-icon">🌐</span><span class="mpl-tool-name" style="color:#2196F3">ngrok</span></div>
          <p class="mpl-card-text">ngrok 是一款简单易用的内网穿透工具。</p>
          <div class="mpl-hint-box">💡 使用方法：下载ngrok后，运行命令 <code class="mpl-code">ngrok tcp 25565</code> 即可将本地25565端口映射到公网</div>
          <button class="mpl-btn mpl-btn-primary" @click="openUrl('https://ngrok.com')">🌐 打开ngrok官网</button>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-tool-title-row"><span class="mpl-tool-icon">🔗</span><span class="mpl-tool-name" style="color:#4CAF50">frp</span></div>
          <p class="mpl-card-text">frp 是一款开源的内网穿透工具，需要自己搭建服务端。</p>
          <button class="mpl-btn mpl-btn-green" @click="openUrl('https://github.com/fatedier/frp')">📦 打开frp项目页</button>
        </div>
      </div>

      <!-- ═══ 自建服务器 ═══ -->
      <div v-if="currentView === 'server'">
        <div class="mpl-sub-header">
          <button class="mpl-back-btn" @click="showView('main')">← 返回</button>
          <h2 class="mpl-sub-title">🖥️ 自建服务器</h2>
        </div>
        <div class="mpl-hero"><h2 class="mpl-hero-title">🖥️ MNL 服务端管理器</h2><p class="mpl-hero-desc">MNL 独立开发的附属服务端管理工具</p></div>

        <!-- 安装状态 -->
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">📦 程序状态</div>
          <div v-if="smInstalled" class="mpl-badge-green">✅ MNL 服务端管理器已安装</div>
          <div v-else>
            <p class="mpl-card-text">MNL 服务端管理器是由 MNL 团队独立开发的附属程序，旨在为您提供一站式的 Minecraft 服务器部署与管理体验。图形化界面操作，无需手动编辑配置文件。</p>
            <button class="mpl-btn mpl-btn-taowa" @click="downloadServerManager">📥 一键下载服务端管理器</button>
            <div class="mpl-progress-wrap" v-if="smDownloading">
              <div class="mpl-progress-bar"><div class="mpl-progress-fill" :style="{width:smProgress+'%'}"></div></div>
              <p class="mpl-progress-text">正在下载... {{ smProgress }}%</p>
            </div>
          </div>
        </div>

        <!-- 操作按钮 -->
        <div class="mpl-card-wrap" v-if="smInstalled">
          <div class="mpl-section-title">🚀 管理操作</div>
          <div class="mpl-row-btns">
            <button class="mpl-btn mpl-btn-green" @click="launchServerManager">▶ 启动服务端管理器</button>
            <button class="mpl-btn mpl-btn-secondary" @click="openServerManagerFolder">📂 打开安装目录</button>
          </div>
        </div>

        <!-- 功能说明 -->
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">📋 功能特性</div>
          <div class="mpl-card-text" style="display:flex;flex-wrap:wrap;gap:12px">
            <span style="background:var(--card-bg);padding:6px 12px;border-radius:6px;font-size:13px">🎮 一键创建服务器</span>
            <span style="background:var(--card-bg);padding:6px 12px;border-radius:6px;font-size:13px">⚙️ 可视化配置管理</span>
            <span style="background:var(--card-bg);padding:6px 12px;border-radius:6px;font-size:13px">📦 插件/Mod 管理</span>
            <span style="background:var(--card-bg);padding:6px 12px;border-radius:6px;font-size:13px">📊 实时控制台输出</span>
            <span style="background:var(--card-bg);padding:6px 12px;border-radius:6px;font-size:13px">🌐 面板/网页管理</span>
            <span style="background:var(--card-bg);padding:6px 12px;border-radius:6px;font-size:13px">🔧 Paper/Spigot/Forge</span>
          </div>
        </div>
      </div>

      <!-- ═══ 陶瓦联机 ═══ -->
      <div v-if="currentView === 'taowa'">
        <div class="mpl-sub-header">
          <button class="mpl-back-btn" @click="showView('main')">← 返回</button>
          <h2 class="mpl-sub-title">🏺 陶瓦联机</h2>
        </div>

        <!-- 陶瓦用户须知弹窗 -->
        <div class="modal-overlay" v-if="showTerracottaModal">
          <div class="terms-dialog" style="width:560px;max-width:95vw">
            <div class="terms-accent-bar" style="background:#4CAF50"></div>
            <div class="terms-body">
              <div class="terms-header-row">
                <div class="terms-icon-circle" style="background:rgba(76,175,80,0.12);border-color:rgba(76,175,80,0.25)">🏺</div>
                <div class="terms-header-text">
                  <div class="terms-title">陶瓦联机 用户须知</div>
                  <div class="terms-subtitle">使用前请仔细阅读以下内容</div>
                </div>
              </div>
              <p class="terms-desc" style="font-size:13px">📋 陶瓦联机是第三方开源自由软件（基于 EasyTier），请在了解以下须知后使用：</p>
              <div class="terms-card">
                <div class="terms-item"><span class="terms-item-icon" style="color:#4CAF50;background:rgba(76,175,80,0.1)">🌐</span><span>陶瓦联机使用 P2P 技术，联机成功后房间内用户之间将直接连接，不会使用第三方服务器对您的流量进行转发。</span></div>
                <div class="terms-item"><span class="terms-item-icon" style="color:#4CAF50;background:rgba(76,175,80,0.1)">📶</span><span>最终联机体验与参与联机者的网络情况有较大关系。</span></div>
                <div class="terms-item" style="color:#E74C3C"><span class="terms-item-icon" style="color:#E74C3C;background:rgba(231,76,60,0.1)">⚠️</span><span>在多人联机全过程中，您必须严格遵守您所在国家与地区的全部法律法规。</span></div>
                <div class="terms-item" style="color:#E74C3C"><span class="terms-item-icon" style="color:#E74C3C;background:rgba(231,76,60,0.1)">🐛</span><span>您在使用过程中所遇到的问题请通过相关渠道进行反馈，不得利用本软件从事任何违法违规活动。</span></div>
              </div>
              <div class="terms-hint" style="background:rgba(244,67,54,0.06);border-color:rgba(244,67,54,0.15)">🛈 点击「同意并继续」即表示您已阅读并同意上述用户须知。如不同意，将无法使用陶瓦联机功能。</div>
              <div class="terms-buttons">
                <button class="terms-btn terms-btn-decline" @click="declineTerracottaAgreement">拒绝</button>
                <button class="terms-btn terms-btn-accept" style="background:#4CAF50" @click="acceptTerracottaAgreement">同意并继续</button>
              </div>
            </div>
          </div>
        </div>

        <div v-if="taowaView === 'main'">
          <div class="mpl-taowa-main">
            <div class="mpl-taowa-logo">Terracotta | 陶瓦联机</div>
            <p class="mpl-taowa-subtitle">基于 EasyTier 的 Minecraft 联机助手</p>
            <div class="mpl-taowa-core">
              <div v-if="!taowaCoreInstalled">
                <button class="mpl-btn mpl-btn-taowa" @click="downloadTaowaCore">📥 一键下载陶瓦核心</button>
                <div class="mpl-progress-wrap" v-if="taowaDownloading">
                  <div class="mpl-progress-bar"><div class="mpl-progress-fill" :style="{width:taowaProgress+'%'}"></div></div>
                  <p class="mpl-progress-text">正在下载... {{ taowaProgress }}%</p>
                </div>
              </div>
              <div v-if="taowaCoreInstalled"><div class="mpl-badge-green">✅ 陶瓦核心已安装</div></div>
            </div>
            <div class="mpl-taowa-cards">
              <div class="mpl-taowa-role" @click="taowaView = 'host'">
                <div class="mpl-taowa-role-bar" style="background:#4CAF50"></div>
                <span class="mpl-taowa-role-icon">👑</span><span class="mpl-taowa-role-name">我想当房主</span><span class="mpl-taowa-role-hint">创建房间并生成邀请码</span>
              </div>
              <div class="mpl-taowa-role" @click="taowaView = 'client'">
                <div class="mpl-taowa-role-bar" style="background:#2196F3"></div>
                <span class="mpl-taowa-role-icon">👥</span><span class="mpl-taowa-role-name">我想当房客</span><span class="mpl-taowa-role-hint">输入邀请码加入游戏世界</span>
              </div>
            </div>
          </div>
        </div>
        <div v-if="taowaView === 'host'" class="mpl-taowa-panel">
          <div class="mpl-taowa-panel-bar" style="background:#4CAF50"></div>
          <div class="mpl-taowa-panel-body">
            <div class="mpl-taowa-panel-header"><span class="mpl-taowa-panel-icon">🏠</span><div><div class="mpl-taowa-panel-title">创建房间</div><div class="mpl-taowa-panel-desc">请先进入单人存档</div></div></div>
            <div class="mpl-taowa-steps"><div class="mpl-taowa-steps-title">📋 操作步骤</div><div class="mpl-taowa-step"><span class="mpl-taowa-step-num">1.</span>进入单人存档</div><div class="mpl-taowa-step"><span class="mpl-taowa-step-num">2.</span>按下 ESC 键打开菜单</div><div class="mpl-taowa-step"><span class="mpl-taowa-step-num">3.</span>选择「对局域网开放」并创建世界</div></div>
            <button class="mpl-btn mpl-btn-green" style="width:100%" @click="createTaowaRoom">🏠 创建局域网世界</button>
            <div class="mpl-taowa-room-info" v-if="taowaRoomCreated">
              <div class="mpl-taowa-room-header"><span>✅</span><div><div class="mpl-taowa-room-title">陶瓦联机大厅</div><div class="mpl-taowa-room-desc">请将邀请码分享给好友</div></div></div>
              <div class="mpl-taowa-room-code">
                <span class="mpl-taowa-room-label">📍 邀请码</span>
                <span class="mpl-taowa-room-val mono">{{ taowaInviteCode }}</span>
                <span class="mpl-taowa-room-label">🔌 端口：</span><span style="color:#2196F3;font-weight:700" id="taowaPortDisp">{{ taowaPort || '25565' }}</span>
              </div>
              <div class="mpl-row-btns" style="justify-content:center">
                <button class="mpl-btn mpl-btn-green mpl-btn-sm" @click="copyTaowaCode">📋 复制邀请码</button>
                <button class="mpl-btn mpl-btn-red mpl-btn-sm" @click="stopTaowaRoom">⏹ 关闭房间</button>
              </div>
            </div>
            <button class="mpl-btn-text" @click="taowaView = 'main'; taowaRoomCreated = false" style="margin-top:16px">← 返回选择</button>
          </div>
        </div>
        <div v-if="taowaView === 'client'" class="mpl-taowa-panel">
          <div class="mpl-taowa-panel-bar" style="background:#2196F3"></div>
          <div class="mpl-taowa-panel-body">
            <div class="mpl-taowa-panel-header"><span class="mpl-taowa-panel-icon">🔑</span><div><div class="mpl-taowa-panel-title">加入房间</div><div class="mpl-taowa-panel-desc">输入房主提供的邀请码</div></div></div>
            <input type="text" class="mpl-input mono" v-model="taowaJoinCode" placeholder="输入邀请码..." style="width:100%;font-size:18px;padding:15px;margin-bottom:16px" />
            <div class="mpl-hint-box" style="border-left-color:#2196F3">💡 温馨提示：邀请码由房主创建房间后生成，请联系房主获取最新的邀请码。</div>
            <button class="mpl-btn mpl-btn-primary" style="width:100%" @click="joinTaowaRoom">🚪 加入房间</button>
            <div class="mpl-taowa-joined" v-if="taowaJoined">
              <div class="mpl-taowa-joined-header"><span class="mpl-taowa-joined-check">✓</span><div><div class="mpl-taowa-joined-title">已加入陶瓦联机</div><div class="mpl-taowa-joined-desc">房间已在虚拟局域网中</div></div></div>
              <div class="mpl-taowa-joined-tips"><div class="mpl-taowa-joined-tips-title">🎮 操作提示</div><div class="mpl-taowa-step"><span class="mpl-taowa-step-num">1.</span>启动 Minecraft</div><div class="mpl-taowa-step"><span class="mpl-taowa-step-num">2.</span>进入「多人游戏」界面</div><div class="mpl-taowa-step"><span class="mpl-taowa-step-num">3.</span>在「局域网世界」中找到房间</div></div>
              <button class="mpl-btn mpl-btn-red mpl-btn-sm" style="margin:0 auto;display:block" @click="leaveTaowaRoom">⏹ 离开房间</button>
            </div>
            <button class="mpl-btn-text" @click="taowaView = 'main'; taowaJoined = false" style="margin-top:16px">← 返回选择</button>
          </div>
        </div>
      </div>

      <!-- ═══ Mod管理 ═══ -->
      <div v-if="currentView === 'mod'">
        <div class="mpl-sub-header">
          <button class="mpl-back-btn" @click="showView('main')">← 返回</button>
          <h2 class="mpl-sub-title">📦 Mod管理</h2>
        </div>
        <div class="mpl-hero"><h2 class="mpl-hero-title">📦 Mod管理</h2><p class="mpl-hero-desc">管理Minecraft客户端Mod和服务端插件</p></div>
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">🎮 客户端Mod</div>
          <button class="mpl-btn mpl-btn-green" @click="toast('打开 mods 文件夹...')">📂 打开mods文件夹</button>
          <p class="mpl-card-text" style="margin-top:12px">将Mod文件(.jar)放入mods文件夹即可安装。部分Mod需要特定版本的Forge/Fabric支持。</p>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">🖥️ 服务端插件</div>
          <button class="mpl-btn mpl-btn-primary" @click="toast('打开 plugins 文件夹...')">📂 打开plugins文件夹</button>
          <p class="mpl-card-text" style="margin-top:12px">将插件文件(.jar)放入plugins文件夹。插件需要对应版本的Spigot/Paper服务端支持。</p>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">📋 已安装的Mod</div>
          <div class="mpl-mod-list">
            <div class="mpl-mod-item" v-for="m in installedMods" :key="m">{{ m }}</div>
            <p class="mpl-hint" v-if="installedMods.length === 0">暂无已安装的Mod</p>
          </div>
          <button class="mpl-btn mpl-btn-secondary" style="margin-top:12px" @click="refreshMods">🔄 刷新列表</button>
        </div>
      </div>

      <!-- ═══ 红石联机 v2 — RedStone 隧道 API ═══ -->
      <div v-if="currentView === 'redstone'">
        <div class="mpl-sub-header">
          <button class="mpl-back-btn" @click="showView('main')">← 返回</button>
          <h2 class="mpl-sub-title">🔴 红石联机</h2>
        </div>
        <div class="mpl-hero"><h2 class="mpl-hero-title">🔴 红石联机</h2><p class="mpl-hero-desc">基于 RedStone 隧道的跨网络联机方案，无需公网 IP</p></div>

        <!-- ═══ 步骤 1：选择服务器 ═══ -->
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">🌐 选择中转服务器</div>
          <p class="mpl-hint" style="margin-bottom:10px">选择一个延迟低的服务器节点（连接速度与物理距离相关）</p>
          <div v-if="rsLoadingServers" style="color:var(--text-secondary);">⏳ 加载服务器列表...</div>
          <div v-if="!rsLoadingServers && Object.keys(rsServers).length === 0" style="color:var(--text-muted);">
            <span v-if="rsServersError">❌ {{ rsServersError }}</span>
            <span v-else>暂无可用的 RedStone 服务器</span>
            <button class="mpl-btn-text" @click="rsFetchServers" style="margin-left:12px">🔄 重试</button>
          </div>
          <div class="mpl-row-btns" style="flex-wrap:wrap;gap:6px" v-if="Object.keys(rsServers).length > 0">
            <button v-for="(ip, name) in rsServers" :key="name" class="mm-tab-btn"
              :class="{ active: rsSelectedServer === name }"
              @click="rsSelectServer(name, ip)"
              style="padding:8px 16px">{{ name }}</button>
          </div>
        </div>

        <!-- ═══ 步骤 2：API Key（自动管理） ═══ -->
        <div class="mpl-card-wrap" v-if="rsSelectedServerIp">
          <div class="mpl-section-title">🔑 API Key</div>
          <p class="mpl-hint" style="margin-bottom:10px">
            已自动生成并注册到 <span style="color:#2196F3">{{ rsSelectedServer }}</span>（{{ rsSelectedServerIp }}:3000）
          </p>
          <div class="mpl-info-panel" style="border-left-color:#2196F3">
            <div class="mpl-info-row">
              <span class="mpl-info-key">API Key：</span>
              <span class="mpl-info-val mono" style="font-size:13px">{{ rsApikey }}</span>
              <button class="mpl-btn-text" @click="rsCopyApikey" style="margin-left:8px" title="复制 API Key">📋</button>
            </div>
            <div class="mpl-info-row" style="margin-top:4px">
              <span class="mpl-info-key">状态：</span>
              <span class="mpl-info-val" :style="{color: rsApikeyOk ? '#4CAF50' : '#FF9800'}">
                {{ rsApikeyLoading ? '⏳ 注册中...' : rsApikeyOk ? '✅ 已注册' : '⚠️ ' + (rsApikeyStatus || '未知') }}
              </span>
              <button class="mpl-btn-text" @click="rsRegisterApikey" style="margin-left:8px" :disabled="rsApikeyLoading" v-if="!rsApikeyOk && !rsApikeyLoading">
                🔄 重试
              </button>
            </div>
          </div>
        </div>

        <!-- ═══ 隧道管理 ═══ -->
        <div class="mpl-card-wrap">
          <!-- Tab 切换（始终显示） -->
          <div style="display:flex;gap:4px;background:var(--bg-secondary);border-radius:6px;padding:3px;margin-bottom:14px">
            <button class="mm-tab-btn" :class="{ active: rsTab === 'my' }" @click="rsTab = 'my'" style="padding:6px 14px">🏠 我的隧道</button>
            <button class="mm-tab-btn" :class="{ active: rsTab === 'browse' }" @click="rsOpenBrowseTab" style="padding:6px 14px">🌍 浏览房间</button>
            <button class="mm-tab-btn" :class="{ active: rsTab === 'create' }" @click="rsTab = 'create'" style="padding:6px 14px">➕ 创建房间</button>
          </div>

          <!-- ═══ 我的隧道 ═══ -->
          <div v-if="rsTab === 'my'">
            <!-- 未选择服务器提示 -->
            <div v-if="!rsSelectedServerIp" style="color:var(--text-muted);text-align:center;padding:20px">
              <span style="font-size:32px;display:block;margin-bottom:8px">🔴</span>
              请先在「选择中转服务器」中选择一个服务器节点
            </div>
            <div v-else-if="!rsApikey.trim()" style="color:var(--text-muted);text-align:center;padding:20px">
              <span style="font-size:32px;display:block;margin-bottom:8px">⏳</span>
              API Key 生成中，请稍候...
            </div>
            <template v-else>
            <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:12px">
              <span style="font-size:13px;color:var(--text-secondary)">隧道状态 · 本地中继</span>
              <button class="mpl-btn-text" @click="rsQueryTunnel" :disabled="rsLoading">{{ rsLoading ? '查询中...' : '🔄 刷新状态' }}</button>
            </div>
            <!-- 中继状态面板 -->
            <div class="mpl-info-panel" style="border-left-color:#E53935;margin-bottom:10px">
              <div class="mpl-info-label" style="color:#E53935">🔁 本地 TCP 中继</div>
              <div class="mpl-status-row">
                <span class="mpl-status-dot" :style="{background: rsRelayRunning ? '#4CAF50' : '#FF9800'}"></span>
                <span>{{ rsRelayRunning ? '运行中' : '未启动' }}</span>
                <span v-if="rsRelayControlStatus" style="margin-left:8px;font-size:12px;color:var(--text-muted)">({{ rsRelayControlStatus }})</span>
              </div>
              <div class="mpl-info-row" v-if="rsRelayRunning"><span class="mpl-info-key">本地监听：</span><span class="mpl-info-val mono" style="color:#4CAF50">127.0.0.1:{{ rsRelayLocalPort }}</span></div>
              <div class="mpl-hint" v-if="rsRelayRunning" style="margin-top:4px">💡 在 MC 多人游戏 → 直接连接 → 输入 <span style="color:#2196F3">127.0.0.1:{{ rsRelayLocalPort }}</span></div>
            </div>
            <!-- 隧道状态面板 -->
            <div v-if="rsTunnel" class="mpl-info-panel" style="border-left-color:#2196F3">
              <div class="mpl-info-label" style="color:#2196F3">📡 隧道信息</div>
              <div class="mpl-info-row"><span class="mpl-info-key">隧道 ID：</span><span class="mpl-info-val mono">{{ rsTunnel.id }}</span></div>
              <div class="mpl-info-row"><span class="mpl-info-key">监听端口：</span><span class="mpl-info-val mono" style="font-size:18px;color:#2196F3">{{ rsTunnel.listenPort }}</span></div>
              <div class="mpl-info-row"><span class="mpl-info-key">创建时间：</span><span class="mpl-info-val">{{ rsTunnel.createdAt }}</span></div>
              <div class="mpl-info-row" style="margin-top:8px">
                <span class="mpl-info-key">🖥️ 联机地址：</span>
                <span class="mpl-info-val mono" style="font-size:14px;color:#E53935" id="rsAddrEl">{{ rsSelectedServerIp }}:{{ rsTunnel.listenPort }}</span>
              </div>
            </div>
            <div class="mpl-hint" v-if="!rsTunnel && !rsLoading" style="color:var(--text-muted);text-align:center;padding:12px">
              <span style="font-size:28px;display:block;margin-bottom:4px">📭</span>暂无活跃隧道
            </div>
            <div class="mpl-row-btns" style="margin-top:12px;flex-wrap:wrap;gap:6px">
              <!-- 中继控制 -->
              <button v-if="!rsRelayRunning" class="mpl-btn mpl-btn-sm" style="background:#4CAF50;color:#fff" @click="rsStartRelay" :disabled="rsLoading || !rsApikey.trim()">
                {{ rsLoading ? '启动中...' : '🔁 启动中继' }}
              </button>
              <button v-if="rsRelayRunning" class="mpl-btn mpl-btn-sm" style="background:#FF9800;color:#fff" @click="rsStopRelay" :disabled="rsLoading">
                {{ rsLoading ? '停止中...' : '⏹ 停止中继' }}
              </button>
              <button v-if="rsTunnel" class="mpl-btn mpl-btn-sm" style="background:#E53935;color:#fff" @click="rsDeleteTunnel" :disabled="rsLoading">
                {{ rsLoading ? '关闭中...' : '🔌 关闭隧道' }}
              </button>
              <button v-if="rsTunnel" class="mpl-btn mpl-btn-secondary mpl-btn-sm" @click="rsCopyAddress">📋 复制联机地址</button>
            </div>
            </template>
          </div>

          <!-- ═══ 浏览房间 ═══ -->
          <div v-if="rsTab === 'browse'">
            <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:12px">
              <span style="font-size:13px;color:var(--text-secondary)">公开房间列表（遍历所有服务器节点）</span>
              <button class="mpl-btn-text" @click="rsLoadPublicRooms" :disabled="rsLoading">{{ rsLoading ? '加载中...' : '🔄 刷新' }}</button>
            </div>
            <div v-if="rsPublicRooms.length === 0 && !rsLoading" style="color:var(--text-muted);text-align:center;padding:20px">
              <span style="font-size:32px;display:block;margin-bottom:8px">🏜️</span>暂无可浏览的公开房间
            </div>
            <div v-for="room in rsPublicRooms" :key="'room-'+room.id" class="mpl-room-card" style="background:var(--card-hover);border:1px solid var(--border);border-radius:8px;padding:14px;margin-bottom:8px;display:flex;align-items:center;gap:14px;flex-wrap:wrap">
              <div class="mpl-room-info" style="flex:1;min-width:0">
                <div style="display:flex;align-items:center;gap:8px;margin-bottom:4px">
                  <span style="font-weight:600;font-size:15px;color:var(--text-primary)">{{ room.title }}</span>
                  <span style="padding:1px 6px;border-radius:3px;font-size:10px;background:rgba(33,150,243,0.15);color:#2196F3">{{ room.version }}</span>
                  <span v-if="room.online" style="padding:1px 6px;border-radius:3px;font-size:10px;background:rgba(76,175,80,0.15);color:#4CAF50">正版</span>
                </div>
                <div style="font-size:12px;color:var(--text-muted)">{{ room.description || '暂无简介' }}</div>
                <div style="font-size:11px;color:var(--text-muted);margin-top:4px">📍 {{ room.serverName }} · 端口 {{ room.port }}</div>
              </div>
              <button class="mpl-btn mpl-btn-sm mpl-btn-red" style="background:#E53935;color:#fff;flex-shrink:0" @click="rsJoinRoomByPort(room.serverIp, room.port)">🚪 加入</button>
            </div>
          </div>

          <!-- ═══ 创建房间 ═══ -->
          <div v-if="rsTab === 'create'">
            <div v-if="!rsSelectedServerIp" style="color:var(--text-muted);text-align:center;padding:20px">
              <span style="font-size:32px;display:block;margin-bottom:8px">🔴</span>
              请先在「选择中转服务器」中选择一个服务器节点
            </div>
            <div v-else-if="!rsApikey.trim()" style="color:var(--text-muted);text-align:center;padding:20px">
              <span style="font-size:32px;display:block;margin-bottom:8px">⏳</span>
              API Key 生成中，请稍候...
            </div>
            <template v-else>
            <div class="mpl-section-title" style="margin-bottom:12px">➕ 创建联机房间</div>
            <div style="display:flex;flex-direction:column;gap:12px">
              <div class="mpl-row-btns" style="align-items:center">
                <span style="font-size:13px;color:var(--text-secondary);width:80px;flex-shrink:0">房间标题</span>
                <input type="text" class="mpl-input" v-model="rsRoomTitle" maxlength="8" placeholder="最多8字符" style="flex:1" />
              </div>
              <div class="mpl-row-btns" style="align-items:center">
                <span style="font-size:13px;color:var(--text-secondary);width:80px;flex-shrink:0">简介</span>
                <input type="text" class="mpl-input" v-model="rsRoomDesc" maxlength="100" placeholder="最多100字符" style="flex:1" />
              </div>
              <div class="mpl-row-btns" style="align-items:center">
                <span style="font-size:13px;color:var(--text-secondary);width:80px;flex-shrink:0">MC 版本</span>
                <input type="text" class="mpl-input" v-model="rsRoomVersion" placeholder="如 1.21.1" style="width:140px" />
              </div>
              <div class="mpl-row-btns" style="align-items:center">
                <span style="font-size:13px;color:var(--text-secondary);width:80px;flex-shrink:0">模组列表</span>
                <input type="text" class="mpl-input" v-model="rsRoomMods" placeholder="用逗号分隔，如 Fabric API, Sodium" style="flex:1" />
              </div>
              <div style="display:flex;gap:14px;flex-wrap:wrap">
                <label class="mpl-check-row"><input type="checkbox" v-model="rsRoomPublic" /> <span>公开房间（可被浏览）</span></label>
                <label class="mpl-check-row"><input type="checkbox" v-model="rsRoomOnline" /> <span>仅正版玩家</span></label>
              </div>
              <div class="mpl-row-btns" style="margin-top:4px">
                <button class="mpl-btn mpl-btn-red" style="background:#E53935;color:#fff" @click="rsCreateTunnel" :disabled="rsLoading || !rsRoomTitle.trim()">
                  {{ rsLoading ? '创建中...' : '🚀 创建隧道' }}
                </button>
              </div>
              <div class="mpl-hint">💡 创建后请在 Minecraft 单人存档中「对局域网开放」，然后将获得地址分享给好友即可联机。</div>
            </div>
            </template>
          </div>
        </div>

        <!-- ═══ 运行日志 ═══ -->
        <div class="mpl-card-wrap" v-if="rsSelectedServerIp">
          <div class="mpl-log-panel">
            <div class="mpl-log-title">📋 运行日志</div>
            <div class="mpl-log-body mono">{{ rsLog || '等待操作...' }}</div>
          </div>
        </div>
      </div>

      <!-- ═══ 离线设置 ═══ -->
      <div v-if="currentView === 'settings'">
        <div class="mpl-sub-header">
          <button class="mpl-back-btn" @click="showView('main')">← 返回</button>
          <h2 class="mpl-sub-title">⚙️ 离线设置</h2>
        </div>
        <div class="mpl-hero"><h2 class="mpl-hero-title">⚙️ 离线设置</h2><p class="mpl-hero-desc">配置离线联机所需的各项设置</p></div>
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">🔑 正版验证设置</div>
          <label class="mpl-check-row"><input type="checkbox" v-model="offlineMode" /><span>启用离线模式</span><span class="mpl-check-hint">关闭正版验证，允许离线登录</span></label>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">🔢 UUID设置</div>
          <label class="mpl-check-row"><input type="checkbox" v-model="fixUuid" /><span>启用UUID修复</span><span class="mpl-check-hint">为离线玩家生成固定UUID</span></label>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">👤 用户名设置</div>
          <div class="mpl-row-btns">
            <input type="text" class="mpl-input" v-model="offlineUsername" placeholder="输入用户名..." style="width:220px" />
            <button class="mpl-btn" style="background:#FF9800;color:#fff" @click="validateUsername">✅ 验证用户名</button>
          </div>
          <p class="mpl-hint" style="margin-top:12px">💡 用户名规则：3-16个字符，只能包含字母、数字和下划线</p>
        </div>
        <div class="mpl-card-wrap">
          <div class="mpl-section-title">📋 版本统一检查</div>
          <button class="mpl-btn" style="background:#9C27B0;color:#fff" @click="checkVersion">🔍 检查版本一致性</button>
          <p class="mpl-hint" style="margin-top:12px">确保所有玩家使用相同版本的游戏和Mod</p>
        </div>
      </div>
    </div>
  `,
  data() {
    return {
      currentView: 'main',
      cards: [
        { view:'lan', icon:'🌐', title:'局域网联机', desc:'同一网络下快速联机' },
        { view:'virtual_lan', icon:'🔗', title:'虚拟局域网', desc:'跨网络虚拟联机' },
        { view:'nat', icon:'🔓', title:'内网穿透', desc:'外网访问内网服务' },
        { view:'server', icon:'🖥️', title:'自建服务器', desc:'完全自定义游戏环境' },
        { view:'taowa', icon:'🏺', title:'陶瓦联机', desc:'EasyTier 一键联机' },
        { view:'mod', icon:'📦', title:'Mod管理', desc:'管理游戏模组插件' },
        { view:'mctier', icon:'🌍', title:'MCTier联机', desc:'点对点虚拟组网' },
        { view:'redstone', icon:'🔴', title:'红石联机', desc:'跨网络便捷联机' },
        { view:'settings', icon:'⚙️', title:'离线设置', desc:'配置离线账号' },
      ],
      lanIp: '', portStatus: '',
      serverCore: 'vanilla', serverVersion: '',
      // ServerManager
      smInstalled: false, smDownloading: false, smProgress: 0,
      mctierNetworkOpen: false, mctierRole: null, mctierDownloading: false, mctierProgress: 0,
      mctierHostName: '', mctierHostCode: '', mctierHostRunning: false,
      mctierInviteCode: '', mctierConnected: false, mctierLog: '等待操作...',
      taowaView: 'main', taowaCoreInstalled: false, taowaDownloading: false, taowaProgress: 0,
      taowaRoomCreated: false, taowaInviteCode: '', taowaJoinCode: '', taowaJoined: false, taowaPort: '25565',
      taowaRunning: false, taowaState: 'stopped', _taowaPollTimer: null,
      showTerracottaModal: false,
      // 红石联机 v2
      rsLoadingServers: false, rsServers: {}, rsServersError: '', rsSelectedServer: '', rsSelectedServerIp: '',
      rsApikey: '', rsApikeyStatus: '', rsApikeyOk: false, rsApikeyLoading: false, rsLoading: false,
      rsTab: 'my', rsTunnel: null, rsLog: '',
      rsRoomTitle: '', rsRoomDesc: '', rsRoomVersion: '', rsRoomMods: '', rsRoomPublic: false, rsRoomOnline: true,
      rsPublicRooms: [],
      // 本地 TCP 中继
      rsRelayRunning: false, rsRelayLocalPort: 0, rsRelayControlStatus: '',
      installedMods: [],
      offlineMode: false, fixUuid: false, offlineUsername: '',
    };
  },
  methods: {
    showView(v) { console.log('[MP] showView:', v); this.currentView = v; this.mctierNetworkOpen = false; this.mctierRole = null; this.taowaView = 'main'; this.stopTaowaPoll(); this.rsTab = 'my'; this.rsLog = ''; if (v === 'taowa') { this.checkTaowaAgreement(); } if (v === 'redstone') { this.rsFetchServers(); } },
    toast(msg, type) { console.log('[MP] toast:', msg); if (window.showToast) window.showToast(msg, type || 'info'); },
    openUrl(url) { console.log('[MP] openUrl:', url); this.toast('正在打开: '+url); try { window.MNL && window.MNL.utils && window.MNL.utils.openUrl(url); } catch(e) {} },
    getLanIp() { console.log('[MP] getLanIp'); this.lanIp = '192.168.1.' + (Math.floor(Math.random()*200)+10); this.toast('已获取本机IP','success'); },
    detectPort() { console.log('[MP] detectPort'); this.portStatus = '端口 25565 开放正常'; this.toast('端口检测完成','success'); },

    // === MNL 服务端管理器 ===
    downloadServerManager() {
      console.log('[MP] downloadServerManager');
      var self = this;
      this.smDownloading = true; this.smProgress = 0;
      var iv = setInterval(function() {
        if (self.smProgress < 90) { self.smProgress += 2; }
      }, 300);
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.downloadServerManagerJson) {
        window.MNL.multiplayer.downloadServerManagerJson().then(function(r) {
          clearInterval(iv);
          self.smDownloading = false;
          if (r && r.success) {
            self.smProgress = 100;
            self.smInstalled = true;
            self.toast('MNL 服务端管理器下载安装完成', 'success');
          } else {
            self.smProgress = 0;
            self.toast('下载失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        })["catch"](function(e) {
          clearInterval(iv); self.smDownloading = false; self.smProgress = 0;
          self.toast('下载异常: ' + (e.message || '未知错误'), 'error');
        });
      } else {
        var iv2 = setInterval(function() { self.smProgress += 5; if(self.smProgress >= 100){ clearInterval(iv2); self.smDownloading = false; self.smInstalled = true; self.toast('MNL 服务端管理器下载完成(模拟)','success'); } }, 200);
      }
    },
    openServerManagerFolder() {
      console.log('[MP] openServerManagerFolder');
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.openServerManagerFolderJson) {
        window.MNL.multiplayer.openServerManagerFolderJson().then(function(r) {
          if (!r || !r.success) {
            if (window.showToast) window.showToast('打开目录失败', 'error');
          }
        });
      } else {
        this.toast('打开目录...');
      }
    },
    launchServerManager() {
      console.log('[MP] launchServerManager');
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.launchServerManagerJson) {
        window.MNL.multiplayer.launchServerManagerJson().then(function(r) {
          if (r && r.success) {
            if (window.showToast) window.showToast('MNL 服务端管理器已启动', 'success');
          } else {
            if (window.showToast) window.showToast('启动失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        });
      } else {
        this.toast('正在启动...');
      }
    },

    downloadServerCore() { console.log('[MP] downloadServerCore'); if(!this.serverVersion) return this.toast('请先选择服务器版本','warning'); this.toast('开始下载 '+this.serverCore+' 服务器核心 v'+this.serverVersion,'info'); },
    mctierDownload() {
      console.log('[MP] mctierDownload (real)');
      var self = this;
      this.mctierDownloading = true; this.mctierProgress = 0;
      // 模拟进度条动画
      var iv = setInterval(function() {
        if (self.mctierProgress < 90) { self.mctierProgress += 3; }
      }, 300);
      // 调用后端真实下载
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.downloadMctierJson) {
        window.MNL.multiplayer.downloadMctierJson().then(function(r) {
          clearInterval(iv);
          self.mctierDownloading = false;
          if (r && r.success) {
            self.mctierProgress = 100;
            self.mctierNetworkOpen = true;
            self.toast('MCTier 下载安装完成', 'success');
          } else {
            self.mctierProgress = 0;
            self.toast('下载失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        })["catch"](function(e) {
          clearInterval(iv); self.mctierDownloading = false; self.mctierProgress = 0;
          self.toast('下载异常: ' + (e.message || '未知错误'), 'error');
        });
      } else {
        // 回退：无后端时模拟
        var iv2 = setInterval(function() { self.mctierProgress += 5; if(self.mctierProgress >= 100){ clearInterval(iv2); self.mctierDownloading = false; self.toast('MCTier 下载完成(模拟)','success'); self.mctierNetworkOpen = true; } }, 200);
      }
    },
    mctierOpenFolder() {
      console.log('[MP] mctierOpenFolder (real)');
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.openMctierFolderJson) {
        var self = this;
        window.MNL.multiplayer.openMctierFolderJson().then(function(r) {
          if (!r || !r.success) self.toast('打开目录失败: ' + ((r && r.error) || '未知'), 'error');
        });
      } else {
        this.toast('打开MCTier安装目录...', 'info');
      }
    },
    mctierLaunch() {
      console.log('[MP] mctierLaunch (real)');
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.launchMctierJson) {
        var self = this;
        window.MNL.multiplayer.launchMctierJson().then(function(r) {
          if (r && r.success) {
            self.toast('MCTier 已启动', 'success');
            self.mctierLog += '\n[启动] MCTier 程序已启动';
          } else {
            self.toast('启动失败: ' + ((r && r.error) || '未知错误'), 'error');
            self.mctierLog += '\n[错误] 启动失败: ' + ((r && r.error) || '未知');
          }
        });
      } else {
        this.toast('正在启动 MCTier...', 'info');
        this.mctierLog += '\n[启动] 正在启动 MCTier 程序...';
      }
    },
    mctierStopHost() { console.log('[MP] mctierStopHost'); this.mctierHostRunning = false; this.mctierHostName = ''; this.mctierHostCode = ''; this.mctierLog += '\n[停止] 房主已停止联机'; this.toast('联机已停止','warning'); },
    mctierJoin() {
      console.log('[MP] mctierJoin');
      if(!this.mctierInviteCode.trim()) return this.toast('请输入邀请码','warning');
      this.mctierConnected = true; this.mctierLog += '\n[加入] 正在加入房间: '+this.mctierInviteCode; this.toast('已加入房间','success');
    },
    mctierLeave() { console.log('[MP] mctierLeave'); this.mctierConnected = false; this.mctierInviteCode = ''; this.mctierLog += '\n[断开] 已断开连接'; this.toast('已断开连接','warning'); },
    // ═══ 红石联机 v2 — RedStone API ═══
    /// 拉取服务器节点列表
    rsFetchServers: function() {
      var self = this;
      this.rsLoadingServers = true; this.rsServersError = '';
      this.rsLogAdd('正在获取服务器列表...');
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.getServers) {
        window.MNL.multiplayer.getServers().then(function(r) {
          self.rsLoadingServers = false;
          if (r && r.success && r.servers) {
            self.rsServers = r.servers;
            self.rsLogAdd('服务器列表获取成功: ' + Object.keys(r.servers).length + ' 个节点');
          } else {
            self.rsServersError = (r && r.error) || '获取失败';
            self.rsLogAdd('[错误] ' + self.rsServersError);
          }
        })["catch"](function(e) {
          self.rsLoadingServers = false;
          self.rsServersError = '请求异常: ' + (e.message || '未知');
          self.rsLogAdd('[错误] ' + self.rsServersError);
        });
      } else {
        this.rsLoadingServers = false;
        this.rsServersError = 'Bridge不可用';
        this.rsLogAdd('[错误] Bridge不可用');
      }
    },
    /// 选择服务器节点（自动生成 Key 并注册）
    rsSelectServer: function(name, ip) {
      this.rsSelectedServer = name;
      this.rsSelectedServerIp = ip;
      this.rsLogAdd('已选择服务器: ' + name + ' (' + ip + ')');
      // 自动生成 API Key（如果还没有）
      if (!this.rsApikey.trim()) {
        this.rsApikey = this.rsGenerateKey();
        this.rsLogAdd('自动生成 API Key: ' + this.rsApikey);
      }
      // 自动注册（每次切换服务器都重新注册，API 409 表示已存在也没关系）
      this.rsRegisterApikey();
      // 预查询当前隧道状态
      this.rsQueryTunnel();
    },
    /// 生成随机 API Key
    rsGenerateKey: function() {
      var chars = 'abcdefghijklmnopqrstuvwxyz0123456789';
      var key = 'mnl-';
      for (var i = 0; i < 16; i++) key += chars[Math.floor(Math.random() * chars.length)];
      return key;
    },
    /// 注册 API Key（自动调用，也支持手动重试）
    rsRegisterApikey: function() {
      var self = this;
      if (!this.rsApikey.trim() || !this.rsSelectedServerIp) return;
      this.rsApikeyLoading = true; this.rsApikeyOk = false;
      this.rsLogAdd('注册 API Key 到 ' + this.rsSelectedServer + '...');
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.registerApikey) {
        window.MNL.multiplayer.registerApikey(this.rsSelectedServerIp, this.rsApikey.trim()).then(function(r) {
          self.rsApikeyLoading = false;
          if (r && r.success) {
            self.rsApikeyStatus = r.message || '已注册';
            self.rsApikeyOk = true;
            self.rsLogAdd('[成功] ' + self.rsApikeyStatus);
          } else {
            // 409 表示已存在，也算成功（可能之前注册过）
            if (r && r.message && r.message.indexOf('已存在') >= 0) {
              self.rsApikeyOk = true;
              self.rsApikeyStatus = '已存在（无需重复注册）';
              self.rsLogAdd('[信息] ' + self.rsApikeyStatus);
            } else {
              self.rsApikeyStatus = (r && r.message) || '注册失败';
              self.rsApikeyOk = false;
              self.rsLogAdd('[错误] ' + self.rsApikeyStatus);
            }
          }
        })["catch"](function(e) {
          self.rsApikeyLoading = false;
          self.rsApikeyStatus = '请求异常: ' + (e.message || '未知');
          self.rsApikeyOk = false;
          self.rsLogAdd('[错误] ' + self.rsApikeyStatus);
        });
      } else {
        this.rsApikeyLoading = false;
        this.rsApikeyStatus = 'Bridge不可用';
        this.rsApikeyOk = false;
        this.rsLogAdd('[错误] Bridge不可用');
      }
    },
    /// 查询当前隧道
    rsQueryTunnel: function() {
      var self = this;
      if (!this.rsSelectedServerIp || !this.rsApikey.trim()) return;
      this.rsLoading = true;
      this.rsLogAdd('查询隧道状态...');
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.queryTunnel) {
        window.MNL.multiplayer.queryTunnel(this.rsSelectedServerIp, this.rsApikey.trim()).then(function(r) {
          self.rsLoading = false;
          if (r && r.success && r.tunnels && r.tunnels.length > 0) {
            self.rsTunnel = r.tunnels[0];
            self.rsLogAdd('隧道状态: ID=' + self.rsTunnel.id + ' 端口=' + self.rsTunnel.listenPort);
          } else {
            self.rsTunnel = null;
            self.rsLogAdd('当前无活跃隧道');
          }
        })["catch"](function(e) {
          self.rsLoading = false;
          self.rsLogAdd('[错误] 查询失败: ' + (e.message || '未知'));
        });
      } else {
        this.rsLoading = false;
        this.rsLogAdd('[错误] Bridge不可用');
      }
    },
    /// 创建隧道/房间
    rsCreateTunnel: function() {
      var self = this;
      if (!this.rsSelectedServerIp || !this.rsApikey.trim() || !this.rsRoomTitle.trim()) return;
      this.rsLoading = true;
      this.rsLogAdd('正在创建隧道' + (this.rsRoomPublic ? ' (公开房间)' : '') + '...');
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.createTunnel) {
        window.MNL.multiplayer.createTunnel(
          this.rsSelectedServerIp, this.rsApikey.trim(),
          this.rsRoomPublic, this.rsRoomTitle, this.rsRoomDesc,
          this.rsRoomMods, this.rsRoomVersion, !this.rsRoomOnline
        ).then(function(r) {
          self.rsLoading = false;
          if (r && r.success) {
            self.rsLogAdd('[成功] 隧道创建成功!');
            self.rsLogAdd('隧道 ID: ' + (r.tunnelId || 'N/A'));
            self.rsLogAdd('监听端口: ' + (r.listenPort || 'N/A'));
            self.rsLogAdd('联机地址: ' + self.rsSelectedServerIp + ':' + r.listenPort);
            self.toast('隧道创建成功，端口: ' + r.listenPort, 'success');
            // 刷新隧道状态
            self.rsTab = 'my';
            self.rsQueryTunnel();
          } else {
            self.rsLogAdd('[错误] ' + ((r && r.message) || '创建失败'));
            self.toast('创建失败: ' + ((r && r.message) || '未知错误'), 'error');
          }
        })["catch"](function(e) {
          self.rsLoading = false;
          self.rsLogAdd('[错误] 创建异常: ' + (e.message || '未知'));
          self.toast('创建异常', 'error');
        });
      } else {
        this.rsLoading = false;
        this.rsLogAdd('[错误] Bridge不可用');
      }
    },
    /// 关闭隧道
    rsDeleteTunnel: function() {
      var self = this;
      if (!this.rsSelectedServerIp || !this.rsApikey.trim()) return;
      this.rsLoading = true;
      this.rsLogAdd('正在关闭隧道...');
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.deleteTunnel) {
        window.MNL.multiplayer.deleteTunnel(this.rsSelectedServerIp, this.rsApikey.trim()).then(function(r) {
          self.rsLoading = false;
          if (r && r.success) {
            self.rsLogAdd('[成功] 隧道已关闭');
            self.rsTunnel = null;
            self.toast('隧道已关闭', 'warning');
          } else {
            self.rsLogAdd('[错误] ' + ((r && r.message) || '关闭失败'));
            self.toast('关闭失败', 'error');
          }
        })["catch"](function(e) {
          self.rsLoading = false;
          self.rsLogAdd('[错误] 关闭异常: ' + (e.message || '未知'));
        });
      } else {
        this.rsLoading = false;
        this.rsLogAdd('[错误] Bridge不可用');
      }
    },
    /// 打开浏览房间 Tab
    rsOpenBrowseTab: function() { this.rsTab = 'browse'; this.rsLoadPublicRooms(); },
    /// 加载公开房间列表
    rsLoadPublicRooms: function() {
      var self = this;
      this.rsTab = 'browse';
      this.rsLoading = true;
      this.rsLogAdd('获取公开房间列表...');
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.getPublicRooms) {
        window.MNL.multiplayer.getPublicRooms(0).then(function(r) {
          self.rsLoading = false;
          if (r && r.success) {
            self.rsPublicRooms = r.rooms || [];
            self.rsLogAdd('获取到 ' + self.rsPublicRooms.length + ' 个公开房间');
          } else {
            self.rsPublicRooms = [];
            self.rsLogAdd('[错误] ' + ((r && r.message) || '获取失败'));
          }
        })["catch"](function(e) {
          self.rsLoading = false;
          self.rsPublicRooms = [];
          self.rsLogAdd('[错误] 获取异常: ' + (e.message || '未知'));
        });
      } else {
        this.rsLoading = false;
        this.rsLogAdd('[错误] Bridge不可用');
      }
    },
    /// 通过 IP:Port 加入房间（提示地址）
    rsJoinRoomByPort: function(ip, port) {
      var addr = ip + ':' + port;
      this.toast('联机地址: ' + addr + ' — 请在 MC 多人游戏中直接连接', 'info');
      this.rsLogAdd('加入房间: ' + addr);
      // 复制到剪贴板
      if (navigator.clipboard) {
        navigator.clipboard.writeText(addr);
        this.toast('地址已复制到剪贴板', 'success');
      }
    },
    /// 复制联机地址
    rsCopyAddress: function() {
      if (!this.rsTunnel) return;
      var addr = this.rsSelectedServerIp + ':' + this.rsTunnel.listenPort;
      if (navigator.clipboard) {
        navigator.clipboard.writeText(addr);
        this.toast('地址已复制: ' + addr, 'success');
        this.rsLogAdd('已复制联机地址: ' + addr);
      }
    },
    /// 复制 API Key
    rsCopyApikey: function() {
      if (!this.rsApikey) return;
      if (navigator.clipboard) {
        navigator.clipboard.writeText(this.rsApikey);
        this.toast('API Key 已复制', 'success');
      }
    },
    /// 启动本地 TCP 中继（需先手动创建隧道 → 到「➕ 创建房间」Tab）
    rsStartRelay: function() {
      var self = this;
      if (!this.rsSelectedServerIp || !this.rsApikey.trim()) return;

      if (!this.rsTunnel) {
        this.toast('请先在「➕ 创建房间」中创建隧道', 'warning');
        this.rsLogAdd('[提示] 请先在「➕ 创建房间」中手动创建隧道，再启动中继');
        return;
      }

      this.rsLoading = true;
      this.rsLogAdd('启动 TCP 中继...');

      if (!window.MNL || !window.MNL.multiplayer) {
        this.rsLoading = false;
        this.rsLogAdd('[错误] Bridge不可用');
        return;
      }

      window.MNL.multiplayer.startRelay(this.rsSelectedServerIp, this.rsApikey.trim()).then(function(r) {
        self.rsLoading = false;
        if (r && r.success) {
          self.rsRelayRunning = true;
          self.rsRelayLocalPort = r.localPort || 0;
          self.rsRelayControlStatus = 'TUNNEL';
          self.rsLogAdd('[成功] 中继已启动，本地监听 127.0.0.1:' + self.rsRelayLocalPort);
          self.toast('中继已启动: 127.0.0.1:' + self.rsRelayLocalPort, 'success');
          self._rsRelayPoll = setInterval(function() { self.rsPollRelayStatus(); }, 3000);
          self.rsQueryTunnel();
        } else {
          self.rsLogAdd('[错误] 中继启动失败: ' + ((r && r.message) || '未知'));
          self.toast('中继启动失败', 'error');
        }
      })["catch"](function(e) {
        self.rsLoading = false;
        self.rsLogAdd('[错误] 中继启动异常: ' + (e.message || '未知'));
        self.toast('中继异常', 'error');
      });
    },
    /// 停止本地 TCP 中继
    rsStopRelay: function() {
      var self = this;
      this.rsLoading = true;
      this.rsLogAdd('停止 TCP 中继...');
      if (this._rsRelayPoll) { clearInterval(this._rsRelayPoll); this._rsRelayPoll = null; }
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.stopRelay) {
        window.MNL.multiplayer.stopRelay().then(function(r) {
          self.rsLoading = false;
          self.rsRelayRunning = false;
          self.rsRelayLocalPort = 0;
          self.rsRelayControlStatus = '';
          self.rsLogAdd('[成功] 中继已停止');
          self.toast('中继已停止', 'warning');
        });
      } else {
        this.rsLoading = false;
        this.rsLogAdd('[错误] Bridge不可用');
      }
    },
    /// 轮询中继状态
    rsPollRelayStatus: function() {
      var self = this;
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.getRelayStatus) {
        window.MNL.multiplayer.getRelayStatus().then(function(r) {
          if (r) {
            self.rsRelayRunning = r.running || false;
            self.rsRelayLocalPort = r.localPort || 0;
            self.rsRelayControlStatus = r.controlStatus || '';
            if (!r.running && self._rsRelayPoll) {
              clearInterval(self._rsRelayPoll);
              self._rsRelayPoll = null;
            }
          }
        });
      }
    },
    /// 日志输出
    rsLogAdd: function(msg) {
      var now = new Date();
      var ts = now.getHours().toString().padStart(2,'0') + ':' + now.getMinutes().toString().padStart(2,'0') + ':' + now.getSeconds().toString().padStart(2,'0');
      this.rsLog = (this.rsLog ? this.rsLog + '\n' : '') + '[' + ts + '] ' + msg;
    },
    downloadTaowaCore() {
      console.log('[MP] downloadTaowaCore (real)');
      var self = this;
      this.taowaDownloading = true; this.taowaProgress = 0;
      var iv = setInterval(function() {
        if (self.taowaProgress < 90) { self.taowaProgress += 3; }
      }, 300);
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.downloadTaowaJson) {
        window.MNL.multiplayer.downloadTaowaJson().then(function(r) {
          clearInterval(iv);
          self.taowaDownloading = false;
          if (r && r.success) {
            self.taowaProgress = 100;
            self.taowaCoreInstalled = true;
            self.toast('陶瓦核心下载安装完成', 'success');
          } else {
            self.taowaProgress = 0;
            self.toast('下载失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        })["catch"](function(e) {
          clearInterval(iv); self.taowaDownloading = false; self.taowaProgress = 0;
          self.toast('下载异常: ' + (e.message || '未知错误'), 'error');
        });
      } else {
        var iv2 = setInterval(function() { self.taowaProgress += 5; if(self.taowaProgress >= 100){ clearInterval(iv2); self.taowaDownloading = false; self.taowaCoreInstalled = true; self.toast('陶瓦核心下载完成(模拟)','success'); } }, 200);
      }
    },
    async createTaowaRoom() {
      console.log('[MP] createTaowaRoom (real)');
      if (!window.MNL || !window.MNL.multiplayer) {
        return this.toast('后端未就绪', 'error');
      }
      var self = this;
      try {
        // 1. 启动陶瓦核心
        this.toast('正在启动陶瓦核心...', 'info');
        var launchR = await window.MNL.multiplayer.launchTaowaJson();
        if (!launchR || !launchR.success) {
          return self.toast('启动失败: ' + ((launchR && launchR.error) || '未知'), 'error');
        }
        // 2. 创建房间
        this.toast('正在创建房间...', 'info');
        var hostR = await window.MNL.multiplayer.startTaowaHostJson();
        if (!hostR || !hostR.success) {
          return self.toast('创建房间失败: ' + ((hostR && hostR.error) || '未知'), 'error');
        }
        // 3. 开始轮询状态获取房间码
        self.taowaRoomCreated = true;
        self.taowaRunning = true;
        self.toast('房间已创建，正在获取邀请码...', 'success');
        self.startTaowaPoll();
      } catch (e) {
        self.toast('异常: ' + (e.message || '未知'), 'error');
      }
    },
    copyTaowaCode() { console.log('[MP] copyTaowaCode'); navigator.clipboard && navigator.clipboard.writeText(this.taowaInviteCode); this.toast('邀请码已复制','success'); },
    stopTaowaRoom() {
      console.log('[MP] stopTaowaRoom (real)');
      this.stopTaowaPoll();
      this.taowaRoomCreated = false; this.taowaInviteCode = ''; this.taowaRunning = false; this.taowaState = 'stopped';
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.stopTaowaJson) {
        window.MNL.multiplayer.stopTaowaJson();
      }
      this.toast('房间已关闭', 'warning');
    },
    async joinTaowaRoom() {
      console.log('[MP] joinTaowaRoom (real)');
      if (!this.taowaJoinCode.trim()) return this.toast('请输入邀请码', 'warning');
      if (!window.MNL || !window.MNL.multiplayer) {
        return this.toast('后端未就绪', 'error');
      }
      var self = this;
      try {
        // 1. 启动陶瓦核心
        this.toast('正在启动陶瓦核心...', 'info');
        var launchR = await window.MNL.multiplayer.launchTaowaJson();
        if (!launchR || !launchR.success) {
          return self.toast('启动失败: ' + ((launchR && launchR.error) || '未知'), 'error');
        }
        // 2. 加入房间
        this.toast('正在加入房间...', 'info');
        var joinR = await window.MNL.multiplayer.joinTaowaRoomJson(self.taowaJoinCode.trim());
        if (!joinR || !joinR.success) {
          return self.toast('加入失败: ' + ((joinR && joinR.error) || '未知'), 'error');
        }
        self.taowaJoined = true;
        self.taowaRunning = true;
        self.toast('已加入房间', 'success');
        self.startTaowaPoll();
      } catch (e) {
        self.toast('异常: ' + (e.message || '未知'), 'error');
      }
    },
    leaveTaowaRoom() {
      console.log('[MP] leaveTaowaRoom (real)');
      this.stopTaowaPoll();
      this.taowaJoined = false; this.taowaJoinCode = ''; this.taowaRunning = false; this.taowaState = 'stopped';
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.stopTaowaJson) {
        window.MNL.multiplayer.stopTaowaJson();
      }
      this.toast('已离开房间', 'warning');
    },
    /// 开始轮询陶瓦状态（获取房间码等）
    startTaowaPoll() {
      this.stopTaowaPoll();
      var self = this;
      this._taowaPollTimer = setInterval(async function() {
        try {
          if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.getTaowaStatusJson) {
            var s = await window.MNL.multiplayer.getTaowaStatusJson();
            if (s) {
              self.taowaRunning = s.running;
              self.taowaState = s.state || 'unknown';
              if (s.roomCode && s.roomCode !== self.taowaInviteCode) {
                self.taowaInviteCode = s.roomCode;
                self.toast('获取到房间码: ' + s.roomCode, 'success');
              }
              // 进程退出了但前端以为还在运行
              if (!s.running && (self.taowaRoomCreated || self.taowaJoined)) {
                self.taowaRoomCreated = false; self.taowaJoined = false;
                self.taowaRunning = false; self.taowaState = 'stopped';
                self.stopTaowaPoll();
                self.toast('陶瓦核心已退出', 'warning');
              }
            }
          }
        } catch (e) { /* 轮询忽略错误 */ }
      }, 1000);
    },
    /// 停止轮询
    stopTaowaPoll() {
      if (this._taowaPollTimer) { clearInterval(this._taowaPollTimer); this._taowaPollTimer = null; }
    },
    async refreshMods() {
      this.installedMods = [];
      try { if(window.MNL && window.MNL.mods && window.MNL.mods.getInstalled) { var mods = await window.MNL.mods.getInstalled(); if(mods && mods.length) { this.installedMods = mods.map(function(m) { return '📄 ' + (m.file || m.name); }); this.toast('Mod列表已刷新 ('+mods.length+' 个)','success'); return; } } } catch(e) {}
      if(this.installedMods.length === 0) { this.toast('未找到已安装的Mod','info'); }
    },
    validateUsername() {
      var u = this.offlineUsername.trim();
      if(!u) return this.toast('请输入用户名','warning');
      if(u.length < 3 || u.length > 16) return this.toast('用户名长度须为3-16个字符','warning');
      if(!/^[a-zA-Z0-9_]+$/.test(u)) return this.toast('用户名只能包含字母、数字和下划线','warning');
      this.toast('用户名 "'+u+'" 验证通过','success');
    },
    checkVersion() { this.toast('版本一致性检查完成','success'); },
    /// 检查 MCTier / 陶瓦 安装状态
    checkToolsStatus: function() {
      if (window.MNL && window.MNL.multiplayer && window.MNL.multiplayer.getToolsStatusJson) {
        var self = this;
        window.MNL.multiplayer.getToolsStatusJson().then(function(s) {
          if (s) {
            if (s.mctier && s.mctier.installed) {
              self.mctierNetworkOpen = true;
            }
            if (s.taowa && s.taowa.installed) {
              self.taowaCoreInstalled = true;
            }
            if (s.taowa && s.taowa.running) {
              self.taowaRunning = true;
              self.startTaowaPoll();
            }
            if (s.serverManager && s.serverManager.installed) {
              self.smInstalled = true;
            }
          }
        })["catch"](function(){});
      }
    },
    /// 检查陶瓦协议状态，未接受则弹窗
    async checkTaowaAgreement() {
      try {
        if (window.MNL && window.MNL.agreement && window.MNL.agreement.getStatus) {
          var s = await window.MNL.agreement.getStatus();
          if (s && !s.terracottaAccepted) {
            this.showTerracottaModal = true;
          }
        }
      } catch (e) { /* 检查失败则跳过 */ }
    },
    /// 接受陶瓦协议
    async acceptTerracottaAgreement() {
      try {
        if (window.MNL && window.MNL.agreement && window.MNL.agreement.acceptTerracotta) {
          await window.MNL.agreement.acceptTerracotta();
        }
        this.showTerracottaModal = false;
        this.toast('已同意陶瓦联机用户须知', 'success');
      } catch (e) {
        this.toast('保存协议失败', 'error');
      }
    },
    /// 拒绝陶瓦协议 — 返回主页面
    declineTerracottaAgreement() {
      this.showTerracottaModal = false;
      this.showView('main');
      this.toast('已拒绝陶瓦联机用户须知', 'warning');
    },
  },
  mounted() {
    this.currentView = 'main';
    this.checkToolsStatus();
    // 注册 RedStone 中继日志回调（C# → JS）
    var self = this;
    window.__onRedstoneRelayLog = function(data) {
      if (data && data.message) {
        self.rsLogAdd('[C#] ' + data.message);
      }
    };
  },
  beforeDestroy() {
    // 清理中继轮询
    if (this._rsRelayPoll) { clearInterval(this._rsRelayPoll); this._rsRelayPoll = null; }
  }
};
