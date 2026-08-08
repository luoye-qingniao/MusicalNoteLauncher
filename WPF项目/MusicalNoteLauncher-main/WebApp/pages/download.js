// ====== 下载页面 DownloadPage (dl-) ======
// 对齐 UnoApp download.html：6 分类卡片 + 自定义下载
const DownloadPage = {
  template: `
    <div class="page-content download-page">

      <!-- ═══ 主视图：6 分类卡片 ═══ -->
      <div v-if="currentTab === 'grid'" style="display:flex;flex-direction:column;gap:20px;">
        <div class="dl-header">
          <span class="dl-header-title">下载</span>
        </div>
        <div class="dl-content">
          <!-- 3×2 分类卡片网格 -->
          <div class="dl-grid">
            <button class="dl-card" @click="switchTab('versions')">
              <span class="dl-card-icon">📦</span>
              <span class="dl-card-label">游戏版本</span>
            </button>
            <button class="dl-card" @click="switchTab('packs')">
              <span class="dl-card-icon">📁</span>
              <span class="dl-card-label">整合包</span>
            </button>
            <button class="dl-card" @click="switchTab('mods')">
              <span class="dl-card-icon">🔧</span>
              <span class="dl-card-label">Mod模组</span>
            </button>
            <button class="dl-card" @click="switchTab('shaders')">
              <span class="dl-card-icon">✨</span>
              <span class="dl-card-label">光影/材质</span>
            </button>
            <button class="dl-card" @click="switchTab('datapacks')">
              <span class="dl-card-icon">📊</span>
              <span class="dl-card-label">数据包</span>
            </button>
            <button class="dl-card" @click="switchTab('dependencies')">
              <span class="dl-card-icon">📚</span>
              <span class="dl-card-label">依赖库</span>
            </button>
            <button class="dl-card" @click="switchTab('domestic-packs')">
              <span class="dl-card-icon">🇨🇳</span>
              <span class="dl-card-label">国内整合包</span>
            </button>
            <button class="dl-card" @click="switchTab('bbsmc')">
              <span class="dl-card-icon">💬</span>
              <span class="dl-card-label">BBSMC</span>
            </button>
          </div>

          <!-- 自定义下载区域 -->
          <div class="dl-custom">
            <div class="dl-custom-info">
              <div class="dl-custom-title">📥 自定义下载</div>
              <div class="dl-custom-desc">支持通过网址直接下载资源包、模组等文件（8 段并行加速）</div>
            </div>
            <div class="dl-custom-form">
              <label class="dl-custom-label">下载地址</label>
              <input class="dl-custom-input" v-model="customUrl" type="text" placeholder="请输入文件下载链接" :disabled="customDlStatus === 'downloading' || customDlStatus === 'connecting'" />
            </div>
            <div class="dl-custom-form">
              <label class="dl-custom-label">保存位置</label>
              <div class="dl-custom-path-row">
                <input class="dl-custom-input" v-model="customDlPath" type="text" :placeholder="customDlPath || '默认：系统下载文件夹'" :disabled="customDlStatus === 'downloading' || customDlStatus === 'connecting'" />
                <button class="dl-custom-folder-btn" @click="customDlBrowse" :disabled="customDlStatus === 'downloading' || customDlStatus === 'connecting'" title="选择文件夹">📂</button>
              </div>
            </div>
            <button class="dl-custom-btn" @click="startCustomDownload" :disabled="customDlStatus === 'downloading' || customDlStatus === 'connecting'">
              {{ customDlStatus === 'connecting' ? '⏳ 连接中...' : customDlStatus === 'downloading' ? '📥 下载中...' : '开始下载' }}
            </button>

            <!-- 下载进度面板 -->
            <div class="dl-custom-progress" v-if="customDlStatus === 'connecting' || customDlStatus === 'downloading'">
              <div class="dl-cp-file">{{ customDlFileName || '正在连接...' }}</div>
              <div class="dl-cp-bar-wrap">
                <div class="dl-cp-bar"><div class="dl-cp-fill" :style="{width: customDlPercent + '%'}"></div></div>
              </div>
              <div class="dl-cp-stats">
                <span>{{ formatBytes(customDlDownloaded) }} / {{ customDlTotal > 0 ? formatBytes(customDlTotal) : '计算中...' }}</span>
                <span>{{ formatSpeed(customDlSpeed) }}</span>
                <span>{{ customDlPercent.toFixed(1) }}%</span>
              </div>
              <button class="dl-custom-btn dl-custom-cancel-btn" @click="cancelCustomDownload" style="margin-top:8px;">✕ 取消下载</button>
            </div>

            <!-- 错误提示 -->
            <div class="dl-cp-error" v-if="customDlStatus === 'error'">
              <span>❌ {{ customDlError || '下载失败' }}</span>
              <button class="dl-custom-btn dl-custom-retry-btn" @click="startCustomDownload">🔄 重试</button>
            </div>
          </div>

          <!-- ═══ 下载完成弹窗 ═══ -->
          <div class="modal-overlay dl-complete-overlay" v-if="customDlCompleteShow" @click.self="customDlCompleteShow = false">
            <div class="terms-modal dl-complete-modal">
              <div class="terms-title" style="border-bottom:none;padding-bottom:12px;">✅ 下载完成</div>
              <div class="terms-desc" style="font-size:14px;color:var(--text-primary);margin-bottom:4px;">{{ customDlFileName }}</div>
              <div class="terms-desc" style="font-size:11px;color:var(--text-secondary);margin-bottom:4px;word-break:break-all;">{{ customDlSavePath }}</div>
              <div class="terms-desc" style="font-size:11px;color:var(--text-secondary);margin-bottom:16px;">大小: {{ formatBytes(customDlTotal) }}</div>
              <div style="display:flex;gap:10px;justify-content:center;">
                <button class="terms-btn terms-btn-accept" @click="customDlOpenFile" style="flex:1;padding:10px;">📂 查看文件</button>
                <button class="terms-btn terms-btn-accept" @click="customDlOpenFolder" style="flex:1;padding:10px;">📁 打开文件夹</button>
              </div>
              <button class="terms-btn terms-btn-decline" @click="customDlCompleteShow = false" style="margin-top:8px;width:100%;">关闭</button>
            </div>
          </div>
        </div>
      </div>

      <!-- ═══ 子视图：游戏版本 ═══ -->
      <div v-if="currentTab === 'versions'" class="vp-page">
        <div class="vp-header">
          <button class="vp-back-btn" @click="switchTab('grid')">&larr; 返回</button>
          <span class="vp-header-title">游戏版本</span>
        </div>
        <div style="flex:1;margin:20px;display:flex;flex-direction:column;min-height:0;overflow:hidden;">
          <div class="vp-filter">
            <div class="vp-filter-row">
              <span class="vp-filter-label">名称</span>
              <div class="vp-search-wrap">
                <div class="vp-search-box">
                  <input type="text" v-model="vpSearchText" placeholder="搜索版本 ID..." @keyup.enter="vpSearch" />
                </div>
              </div>
              <span class="vp-filter-label vp-type-label">类型</span>
              <div class="mnl-select" :class="{ open: vpTypeOpen }" v-click-outside="() => vpTypeOpen = false">
                <div class="mnl-select-trigger" @click="vpTypeOpen = !vpTypeOpen">
                  <span class="mnl-select-text">{{ vpTypeDisplayText }}</span>
                  <span class="mnl-select-arrow">▾</span>
                </div>
                <div class="mnl-select-menu" v-if="vpTypeOpen">
                  <div class="mnl-select-item" v-for="o in vpTypeOptions" :key="o.v"
                    :class="{ active: vpVersionType === o.v }"
                    @click="vpVersionType = o.v; vpTypeOpen = false; vpSearch()">
                    {{ o.t }}
                  </div>
                </div>
              </div>
            </div>
            <div class="vp-filter-actions">
              <button class="vp-btn-search" @click="vpSearch">搜索</button>
              <button class="vp-btn-reset" @click="vpReset">重置条件</button>
            </div>
          </div>
          <div class="vp-tabs">
            <button class="vp-tab" :class="{ active: vpTab === 'java' }" @click="vpTab = 'java'">Java版下载</button>
            <button class="vp-tab" :class="{ active: vpTab === 'bedrock' }" @click="vpSwitchBedrock">基岩版下载</button>
            <button class="vp-tab" :class="{ active: vpTab === 'installed' }" @click="vpSwitchInstalled">已安装版本</button>
          </div>
          <div style="flex:1;min-height:0;overflow-y:auto;">
            <div class="vp-intro">
              <div class="vp-intro-title">📦 游戏版本下载</div>
              <div class="vp-intro-desc">选择您想要下载的Minecraft游戏版本，支持最新版本和历史版本</div>
            </div>
            <div v-if="vpTab === 'java'">
              <!-- 加载动画 -->
              <div v-if="vpLoading" class="vp-loading">
                <div class="vp-loading-spinner"></div>
                <span>正在加载版本列表...</span>
              </div>

              <!-- 顶部：最新版本区 -->
              <div v-if="!vpLoading && !vpVersionType && !vpSearchText" class="vp-latest-section">
                <div class="vp-latest-header">
                  <span class="vp-latest-header-icon">⭐</span>
                  <span class="vp-latest-header-title">最新版本</span>
                </div>
                <div class="vp-latest-grid">
                  <div class="vp-latest-card" v-for="v in vpLatestTwo" :key="'top-'+v.id" :style="{ borderColor: v.typeColor }">
                    <img class="vp-latest-card-icon" :src="'icons/versions/' + vpIconFor(v)" :alt="v.id" />
                    <div class="vp-latest-card-info">
                      <div class="vp-latest-card-name">{{ v.id }}</div>
                      <div class="vp-latest-card-type" :style="{ color: v.typeColor }">{{ v.type === 'release' ? '正式版' : v.type === 'snapshot' ? '预览版' : v.type }}</div>
                      <div class="vp-latest-card-date">📅 {{ v.date }}</div>
                    </div>
                    <button class="vp-latest-card-btn" @click="vpInstall(v)" :disabled="v.installing">
                      {{ vpDownloadBtnTextLg(v) }}
                    </button>
                    <button v-if="v.installing" class="vp-inst-cancel" @click.stop="vpCancelInstall(v.id)">取消</button>
                  </div>
                </div>
              </div>

              <!-- 下方：三个可折叠分组 -->
              <div v-if="!vpLoading" class="vp-folders">
                <div v-if="vpGroupRelease.length > 0" class="vp-folder">
                  <div class="vp-folder-header" @click="vpToggleGroup('release')">
                    <span class="vp-folder-icon">📦</span>
                    <span class="vp-folder-title">正式版</span>
                    <span class="vp-folder-count">{{ vpGroupRelease.length }} 个</span>
                    <span class="vp-folder-arrow" :class="{ open: vpGroupOpen.release }">▼</span>
                  </div>
                  <div v-show="vpGroupOpen.release" class="vp-folder-body">
                    <div class="vp-inst-item" v-for="v in vpGroupRelease" :key="'rel-'+v.id">
                      <img class="vp-inst-icon" :src="'icons/versions/' + vpIconFor(v)" :alt="v.id" />
                      <div class="vp-inst-info">
                        <div class="vp-inst-name-row">
                          <span class="vp-inst-name">{{ v.id }}</span>
                          <span class="vp-inst-tag" :style="{ background: v.typeColor || '#2E7D32' }">{{ v.type }}</span>
                        </div>
                        <div class="vp-inst-date">{{ v.date }}</div>
                      </div>
                      <div class="vp-inst-actions">
                        <button class="vp-latest-btn" @click="vpInstall(v)" :disabled="v.installing">{{ vpDownloadBtnText(v) }}</button>
                        <button v-if="v.installing" class="vp-inst-cancel" @click.stop="vpCancelInstall(v.id)">取消</button>
                      </div>
                    </div>
                  </div>
                </div>

                <div v-if="vpGroupSnapshot.length > 0" class="vp-folder">
                  <div class="vp-folder-header" @click="vpToggleGroup('snapshot')">
                    <span class="vp-folder-icon">🧪</span>
                    <span class="vp-folder-title">预览版</span>
                    <span class="vp-folder-count">{{ vpGroupSnapshot.length }} 个</span>
                    <span class="vp-folder-arrow" :class="{ open: vpGroupOpen.snapshot }">▼</span>
                  </div>
                  <div v-show="vpGroupOpen.snapshot" class="vp-folder-body">
                    <div class="vp-inst-item" v-for="v in vpGroupSnapshot" :key="'snap-'+v.id">
                      <img class="vp-inst-icon" :src="'icons/versions/' + vpIconFor(v)" :alt="v.id" />
                      <div class="vp-inst-info">
                        <div class="vp-inst-name-row">
                          <span class="vp-inst-name">{{ v.id }}</span>
                          <span class="vp-inst-tag" :style="{ background: v.typeColor || '#FF6F00' }">{{ v.type }}</span>
                        </div>
                        <div class="vp-inst-date">{{ v.date }}</div>
                      </div>
                      <div class="vp-inst-actions">
                        <button class="vp-latest-btn" @click="vpInstall(v)" :disabled="v.installing">{{ vpDownloadBtnText(v) }}</button>
                        <button v-if="v.installing" class="vp-inst-cancel" @click.stop="vpCancelInstall(v.id)">取消</button>
                      </div>
                    </div>
                  </div>
                </div>

                <div v-if="vpGroupAncient.length > 0" class="vp-folder">
                  <div class="vp-folder-header" @click="vpToggleGroup('ancient')">
                    <span class="vp-folder-icon">📜</span>
                    <span class="vp-folder-title">远古版</span>
                    <span class="vp-folder-count">{{ vpGroupAncient.length }} 个</span>
                    <span class="vp-folder-arrow" :class="{ open: vpGroupOpen.ancient }">▼</span>
                  </div>
                  <div v-show="vpGroupOpen.ancient" class="vp-folder-body">
                    <div class="vp-inst-item" v-for="v in vpGroupAncient" :key="'anc-'+v.id">
                      <img class="vp-inst-icon" :src="'icons/versions/' + vpIconFor(v)" :alt="v.id" />
                      <div class="vp-inst-info">
                        <div class="vp-inst-name-row">
                          <span class="vp-inst-name">{{ v.id }}</span>
                          <span class="vp-inst-tag" :style="{ background: v.typeColor || '#5D4037' }">{{ v.type }}</span>
                        </div>
                        <div class="vp-inst-date">{{ v.date }}</div>
                      </div>
                      <div class="vp-inst-actions">
                        <button class="vp-latest-btn" @click="vpInstall(v)" :disabled="v.installing">{{ vpDownloadBtnText(v) }}</button>
                        <button v-if="v.installing" class="vp-inst-cancel" @click.stop="vpCancelInstall(v.id)">取消</button>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <div v-if="!vpLoading && vpFilteredVersions.length === 0" class="vp-empty">
                <span class="vp-empty-icon">📭</span><span>暂无匹配版本</span>
              </div>

            </div>
            <div v-if="vpTab === 'bedrock'" style="margin-top:12px;">
              <div v-if="vpBedrockLoading" class="vp-loading">
                <div class="vp-loading-spinner"></div>
                <span>正在加载基岩版版本列表...</span>
              </div>

              <!-- 最新版本区 -->
              <div v-if="!vpBedrockLoading && !vpSearchText && vpBedrockLatest.length > 0" class="vp-latest-section">
                <div class="vp-latest-header">
                  <span class="vp-latest-header-icon">🪨</span>
                  <span class="vp-latest-header-title">最新版本</span>
                  <button class="vp-btn-reset" @click="vpRefreshBedrock" style="margin-left:auto;font-size:11px;">🔄 刷新</button>
                </div>
                <div class="vp-latest-grid">
                  <div class="vp-latest-card" v-for="v in vpBedrockLatest" :key="'bl-'+v.id" :style="{ borderColor: v.typeColor }">
                    <img class="vp-latest-card-icon" :src="'icons/versions/' + vpIconFor(v)" :alt="v.id" />
                    <div class="vp-latest-card-info">
                      <div class="vp-latest-card-name">{{ v.version || v.id }}</div>
                      <div class="vp-latest-card-type" :style="{ color: v.typeColor }">{{ v.typeDisplay || v.type }}</div>
                      <div class="vp-latest-card-date">📅 {{ v.releaseTime || v.desc }}</div>
                    </div>
                    <button class="vp-latest-card-btn" @click="vpInstallBedrock(v)" :disabled="vpBedrockInstalling[v.id]">
                      {{ vpBedrockInstalling[v.id] ? '下载中...' : v.isDownloaded ? '已下载' : '📥 下载' }}
                    </button>
                  </div>
                </div>
              </div>

              <!-- 可折叠分组 -->
              <div v-if="!vpBedrockLoading" class="vp-folders">
                <div v-if="vpBedrockGroupRelease.length > 0" class="vp-folder">
                  <div class="vp-folder-header" @click="vpToggleBedrockGroup('release')">
                    <span class="vp-folder-icon">📦</span>
                    <span class="vp-folder-title">正式版</span>
                    <span class="vp-folder-count">{{ vpBedrockGroupRelease.length }} 个</span>
                    <span class="vp-folder-arrow" :class="{ open: vpBedrockGroupOpen.release }">▼</span>
                  </div>
                  <div v-show="vpBedrockGroupOpen.release" class="vp-folder-body">
                    <div class="vp-inst-item" v-for="v in vpBedrockGroupRelease" :key="'br-'+v.id">
                      <img class="vp-inst-icon" :src="'icons/versions/' + vpIconFor(v)" :alt="v.id" />
                      <div class="vp-inst-info">
                        <div class="vp-inst-name-row">
                          <span class="vp-inst-name">{{ v.version || v.id }}</span>
                          <span class="vp-inst-tag" :style="{ background: v.typeColor || '#2E7D32' }">{{ v.typeDisplay || v.type }}</span>
                          <span v-if="v.source === 'mcapks'" class="vp-inst-tag" style="background:#FF9800;font-size:9px;">{{ v.sourceDisplay }}</span>
                        </div>
                        <div class="vp-inst-date">{{ v.releaseTime || v.desc }}</div>
                      </div>
                      <div class="vp-inst-actions">
                        <button class="vp-latest-btn" @click="vpInstallBedrock(v)" :disabled="vpBedrockInstalling[v.id]">{{ vpBedrockInstalling[v.id] ? '下载中...' : v.isDownloaded ? '已下载' : '下载' }}</button>
                      </div>
                    </div>
                  </div>
                </div>
                <div v-if="vpBedrockGroupPreview.length > 0" class="vp-folder">
                  <div class="vp-folder-header" @click="vpToggleBedrockGroup('preview')">
                    <span class="vp-folder-icon">🧪</span>
                    <span class="vp-folder-title">预览版</span>
                    <span class="vp-folder-count">{{ vpBedrockGroupPreview.length }} 个</span>
                    <span class="vp-folder-arrow" :class="{ open: vpBedrockGroupOpen.preview }">▼</span>
                  </div>
                  <div v-show="vpBedrockGroupOpen.preview" class="vp-folder-body">
                    <div class="vp-inst-item" v-for="v in vpBedrockGroupPreview" :key="'bp-'+v.id">
                      <img class="vp-inst-icon" :src="'icons/versions/' + vpIconFor(v)" :alt="v.id" />
                      <div class="vp-inst-info">
                        <div class="vp-inst-name-row">
                          <span class="vp-inst-name">{{ v.version || v.id }}</span>
                          <span class="vp-inst-tag" :style="{ background: v.typeColor || '#FF6F00' }">{{ v.typeDisplay || v.type }}</span>
                          <span v-if="v.source === 'mcapks'" class="vp-inst-tag" style="background:#FF9800;font-size:9px;">{{ v.sourceDisplay }}</span>
                        </div>
                        <div class="vp-inst-date">{{ v.releaseTime || v.desc }}</div>
                      </div>
                      <div class="vp-inst-actions">
                        <button class="vp-latest-btn" @click="vpInstallBedrock(v)" :disabled="vpBedrockInstalling[v.id]">{{ vpBedrockInstalling[v.id] ? '下载中...' : v.isDownloaded ? '已下载' : '下载' }}</button>
                      </div>
                    </div>
                  </div>
                </div>
                <div v-if="vpBedrockGroupOther.length > 0" class="vp-folder">
                  <div class="vp-folder-header" @click="vpToggleBedrockGroup('other')">
                    <span class="vp-folder-icon">📜</span>
                    <span class="vp-folder-title">其他版本</span>
                    <span class="vp-folder-count">{{ vpBedrockGroupOther.length }} 个</span>
                    <span class="vp-folder-arrow" :class="{ open: vpBedrockGroupOpen.other }">▼</span>
                  </div>
                  <div v-show="vpBedrockGroupOpen.other" class="vp-folder-body">
                    <div class="vp-inst-item" v-for="v in vpBedrockGroupOther" :key="'bo-'+v.id">
                      <img class="vp-inst-icon" :src="'icons/versions/' + vpIconFor(v)" :alt="v.id" />
                      <div class="vp-inst-info">
                        <div class="vp-inst-name-row">
                          <span class="vp-inst-name">{{ v.version || v.id }}</span>
                          <span class="vp-inst-tag" :style="{ background: v.typeColor || '#6A1B9A' }">{{ v.typeDisplay || v.type }}</span>
                          <span v-if="v.source === 'mcapks'" class="vp-inst-tag" style="background:#FF9800;font-size:9px;">{{ v.sourceDisplay }}</span>
                        </div>
                        <div class="vp-inst-date">{{ v.releaseTime || v.desc }}</div>
                      </div>
                      <div class="vp-inst-actions">
                        <button class="vp-latest-btn" @click="vpInstallBedrock(v)" :disabled="vpBedrockInstalling[v.id]">{{ vpBedrockInstalling[v.id] ? '下载中...' : v.isDownloaded ? '已下载' : '下载' }}</button>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <div v-if="!vpBedrockLoading && vpBedrockFiltered.length === 0" class="vp-empty">
                <span class="vp-empty-icon">📭</span><span>暂无基岩版版本数据，请检查网络后重试</span>
              </div>
            </div>
            </div>
            <div v-if="vpTab === 'installed'">
              <div class="version-tabs" style="margin-bottom:12px;">
                <div class="vtab" :class="{ active: vpInstalledType === 'java' }" @click="vpInstalledType = 'java'">
                  <span class="vt-icon">☕</span><span>Java版</span>
                </div>
                <div class="vtab" :class="{ active: vpInstalledType === 'bedrock' }" @click="vpInstalledType = 'bedrock'">
                  <span class="vt-icon">🪨</span><span>基岩版</span>
                </div>
              </div>
              <template v-if="vpInstalledType === 'java'">
                <div v-if="vpInstalled.length > 0">
                  <div class="vp-inst-item" v-for="v in vpInstalled" :key="'inst-'+v.id">
                    <img class="vp-inst-icon" :src="'icons/versions/' + vpIconFor(v)" :alt="v.id" />
                    <div class="vp-inst-info">
                      <div class="vp-inst-name-row">
                        <span class="vp-inst-name">{{ v.id }}</span>
                        <span class="vp-inst-tag" :style="{ background: v.typeColor || '#2E7D32' }">{{ v.type }}</span>
                      </div>
                      <div class="vp-inst-date">{{ v.date }}</div>
                    </div>
                    <div class="vp-inst-actions">
                      <button class="vp-inst-launch" :data-vp-id="v.id" @click="vpDoLaunch(v.id)">启动</button>
                      <button class="vp-inst-repair" :data-vp-id="v.id" @click="vpDoRepair(v.id)">修复</button>
                      <button class="vp-inst-delete" :data-vp-id="v.id" @click="vpDoDelete(v.id)">删除</button>
                    </div>
                  </div>
                </div>
                <div v-else class="vp-empty"><span class="vp-empty-icon">📭</span><span>暂无已安装Java版</span></div>
              </template>
              <template v-if="vpInstalledType === 'bedrock'">
                <div v-if="vpInstalledBedrock.length > 0">
                  <div class="vp-inst-item" v-for="v in vpInstalledBedrock" :key="'inst-be-'+v.id">
                    <img class="vp-inst-icon" :src="'icons/versions/' + vpIconFor(v)" :alt="v.id" />
                    <div class="vp-inst-info">
                      <div class="vp-inst-name-row">
                        <span class="vp-inst-name">{{ v.id }}</span>
                        <span class="vp-inst-tag" :style="{ background: v.typeColor || '#1565C0' }">{{ v.type || '基岩版' }}</span>
                      </div>
                      <div class="vp-inst-date">{{ v.date }}</div>
                    </div>
                    <div class="vp-inst-actions">
                      <button class="vp-inst-launch" :data-vp-id="v.id" @click="vpDoLaunch(v.id)">启动</button>
                      <button class="vp-inst-delete" :data-vp-id="v.id" @click="vpDoDelete(v.id)">删除</button>
                    </div>
                  </div>
                </div>
                <div v-else class="vp-empty"><span class="vp-empty-icon">📭</span><span>暂无已安装基岩版</span></div>
              </template>
            </div>
          </div>
        </div>

        <!-- 加载器选择弹窗（对齐 WPF LoaderSelectionPage） -->
        <div class="modal-overlay" v-if="vpLoaderShow" @click.self="vpLoaderShow = false">
          <div class="loader-select-dialog" style="background:var(--card-bg);border:1px solid var(--border);border-radius:12px;padding:0;max-width:520px;width:90vw;">
            <!-- 头部 -->
            <div style="display:flex;align-items:center;justify-content:space-between;padding:16px 20px;border-bottom:1px solid var(--border);">
              <div>
                <div style="font-weight:700;font-size:16px;color:var(--text-primary);">🔧 选择模组加载器</div>
                <div style="font-size:12px;color:var(--text-secondary);margin-top:2px;">目标版本: {{ vpLoaderTarget ? vpLoaderTarget.id : '' }}</div>
              </div>
              <button @click="vpLoaderShow = false" style="background:none;border:none;font-size:20px;color:var(--text-secondary);cursor:pointer;">✕</button>
            </div>
            <!-- 加载器卡片列表 -->
            <div style="padding:16px 20px;display:flex;flex-direction:column;gap:8px;">
              <div class="loader-card forge" :class="{ selected: vpSelectedLoaders.includes('Forge') }" @click="vpLoaderToggle('Forge')">
                <img class="loader-card-icon" src="img/Anvil.png" alt="Forge" @error="$event.target.style.display='none'" />
                <div class="loader-card-info">
                  <div class="loader-card-name">Forge</div>
                  <div class="loader-card-desc">最流行的 Mod 加载器，兼容性最好</div>
                </div>
                <div class="loader-card-check" v-if="vpSelectedLoaders.includes('Forge')">✓</div>
              </div>
              <div class="loader-card fabric" :class="{ selected: vpSelectedLoaders.includes('Fabric') }" @click="vpLoaderToggle('Fabric')">
                <img class="loader-card-icon" src="img/Fabric.png" alt="Fabric" @error="$event.target.style.display='none'" />
                <div class="loader-card-info">
                  <div class="loader-card-name">Fabric</div>
                  <div class="loader-card-desc">轻量级加载器，启动更快，Mod 更小</div>
                </div>
                <div class="loader-card-check" v-if="vpSelectedLoaders.includes('Fabric')">✓</div>
              </div>
              <div class="loader-card neoforge" :class="{ selected: vpSelectedLoaders.includes('NeoForge') }" @click="vpLoaderToggle('NeoForge')">
                <img class="loader-card-icon" src="img/NeoForge.png" alt="NeoForge" @error="$event.target.style.display='none'" />
                <div class="loader-card-info">
                  <div class="loader-card-name">NeoForge</div>
                  <div class="loader-card-desc">Forge 的现代分支，支持最新游戏版本</div>
                </div>
                <div class="loader-card-check" v-if="vpSelectedLoaders.includes('NeoForge')">✓</div>
              </div>
              <div class="loader-card optifine" :class="{ selected: vpSelectedLoaders.includes('OptiFine') }" @click="vpLoaderToggle('OptiFine')">
                <img class="loader-card-icon" src="img/GrassPath.png" alt="OptiFine" @error="$event.target.style.display='none'" />
                <div class="loader-card-info">
                  <div class="loader-card-name">OptiFine</div>
                  <div class="loader-card-desc">性能优化与光影支持，提升帧率与画质</div>
                </div>
                <div class="loader-card-check" v-if="vpSelectedLoaders.includes('OptiFine')">✓</div>
              </div>
              <div class="loader-card vanilla" :class="{ selected: vpSelectedLoaders.includes('Vanilla') }" @click="vpLoaderToggle('Vanilla')">
                <div class="loader-card-icon-box">🧱</div>
                <div class="loader-card-info">
                  <div class="loader-card-name">原版（不安装加载器）</div>
                  <div class="loader-card-desc">纯净 Minecraft，不安装任何模组加载器</div>
                </div>
                <div class="loader-card-check" v-if="vpSelectedLoaders.includes('Vanilla')">✓</div>
              </div>
            </div>
            <!-- 提示 -->
            <div style="padding:0 20px 12px;font-size:12px;color:var(--text-muted);">💡 可同时选择多个加载器一起安装</div>
            <!-- 底部按钮 -->
            <div style="display:flex;gap:10px;justify-content:flex-end;padding:16px 20px;border-top:1px solid var(--border);">
              <button class="btn btn-ghost" @click="vpLoaderShow = false" style="padding:8px 20px;border:1px solid var(--border);border-radius:6px;background:transparent;color:var(--text-primary);cursor:pointer;">取消</button>
              <button class="btn btn-primary" @click="vpLoaderConfirm" style="padding:8px 24px;border:none;border-radius:6px;background:#2196F3;color:#fff;cursor:pointer;">开始下载</button>
            </div>
          </div>
        </div>
      </div>

      <!-- 版本命名对话框 -->
      <div class="modal-overlay" v-if="vpNameShow" @click.self="vpNameShow = false">
        <div style="background:var(--card-bg);border:1px solid var(--border);border-radius:12px;padding:0;max-width:420px;width:90vw;">
          <!-- 头部 -->
          <div style="display:flex;align-items:center;justify-content:space-between;padding:16px 20px;border-bottom:1px solid var(--border);">
            <div>
              <div style="font-weight:700;font-size:16px;color:var(--text-primary);">📦 版本命名</div>
              <div style="font-size:12px;color:var(--text-secondary);margin-top:2px;">设置版本目录名称，留空则使用默认 ID</div>
            </div>
            <button @click="vpNameShow = false" style="background:none;border:none;font-size:20px;color:var(--text-secondary);cursor:pointer;">✕</button>
          </div>
          <!-- 内容 -->
          <div style="padding:20px;">
            <div style="font-size:13px;color:var(--text-secondary);margin-bottom:4px;">原始版本 ID</div>
            <div style="font-size:14px;color:var(--text-primary);font-weight:600;margin-bottom:16px;padding:8px 12px;background:var(--hover-bg);border-radius:6px;">{{ vpLoaderTarget ? vpLoaderTarget.id : '' }}</div>
            <div style="font-size:13px;color:var(--text-secondary);margin-bottom:4px;">版本名称</div>
            <input type="text" v-model="vpNameInput" @input="vpCheckName" :placeholder="vpLoaderTarget ? vpLoaderTarget.id : ''"
              style="width:100%;padding:10px 12px;border:1px solid var(--border);border-radius:6px;background:var(--input-bg);color:var(--text-primary);font-size:14px;outline:none;box-sizing:border-box;" />
            <div v-if="vpNameError" style="margin-top:8px;font-size:12px;color:#E53935;">⚠️ {{ vpNameError }}</div>
          </div>
          <!-- 底部按钮 -->
          <div style="display:flex;gap:10px;justify-content:flex-end;padding:16px 20px;border-top:1px solid var(--border);">
            <button @click="vpNameShow = false" style="padding:8px 20px;border:1px solid var(--border);border-radius:6px;background:transparent;color:var(--text-primary);cursor:pointer;font-size:14px;">取消</button>
            <button @click="vpNameConfirm" :disabled="!!vpNameError" :style="{padding:'8px 24px',border:'none',borderRadius:'6px',background:vpNameError ? '#666' : '#2196F3',color:'#fff',cursor:vpNameError ? 'not-allowed' : 'pointer',fontSize:'14px'}">确认下载</button>
          </div>
        </div>
      </div>

      <!-- 删除确认对话框 -->
      <div class="modal-overlay" v-if="confirmShow" @click.self="confirmShow = false">
        <div style="background:var(--card-bg);border:1px solid var(--border);border-radius:12px;padding:0;max-width:420px;width:90vw;">
          <!-- 头部 -->
          <div style="display:flex;align-items:center;justify-content:space-between;padding:16px 20px;border-bottom:1px solid var(--border);">
            <div>
              <div style="font-weight:700;font-size:16px;color:var(--text-primary);">⚠️ {{ confirmTitle }}</div>
              <div style="font-size:12px;color:var(--text-secondary);margin-top:2px;">此操作不可撤销，请确认</div>
            </div>
            <button @click="confirmShow = false" style="background:none;border:none;font-size:20px;color:var(--text-secondary);cursor:pointer;">✕</button>
          </div>
          <!-- 内容 -->
          <div style="padding:20px;">
            <div style="font-size:14px;color:var(--text-primary);line-height:1.6;">{{ confirmMsg }}</div>
          </div>
          <!-- 底部按钮 -->
          <div style="display:flex;gap:10px;justify-content:flex-end;padding:16px 20px;border-top:1px solid var(--border);">
            <button @click="confirmShow = false" style="padding:8px 20px;border:1px solid var(--border);border-radius:6px;background:transparent;color:var(--text-primary);cursor:pointer;font-size:14px;">取消</button>
            <button @click="doConfirm(); confirmShow = false" style="padding:8px 24px;border:none;border-radius:6px;background:#E53935;color:#fff;cursor:pointer;font-size:14px;">确认删除</button>
          </div>
        </div>
      </div>

      <!-- Java 兼容性对话框（参考 PCL ModJava.vb） -->
      <div class="modal-overlay" v-if="javaCheckShow" @click.self="javaCheckShow = false">
        <div style="background:var(--card-bg);border:1px solid var(--border);border-radius:12px;padding:0;max-width:500px;width:90vw;">
          <!-- 头部 -->
          <div style="display:flex;align-items:center;justify-content:space-between;padding:16px 20px;border-bottom:1px solid var(--border);">
            <div>
              <div style="font-weight:700;font-size:16px;color:var(--text-primary);">⚠️ Java 兼容性警告</div>
              <div style="font-size:12px;color:var(--text-secondary);margin-top:2px;">当前 Java 版本与游戏版本不匹配</div>
            </div>
            <button @click="javaCheckShow = false" style="background:none;border:none;font-size:20px;color:var(--text-secondary);cursor:pointer;">✕</button>
          </div>
          <!-- 内容 -->
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
            <!-- 可选 Java 列表 -->
            <div v-if="javaCheckResult.alternatives && javaCheckResult.alternatives.length > 0" style="margin-bottom:12px;">
              <div style="font-size:12px;color:var(--text-secondary);margin-bottom:6px;">已检测到的 Java 版本：</div>
              <div v-for="alt in javaCheckResult.alternatives.slice(0, 6)" :key="alt.path"
                :style="{ padding:'4px 8px', fontSize:'12px', borderRadius:'4px', marginBottom:'2px',
                  background: alt.compatible ? 'rgba(39,174,96,0.1)' : 'rgba(229,57,53,0.05)',
                  color: alt.compatible ? '#27AE60' : 'var(--text-secondary)' }">
                {{ alt.compatible ? '✅' : '❌' }} Java {{ alt.majorVersion }}
                <span v-if="alt.isCurrent" style="font-weight:600;">(当前)</span>
                <span style="opacity:0.5;font-size:10px;margin-left:8px;">{{ alt.path.split(/[\\/\\\\]/).pop() || alt.path }}</span>
              </div>
            </div>
          </div>
          <!-- 底部按钮 -->
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

      <!-- ═══ 子视图：整合包/Mod/光影/数据包 ═══ -->
      <div v-if="currentTab === 'packs' || currentTab === 'mods' || currentTab === 'shaders' || currentTab === 'datapacks' || currentTab === 'domestic-packs' || currentTab === 'bbsmc'" class="dl-cat-page">
        <div class="vp-header">
          <button class="vp-back-btn" @click="switchTab('grid')">&larr; 返回</button>
          <span class="dl-cat-icon-box" :style="{ background: catConfig.color }">{{ catConfig.icon }}</span>
          <span class="vp-header-title">{{ catConfig.title }}</span>
        </div>
        <div style="flex:1;margin:10px 14px;display:flex;flex-direction:column;min-height:0;overflow:hidden;">
          <!-- 信息横幅 -->
          <div class="dl-cat-banner">
            <div class="dl-cat-banner-bar" :style="{ background: catConfig.color }"></div>
            <div class="dl-cat-banner-body">
              <div class="dl-cat-banner-title">{{ catConfig.icon + ' ' + catConfig.title }}下载</div>
              <div class="dl-cat-banner-desc">{{ catConfig.desc }}</div>
            </div>
          </div>
          <!-- 筛选面板 -->
          <div class="dl-cat-filter">
            <div class="dl-cat-filter-row">
              <span class="dl-cat-filter-label">名称</span>
              <div class="dl-cat-search-wrap">
                <input type="text" v-model="catSearch" :placeholder="catConfig.placeholder" @keyup.enter="catSearchGo" />
              </div>
              <span class="dl-cat-filter-label">来源</span>
              <div class="mnl-select" :class="{ open: catSourceOpen }" v-click-outside="() => catSourceOpen = false">
                <div class="mnl-select-trigger" @click="catSourceOpen = !catSourceOpen">
                  <span class="mnl-select-text">{{ catSourceDisplayText }}</span>
                  <span class="mnl-select-arrow">▾</span>
                </div>
                <div class="mnl-select-menu" v-if="catSourceOpen">
                  <div class="mnl-select-item" v-for="o in catConfig.sources" :key="o.v"
                    :class="{ active: catSource === o.v }"
                    @click="catSource = o.v; catSourceOpen = false; catSearchGo()">
                    {{ o.t }}
                  </div>
                </div>
              </div>
              <span class="dl-cat-filter-label">版本</span>
              <div class="mnl-select" :class="{ open: catVersionOpen }" v-click-outside="() => catVersionOpen = false">
                <div class="mnl-select-trigger" @click="catVersionOpen = !catVersionOpen">
                  <span class="mnl-select-text">{{ catVersionDisplayText }}</span>
                  <span class="mnl-select-arrow">▾</span>
                </div>
                <div class="mnl-select-menu" v-if="catVersionOpen">
                  <div class="mnl-select-item" v-for="o in catConfig.versions" :key="o.v"
                    :class="{ active: catVersion === o.v }"
                    @click="catVersion = o.v; catVersionOpen = false; catSearchGo()">
                    {{ o.t }}
                  </div>
                </div>
              </div>
              <span class="dl-cat-filter-label">类型</span>
              <div class="mnl-select" :class="{ open: catTypeOpen }" v-click-outside="() => catTypeOpen = false">
                <div class="mnl-select-trigger" @click="catTypeOpen = !catTypeOpen">
                  <span class="mnl-select-text">{{ catTypeDisplayText }}</span>
                  <span class="mnl-select-arrow">▾</span>
                </div>
                <div class="mnl-select-menu" v-if="catTypeOpen">
                  <div class="mnl-select-item" v-for="o in catConfig.types" :key="o.v"
                    :class="{ active: catType === o.v }"
                    @click="catType = o.v; catTypeOpen = false; catSearchGo()">
                    {{ o.t }}
                  </div>
                </div>
              </div>
            </div>
            <div class="dl-cat-filter-actions">
              <button class="dl-cat-btn-search" :style="{ background: catConfig.color }" @click="catSearchGo">搜索</button>
              <button class="dl-cat-btn-reset" @click="catReset">重置条件</button>
            </div>
          </div>
          <!-- 列表 -->
          <div class="dl-cat-list" style="flex:1;min-height:0;overflow-y:auto;">
            <div class="dl-cat-loading" v-if="catLoading">⏳ 加载中...</div>
            <div class="dl-cat-empty" v-else-if="catItems.length === 0">暂无匹配结果</div>
            <div class="dl-cat-item" v-for="item in catItems" :key="item.projectId + item.name">
              <img class="dl-cat-item-icon" :src="item.iconUrl" :alt="item.name" @error="catIconError" />
              <div class="dl-cat-item-info">
                <div style="display:flex;align-items:center;gap:8px;">
                  <span class="dl-cat-item-name">{{ item.name }}</span>
                  <span class="dl-cat-item-tag" :style="{ background: catConfig.color }">{{ item.version || 'Latest' }}</span>
                </div>
                <div class="dl-cat-item-desc">{{ item.description || '暂无描述' }}</div>
                <div class="dl-cat-item-meta">
                  <span>{{ item.author || '未知作者' }}</span>
                  <span>·</span>
                  <span>⬇ {{ item.downloads || '0' }}</span>
                </div>
              </div>
              <button class="dl-cat-item-btn" :style="{ background: catConfig.color }" @click="catDownload(item)">下载</button>
            </div>
          </div>
          <!-- 分页 -->
          <div class="dl-cat-pager" v-if="catTotalPages > 1">
            <button @click="catPageGo(1)" :disabled="catPage <= 1">首页</button>
            <button @click="catPageGo(catPage - 1)" :disabled="catPage <= 1">◂</button>
            <span class="dl-cat-pager-info">第 <input type="number" v-model.number="catPageInput" @keyup.enter="catPageGo(catPageInput)" class="dl-cat-pager-input" min="1" :max="catTotalPages" /> / {{ catTotalPages }} 页</span>
            <button @click="catPageGo(catPage + 1)" :disabled="catPage >= catTotalPages">▸</button>
          </div>
        </div>

        <!-- ═══ 下载确认弹窗（版本选择 + 路径 + 文件名） ═══ -->
        <div class="modal-overlay" v-if="dlShow" @click.self="dlShow = false">
          <div class="download-confirm-dialog" style="background:var(--card-bg);border:1px solid var(--border);border-radius:12px;padding:0;max-width:520px;width:90vw;">
            <!-- 头部 -->
            <div class="dl-confirm-header" style="display:flex;align-items:center;justify-content:space-between;padding:16px 20px;border-bottom:1px solid var(--border);">
              <span style="font-weight:700;font-size:16px;color:var(--text-primary);">📥 下载确认</span>
              <button @click="dlShow = false" style="background:none;border:none;font-size:20px;color:var(--text-secondary);cursor:pointer;">✕</button>
            </div>
            <!-- 内容 -->
            <div style="padding:20px;display:flex;flex-direction:column;gap:16px;">
              <!-- 项目名 -->
              <div style="display:flex;align-items:center;gap:10px;">
                <img v-if="dlItemIcon" :src="dlItemIcon" style="width:40px;height:40px;border-radius:6px;object-fit:contain;" />
                <div>
                  <div style="font-weight:600;font-size:15px;color:var(--text-primary);">{{ dlItemName }}</div>
                  <div style="font-size:12px;color:var(--text-secondary);">{{ dlItemTypeName }}</div>
                </div>
              </div>

              <!-- 游戏版本选择（对齐 WPF DownloadConfirmDialog 的版本选择 ComboBox） -->
              <div v-if="dlInstalledMcVersions.length > 0">
                <div style="font-size:13px;font-weight:600;color:var(--text-primary);margin-bottom:6px;">为哪个版本安装</div>
                <div class="dl-dropdown" :class="{ open: dlMcDropdownOpen }" style="position:relative;">
                  <div class="dl-dropdown-trigger" @click="dlMcDropdownOpen = !dlMcDropdownOpen">
                    <span class="dl-dropdown-text">{{ dlMcVersionLabel }}</span>
                    <span class="dl-dropdown-arrow">▾</span>
                  </div>
                  <div class="dl-dropdown-menu" v-if="dlMcDropdownOpen" style="position:absolute;top:calc(100%+4px);left:0;right:0;background:var(--bg-secondary);border:1px solid var(--border);border-radius:8px;box-shadow:0 8px 24px rgba(0,0,0,0.3);z-index:100;max-height:220px;overflow-y:auto;padding:4px;">
                    <div class="dl-dropdown-item" v-for="(v, i) in dlInstalledMcVersions" :key="'mcv-'+i" :class="{ active: i === dlMcVersionIdx }" @click="selectDlMcVersion(i)">
                      <span class="dl-dd-name">{{ v }}</span>
                    </div>
                    <div class="dl-dropdown-item" :class="{ active: dlMcVersionIdx === -1 }" @click="selectDlMcVersion(-1)" style="color:#4CAF50;">
                      <span class="dl-dd-name">📂 全局 mods 目录</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- 模组版本选择 -->
              <div>
                <div style="font-size:13px;font-weight:600;color:var(--text-primary);margin-bottom:6px;">选择版本</div>
                <div class="dl-dropdown" :class="{ open: dlDropdownOpen }" style="position:relative;">
                  <div class="dl-dropdown-trigger" @click="dlDropdownOpen = !dlDropdownOpen">
                    <span class="dl-dropdown-text">{{ dlSelectedVersionName }}</span>
                    <span class="dl-dropdown-arrow">▾</span>
                  </div>
                  <div class="dl-dropdown-menu" v-if="dlDropdownOpen" style="position:absolute;top:calc(100%+4px);left:0;right:0;background:var(--bg-secondary);border:1px solid var(--border);border-radius:8px;box-shadow:0 8px 24px rgba(0,0,0,0.3);z-index:100;max-height:220px;overflow-y:auto;padding:4px;">
                    <div class="dl-dropdown-item" v-for="(v, i) in dlVersions" :key="i" :class="{ active: i === dlVersionIdx, recommended: v.isRecommended }" @click="selectDlVersion(i)">
                      <span class="dl-dd-name">{{ v.isBadDisplay ? v.fileName : v.versionName }}</span>
                      <span class="dl-dd-info" v-if="!v.isBadDisplay && v.displayInfo" style="font-size:11px;color:var(--text-muted);">{{ v.displayInfo }}</span>
                      <span class="dl-dd-tag" v-if="v.isRecommended" style="padding:1px 6px;background:rgba(76,175,80,0.2);color:#4CAF50;border-radius:3px;font-size:10px;">推荐</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- 安装位置 -->
              <div>
                <div style="font-size:13px;font-weight:600;color:var(--text-primary);margin-bottom:6px;">安装位置</div>
                <div style="display:flex;gap:8px;flex-wrap:wrap;">
                  <button class="mm-tab-btn" :class="{ active: dlDirMode === 'auto' }" @click="setDlDirMode('auto')" style="padding:6px 14px;">版本隔离</button>
                  <button class="mm-tab-btn" :class="{ active: dlDirMode === 'global' }" @click="setDlDirMode('global')" style="padding:6px 14px;">全局 mods</button>
                </div>
              </div>

              <!-- 文件名 -->
              <div>
                <div style="font-size:13px;font-weight:600;color:var(--text-primary);margin-bottom:6px;">文件名</div>
                <input type="text" v-model="dlFileName" @input="updateDlPreviewPath()" style="width:100%;padding:9px 12px;background:var(--bg-secondary);border:1px solid var(--border);border-radius:8px;color:var(--text-primary);font-size:13px;box-sizing:border-box;" />
              </div>

              <!-- 路径预览 -->
              <div style="padding:10px 14px;background:var(--bg-secondary);border-radius:8px;font-size:12px;color:var(--text-secondary);word-break:break-all;">📁 {{ dlPreviewPath }}</div>

              <!-- 加载状态 -->
              <div v-if="dlLoading" style="text-align:center;color:var(--text-secondary);">⏳ 正在获取版本信息...</div>
            </div>
            <!-- 底部按钮 -->
            <div style="display:flex;gap:10px;justify-content:flex-end;padding:16px 20px;border-top:1px solid var(--border);">
              <button class="btn btn-ghost" @click="dlShow = false" style="padding:8px 20px;border:1px solid var(--border);border-radius:6px;background:transparent;color:var(--text-primary);cursor:pointer;">取消</button>
              <button class="btn btn-primary" @click="doDlConfirm" :disabled="dlLoading" style="padding:8px 20px;border:none;border-radius:6px;background:#2196F3;color:#fff;cursor:pointer;">确认下载</button>
            </div>
          </div>
        </div>
      </div>

      <!-- ═══ 子视图：依赖库 ═══ -->
      <div v-if="currentTab === 'dependencies'" class="dl-cat-page">
        <div class="vp-header">
          <button class="vp-back-btn" @click="switchTab('grid')">&larr; 返回</button>
          <span class="dl-cat-icon-box" style="background:#E91E63">📚</span>
          <span class="vp-header-title">依赖库</span>
        </div>
        <div style="flex:1;margin:10px 14px;display:flex;flex-direction:column;min-height:0;overflow:hidden;">
          <div class="dl-cat-banner">
            <div class="dl-cat-banner-bar" style="background:#E91E63"></div>
            <div class="dl-cat-banner-body">
              <div class="dl-cat-banner-title">📚 依赖库下载</div>
              <div class="dl-cat-banner-desc">依赖库是Mod运行所必需的基础库文件，如Forge、Fabric等</div>
            </div>
          </div>
          <div class="dl-cat-filter">
            <div class="dl-cat-filter-row">
              <span class="dl-cat-filter-label">名称</span>
              <div class="dl-cat-search-wrap">
                <input type="text" v-model="depSearch" placeholder="搜索依赖库名称..." @keyup.enter="depFilter" />
              </div>
              <span class="dl-cat-filter-label">类型</span>
              <div class="mnl-select" :class="{ open: depTypeOpen }" v-click-outside="() => depTypeOpen = false">
                <div class="mnl-select-trigger" @click="depTypeOpen = !depTypeOpen">
                  <span class="mnl-select-text">{{ depTypeDisplayText }}</span>
                  <span class="mnl-select-arrow">▾</span>
                </div>
                <div class="mnl-select-menu" v-if="depTypeOpen">
                  <div class="mnl-select-item" v-for="o in depTypeOptions" :key="o.v"
                    :class="{ active: depType === o.v }"
                    @click="depType = o.v; depTypeOpen = false; depFilter()">
                    {{ o.t }}
                  </div>
                </div>
              </div>
            </div>
            <div class="dl-cat-filter-actions">
              <button class="dl-cat-btn-search" style="background:#E91E63" @click="depFilter">搜索</button>
              <button class="dl-cat-btn-reset" @click="depSearch='';depType='';depFilter()">重置条件</button>
            </div>
          </div>
          <div class="dl-cat-list" style="flex:1;min-height:0;overflow-y:auto;">
            <div class="dl-cat-loading" v-if="depLoading">⏳ 加载中...</div>
            <div class="dl-cat-empty" v-else-if="depFiltered.length === 0">暂无匹配结果</div>
            <div class="dl-cat-item" v-for="item in depFiltered" :key="item.name">
              <img class="dl-cat-item-icon" :src="item.iconUrl" :alt="item.name" @error="$event.target.style.display='none'" />
              <div class="dl-cat-item-info">
                <div style="display:flex;align-items:center;gap:8px;">
                  <span class="dl-cat-item-name">{{ item.name }}</span>
                  <span class="dl-cat-item-tag" style="background:#E91E63">{{ item.version || 'Latest' }}</span>
                </div>
                <div class="dl-cat-item-desc">{{ item.description }}</div>
                <div class="dl-cat-item-meta">
                  <span style="color:#E91E63">{{ item.type }}</span>
                  <span>·</span>
                  <span>{{ item.compatible }}</span>
                </div>
              </div>
              <button class="dl-cat-item-btn" style="background:#E91E63" @click="depDownload(item)">下载</button>
            </div>
          </div>
        </div>
      </div>

    </div>
  `,
  data() {
    return {
      currentTab: 'grid',
      customUrl: '',
      // 游戏版本子页面
      vpTab: 'java',
      vpSearchText: '',
      vpVersionType: '',
      vpVersions: [],
      vpInstalled: [],
      vpInstalledBedrock: [],
      vpInstalledType: 'java',
      vpLoading: false,
      vpLoaded: false,
      vpGroupOpen: { release: false, snapshot: false, ancient: false },
      vpTypeOpen: false,
      catSourceOpen: false, catVersionOpen: false, catTypeOpen: false,
      depTypeOpen: false,
      // 基岩版版本
      vpBedrockVersions: [],
      vpBedrockLoading: false,
      vpBedrockInstalling: {},
      vpBedrockProgress: {},
      _vpBedrockTimer: null,
      // 版本下载进度
      vpVersionProgress: {},   // { versionId: 0-100 }
      vpVersionStatus: {},     // { versionId: 'status text' }
      _onVersionProgress: null,
      _onVersionComplete: null,
      vpBedrockGroupOpen: { release: false, preview: false, other: false },
      // 加载器选择
      vpLoaderShow: false, vpLoaderTarget: null, vpSelectedLoaders: [],
      // 版本命名
      vpNameShow: false, vpNameInput: '', vpNameError: '',
      // 分类搜索子页面（mods/packs/shaders/datapacks 共享）
      catSearch: '', catSource: '', catVersion: '', catType: '',
      catItems: [], catLoading: false, catPage: 1, catTotalPages: 1, catPageInput: 1,
      // 依赖库
      depSearch: '', depType: '', depItems: [], depLoading: false,
      // 下载确认弹窗
      dlShow: false, dlLoading: false,
      dlProjectId: '', dlItemName: '', dlItemIcon: '', dlItemTypeName: '',
      dlVersions: [], dlVersionIdx: 0, dlSelectedVersionName: '',
      dlDropdownOpen: false,
      dlDirMode: 'auto', dlFileName: '', dlPreviewPath: '',
      dlDefaultTargetDir: '', dlGlobalDir: '',
      dlInstalledMcVersions: [], dlMcVersionIdx: 0, dlMcDropdownOpen: false, dlMinecraftPath: '',
      dlSubDir: 'mods',
      // 确认对话框
      confirmShow: false,
      confirmTitle: '',
      confirmMsg: '',
      confirmAction: null,
      // Java 兼容性对话框
      javaCheckShow: false,
      javaCheckResult: null,
      javaCheckVersionId: null,
      // 自定义下载
      customUrl: '',
      customDlPath: '',
      customDlStatus: 'idle',       // idle | connecting | downloading | completed | error | cancelled
      customDlFileName: '',
      customDlSavePath: '',
      customDlDownloaded: 0,
      customDlTotal: -1,
      customDlSpeed: 0,
      customDlPercent: 0,
      customDlError: '',
      customDlCompleteShow: false,
      _customDlTimer: null,
      _customDlTimeout: null,
    };
  },
  computed: {
    subPageTitle() {
      const titles = { packs: '整合包', mods: 'Mod模组', shaders: '光影/材质', datapacks: '数据包', dependencies: '依赖库' };
      return titles[this.currentTab] || '';
    },
    catConfig() {
      var configs = {
        mods: { color: '#FF9800', icon: '🧩', title: 'Mod模组', placeholder: '搜索模组名称...',
          desc: 'Mod可以为游戏添加新的功能、物品和玩法，让你的Minecraft体验更加丰富',
          sources: [{v:'Modrinth',t:'Modrinth'},{v:'CurseForge',t:'CurseForge'},{v:'All',t:'全部'}],
          versions: [{v:'',t:'全部'},{v:'1.21.1',t:'1.21.1'},{v:'1.20.1',t:'1.20.1'},{v:'1.19.2',t:'1.19.2'},{v:'1.18.2',t:'1.18.2'},{v:'1.16.5',t:'1.16.5'},{v:'1.12.2',t:'1.12.2'},{v:'1.7.10',t:'1.7.10'}],
          types: [{v:'',t:'全部'},{v:'worldgen',t:'世界元素'},{v:'technology',t:'科技'},{v:'magic',t:'魔法'},{v:'adventure',t:'冒险'},{v:'decoration',t:'装饰'},{v:'optimization',t:'性能优化'},{v:'utility',t:'实用'},{v:'library',t:'支持库'}] },
        packs: { color: '#9C27B0', icon: '📁', title: '整合包', placeholder: '搜索整合包名称...',
          desc: '整合包为你打包好了一套完整的Mod组合，即装即玩，无需手动配置',
          sources: [{v:'Modrinth',t:'Modrinth'},{v:'CurseForge',t:'CurseForge'},{v:'All',t:'全部'}],
          versions: [{v:'',t:'全部'},{v:'1.21.1',t:'1.21.1'},{v:'1.20.1',t:'1.20.1'},{v:'1.19.2',t:'1.19.2'},{v:'1.18.2',t:'1.18.2'},{v:'1.16.5',t:'1.16.5'},{v:'1.12.2',t:'1.12.2'}],
          types: [{v:'',t:'全部'},{v:'technology',t:'科技'},{v:'magic',t:'魔法'},{v:'adventure',t:'冒险'},{v:'skyblock',t:'空岛'},{v:'questing',t:'任务'},{v:'exploration',t:'探索'},{v:'optimization',t:'性能优化'}] },
        shaders: { color: '#4CAF50', icon: '✨', title: '光影/材质', placeholder: '搜索光影/材质名称...',
          desc: '光影包可以提升游戏画面表现，材质包改变游戏视觉风格，让你的世界更加美丽',
          sources: [{v:'Modrinth',t:'Modrinth'},{v:'CurseForge',t:'CurseForge'},{v:'All',t:'全部'}],
          versions: [{v:'',t:'全部'},{v:'1.21.1',t:'1.21.1'},{v:'1.20.1',t:'1.20.1'},{v:'1.19.2',t:'1.19.2'},{v:'1.18.2',t:'1.18.2'},{v:'1.16.5',t:'1.16.5'}],
          types: [{v:'',t:'全部'},{v:'vanilla',t:'原版风'},{v:'fantasy',t:'幻想风'},{v:'realistic',t:'写实风'},{v:'cartoon',t:'卡通'},{v:'pbr',t:'PBR'},{v:'performance',t:'性能优化'}] },
        datapacks: { color: '#00BCD4', icon: '📊', title: '数据包', placeholder: '搜索数据包名称...',
          desc: '数据包可以添加自定义合成、进度和游戏机制，无需安装Mod即可扩展玩法',
          sources: [{v:'Modrinth',t:'Modrinth'},{v:'CurseForge',t:'CurseForge'},{v:'All',t:'全部'}],
          versions: [{v:'',t:'全部'},{v:'1.21.1',t:'1.21.1'},{v:'1.20.1',t:'1.20.1'},{v:'1.19.2',t:'1.19.2'},{v:'1.18.2',t:'1.18.2'},{v:'1.16.5',t:'1.16.5'}],
          types: [{v:'',t:'全部'},{v:'worldgen',t:'世界元素'},{v:'technology',t:'科技'},{v:'game-mechanics',t:'游戏机制'},{v:'magic',t:'魔法'},{v:'adventure',t:'冒险'},{v:'utility',t:'实用'}] },
        'domestic-packs': { color: '#E91E63', icon: '🇨🇳', title: '国内整合包', placeholder: '搜索国内整合包名称...',
          desc: '国内社区制作的优质整合包，涵盖科技、魔法、冒险等各种玩法',
          sources: [{v:'All',t:'全部'}],
          versions: [{v:'',t:'全部'},{v:'1.21.1',t:'1.21.1'},{v:'1.20.1',t:'1.20.1'},{v:'1.19.2',t:'1.19.2'},{v:'1.18.2',t:'1.18.2'},{v:'1.16.5',t:'1.16.5'},{v:'1.12.2',t:'1.12.2'}],
          types: [{v:'',t:'全部'},{v:'technology',t:'科技'},{v:'magic',t:'魔法'},{v:'adventure',t:'冒险'},{v:'skyblock',t:'空岛'},{v:'rpg',t:'RPG'},{v:'survival',t:'生存'},{v:'light',t:'轻量'}] },
        bbsmc: { color: '#3F51B5', icon: '💬', title: 'BBSMC', placeholder: '搜索BBSMC资源...',
          desc: 'BBSMC（我的世界中文论坛）社区资源，丰富的MC内容分享平台',
          sources: [{v:'All',t:'全部'}],
          versions: [{v:'',t:'全部'},{v:'1.21.1',t:'1.21.1'},{v:'1.20.1',t:'1.20.1'},{v:'1.19.2',t:'1.19.2'},{v:'1.18.2',t:'1.18.2'},{v:'1.16.5',t:'1.16.5'},{v:'1.12.2',t:'1.12.2'}],
          types: [{v:'',t:'全部'},{v:'mods',t:'模组'},{v:'plugins',t:'插件'},{v:'modpacks',t:'整合包'},{v:'shaders',t:'光影'},{v:'resourcepacks',t:'资源包'},{v:'languages',t:'汉化'},{v:'datapacks',t:'数据包'},{v:'maps',t:'地图'},{v:'softwares',t:'工具'}] }
      };
      return configs[this.currentTab] || configs.mods;
    },
    // 映射前端 tab → 后端 type：mods→mod, packs→modpack, shaders/datapacks→mod
    dlType: function() {
      var map = { mods: 'mod', packs: 'modpack', shaders: 'mod', datapacks: 'mod', 'domestic-packs': 'modpack', bbsmc: 'mod' };
      return map[this.currentTab] || 'mod';
    },
    dlMcVersionLabel: function() {
      if (this.dlMcVersionIdx === -1) return '📂 全局 mods 目录';
      if (this.dlMcVersionIdx >= 0 && this.dlMcVersionIdx < this.dlInstalledMcVersions.length)
        return this.dlInstalledMcVersions[this.dlMcVersionIdx];
      return '请选择游戏版本';
    },
    depFiltered() {
      var list = this.depItems;
      if (this.depSearch) { var kw = this.depSearch.toLowerCase(); list = list.filter(function(d) { return d.name.toLowerCase().indexOf(kw) >= 0; }); }
      if (this.depType) { list = list.filter(function(d) { return d.type === this.depType; }.bind(this)); }
      return list;
    },
    vpFilteredVersions() {
      let list = this.vpVersions;
      if (this.vpSearchText) { const kw = this.vpSearchText.toLowerCase(); list = list.filter(v => v.id.toLowerCase().includes(kw)); }
      if (this.vpVersionType) list = list.filter(v => v.type === this.vpVersionType);
      return list;
    },
    vpLatestTwo() {
      const release = this.vpVersions.find(v => v.type === 'release');
      const snapshot = this.vpVersions.find(v => v.type === 'snapshot');
      const out = [];
      if (release) out.push(release);
      if (snapshot) out.push(snapshot);
      return out;
    },
    vpGroupRelease() {
      return this.vpFilteredVersions.filter(v => v.type === 'release');
    },
    vpGroupSnapshot() {
      return this.vpFilteredVersions.filter(v => v.type === 'snapshot');
    },
    vpGroupAncient() {
      return this.vpFilteredVersions.filter(v => v.type === 'old_alpha' || v.type === 'old_beta');
    },
    vpBedrockFiltered() {
      var list = this.vpBedrockVersions;
      if (this.vpSearchText) {
        var kw = this.vpSearchText.toLowerCase();
        list = list.filter(function(v) { return (v.id || '').toLowerCase().indexOf(kw) >= 0 || (v.version || '').toLowerCase().indexOf(kw) >= 0; });
      }
      return list;
    },
    // 基岩版最新版（前2个）
    vpBedrockLatest: function() {
      var list = this.vpBedrockVersions.slice(0, 2);
      if (this.vpSearchText) {
        var kw = this.vpSearchText.toLowerCase();
        list = this.vpBedrockVersions.filter(function(v) { return (v.id || '').toLowerCase().indexOf(kw) >= 0 || (v.version || '').toLowerCase().indexOf(kw) >= 0; }).slice(0, 2);
      }
      return list;
    },
    // 基岩版分组
    vpBedrockGroupRelease: function() {
      return this.vpBedrockFiltered.filter(function(v) { return (v.type || '') === 'release'; });
    },
    vpBedrockGroupPreview: function() {
      return this.vpBedrockFiltered.filter(function(v) { return (v.type || '') === 'preview'; });
    },
    vpBedrockGroupOther: function() {
      return this.vpBedrockFiltered.filter(function(v) { var t = v.type || ''; return t !== 'release' && t !== 'preview'; });
    },
    vpTypeOptions: function() {
      return [
        { v: '', t: '全部' },
        { v: 'release', t: 'release' },
        { v: 'snapshot', t: 'snapshot' },
        { v: 'old_alpha', t: 'old_alpha' },
        { v: 'old_beta', t: 'old_beta' }
      ];
    },
    vpTypeDisplayText: function() {
      var opt = this.vpTypeOptions.find(function(o) { return o.v === this.vpVersionType; }.bind(this));
      return opt ? opt.t : '全部';
    },
    catSourceDisplayText: function() {
      var opt = this.catConfig.sources.find(function(o) { return o.v === this.catSource; }.bind(this));
      return opt ? opt.t : '全部';
    },
    catVersionDisplayText: function() {
      var opt = this.catConfig.versions.find(function(o) { return o.v === this.catVersion; }.bind(this));
      return opt ? opt.t : '全部';
    },
    catTypeDisplayText: function() {
      var opt = this.catConfig.types.find(function(o) { return o.v === this.catType; }.bind(this));
      return opt ? opt.t : '全部';
    },
    depTypeOptions: function() {
      return [
        { v: '', t: '全部' },
        { v: 'Mod 加载器', t: 'Mod 加载器' },
        { v: '优化', t: '优化' },
        { v: '渲染/图形', t: '渲染/图形' }
      ];
    },
    depTypeDisplayText: function() {
      var opt = this.depTypeOptions.find(function(o) { return o.v === this.depType; }.bind(this));
      return opt ? opt.t : '全部';
    },
  },
  methods: {
    vpIconFor: function(v) {
      var t = (v.type || '').toLowerCase();
      var id = (v.id || '').toLowerCase();
      if (id.indexOf('neoforge') >= 0) return 'NeoForge.png';
      if (id.indexOf('forge') >= 0) return 'Anvil.png';
      if (id.indexOf('fabric') >= 0) return 'Fabric.png';
      if (id.indexOf('optifine') >= 0 || id.indexOf('optifabric') >= 0) return 'GrassPath.png';
      if (id.indexOf('liteloader') >= 0) return 'Egg.png';
      // 中文 type 兼容
      if (t === 'release' || t === '正式版') return 'Grass.png';
      if (t === 'snapshot' || t === 'preview' || t === '预览版') return 'CommandBlock.png';
      if (t.indexOf('old_') === 0 || t.indexOf('远古版') === 0) return 'CobbleStone.png';
      if (id.indexOf('fool') >= 0 || id.indexOf('fun') >= 0 || id.indexOf('23w13a') >= 0 || id.indexOf('20w14infinite') >= 0) return 'GoldBlock.png';
      return 'RedstoneBlock.png';
    },
    vpDownloadBtnText: function(v) {
      if (!v.installing) return '下载';
      var p = this.vpVersionProgress[v.id];
      if (p != null && p >= 0) return '下载中 ' + Math.round(p) + '%';
      return '安装中...';
    },
    vpDownloadBtnTextLg: function(v) {
      if (!v.installing) return '📥 下载';
      var p = this.vpVersionProgress[v.id];
      if (p != null && p >= 0) return '下载中 ' + Math.round(p) + '%';
      return '安装中...';
    },
    switchTab(tab) {
      this.currentTab = tab;
      // 切到版本子视图时强制清除 AI 模式标志
      if (tab === 'versions') window.__mnl_ai_mode = false;
      var self = this;
      this.$nextTick(function() { try { var p = self.$el && self.$el.parentNode; if (p) p.scrollTop = 0; } catch(e) {} });
    },
    // ===== 自定义下载 =====
    formatBytes(bytes) {
      if (!bytes || bytes < 0) return '0 B';
      var units = ['B', 'KB', 'MB', 'GB', 'TB'];
      var i = 0;
      var val = bytes;
      while (val >= 1024 && i < units.length - 1) { val /= 1024; i++; }
      return val.toFixed(i === 0 ? 0 : 1) + ' ' + units[i];
    },
    formatSpeed(bytesPerSec) {
      if (!bytesPerSec || bytesPerSec <= 0) return '0 B/s';
      return this.formatBytes(bytesPerSec) + '/s';
    },
    async startCustomDownload() {
      var url = this.customUrl.trim();
      if (!url) { window.showToast && window.showToast('请输入下载地址', 'warning'); return; }
      if (this.customDlStatus === 'downloading' || this.customDlStatus === 'connecting') return;

      // 简单校验 URL 格式
      if (!/^https?:\/\/.+/.test(url)) {
        window.showToast && window.showToast('请输入有效的下载链接（http:// 或 https:// 开头）', 'warning');
        return;
      }

      var saveDir = this.customDlPath.trim();
      if (!saveDir) {
        try {
          var info = await MNL.customDownload.getPath();
          saveDir = info.path || '';
        } catch(e) {}
        if (!saveDir) {
          this.customDlStatus = 'error';
          this.customDlError = '无法获取下载目录，请在设置中配置保存路径';
          return;
        }
        // 回填默认目录到输入框，方便用户看到
        this.customDlPath = saveDir;
      }

      // 重置进度
      this.customDlStatus = 'connecting';
      this.customDlFileName = '';
      this.customDlSavePath = '';
      this.customDlDownloaded = 0;
      this.customDlTotal = -1;
      this.customDlSpeed = 0;
      this.customDlPercent = 0;
      this.customDlError = '';

      // 30 秒连接超时保护
      var self = this;
      this._customDlTimeout = setTimeout(function() {
        if (self.customDlStatus === 'connecting') {
          self.customDlStatus = 'error';
          self.customDlError = '连接超时，请检查下载地址是否正确';
          MNL.customDownload.cancel().catch(function() {});
        }
      }, 30000);

      try {
        var result = await MNL.customDownload.start(url, saveDir);
        if (!result || !result.success) {
          clearTimeout(this._customDlTimeout);
          this.customDlStatus = 'error';
          this.customDlError = (result && result.error) || '启动下载失败';
          return;
        }
        this._startCustomDlPolling();
      } catch(e) {
        clearTimeout(this._customDlTimeout);
        this.customDlStatus = 'error';
        this.customDlError = e.message || '启动下载失败';
      }
    },
    _startCustomDlPolling() {
      if (this._customDlTimer) clearInterval(this._customDlTimer);
      var self = this;
      this._customDlTimer = setInterval(async function() {
        try {
          var progress = await MNL.customDownload.getProgress();
          if (!progress) return;
          self.customDlStatus = progress.status || 'idle';
          self.customDlFileName = progress.fileName || '';
          self.customDlSavePath = progress.savePath || '';
          self.customDlDownloaded = progress.downloadedBytes || 0;
          self.customDlTotal = progress.totalBytes || -1;
          self.customDlSpeed = progress.speedBytesPerSec || 0;
          if (self.customDlTotal > 0) {
            self.customDlPercent = Math.min(100, (self.customDlDownloaded / self.customDlTotal) * 100);
          }
          self.customDlError = progress.errorMessage || '';

          // 状态变化时清除连接超时
          if (self.customDlStatus !== 'connecting' && self._customDlTimeout) {
            clearTimeout(self._customDlTimeout);
            self._customDlTimeout = null;
          }

          if (self.customDlStatus === 'completed') {
            clearInterval(self._customDlTimer);
            self._customDlTimer = null;
            self.customDlPercent = 100;
            self.customDlCompleteShow = true;
          } else if (self.customDlStatus === 'error' || self.customDlStatus === 'cancelled') {
            clearInterval(self._customDlTimer);
            self._customDlTimer = null;
          }
        } catch(e) {}
      }, 200); // 每 200ms 轮询一次
    },
    cancelCustomDownload() {
      if (this._customDlTimer) { clearInterval(this._customDlTimer); this._customDlTimer = null; }
      if (this._customDlTimeout) { clearTimeout(this._customDlTimeout); this._customDlTimeout = null; }
      MNL.customDownload.cancel().catch(function() {});
      this.customDlStatus = 'cancelled';
    },
    async customDlBrowse() {
      try {
        if (window.MNL && window.MNL.dialog && window.MNL.dialog.openFolder) {
          var path = await window.MNL.dialog.openFolder();
          if (path) {
            this.customDlPath = path;
            // 同步保存到后端
            MNL.customDownload.setPath(path).catch(function() {});
          }
        }
      } catch(e) {
        window.showToast && window.showToast('选择文件夹失败', 'error');
      }
    },
    customDlOpenFile() {
      if (!this.customDlSavePath) return;
      MNL.customDownload.openFile(this.customDlSavePath).catch(function() {});
    },
    customDlOpenFolder() {
      if (!this.customDlSavePath) return;
      MNL.customDownload.openFolder(this.customDlSavePath).catch(function() {});
    },
    vpToggleGroup(name) { this.vpGroupOpen[name] = !this.vpGroupOpen[name]; },
    vpToggleBedrockGroup(name) { this.vpBedrockGroupOpen[name] = !this.vpBedrockGroupOpen[name]; },
    // 游戏版本（惰性加载 + 缓存，避免每次 mount 都调 Mojang API）
    vpLoadVersions: function() {
      if (this.vpLoaded && this.vpVersions.length > 0) return; // 已有缓存，跳过
      if (this.vpLoading) return; // 正在加载中
      this.vpLoading = true;
      var self = this;
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.getAvailableJson) {
          window.MNL.versions.getAvailableJson().then(function(data) {
            self.vpVersions = data || [];
            self.vpLoaded = true;
            self.vpLoading = false;
          })["catch"](function() {
            self.vpVersions = [];
            self.vpLoading = false;
          });
        } else {
          this.vpLoading = false;
        }
      } catch(e) { this.vpLoading = false; }
    },
    vpSearch: function() {
      /* vpFilteredVersions computed 自动按 vpVersionType + vpSearchText 过滤 */
      // 搜索时自动展开所有分组，让用户看到结果
      var self = this;
      if (this.vpSearchText) {
        Object.keys(this.vpGroupOpen).forEach(function(k) { self.vpGroupOpen[k] = true; });
      }
    },
    vpReset: function() {
      this.vpSearchText = '';
      this.vpVersionType = '';
      this.vpBedrockVersions = [];
      this.vpBedrockLoading = false;
    },
    vpSwitchInstalled: function() {
      this.vpTab = 'installed';
      var self = this;
      // 加载 Java 版已安装
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.getInstalledJson) {
          window.MNL.versions.getInstalledJson().then(function(list) {
            self.vpInstalled = (list || []).map(function(v) { return { id: v.id, type: v.type || '正式版', date: v.date, typeColor: v.typeColor || '#2E7D32' }; });
          })["catch"](function() { self.vpInstalled = []; });
        }
      } catch(e) { this.vpInstalled = []; }
      // 加载基岩版已安装
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.getInstalledBedrockJson) {
          window.MNL.versions.getInstalledBedrockJson().then(function(list) {
            self.vpInstalledBedrock = (list || []).map(function(v) { return { id: v.id, type: v.type || '基岩版', date: v.date, typeColor: v.typeColor || '#1565C0' }; });
          })["catch"](function() { self.vpInstalledBedrock = []; });
        }
      } catch(e) { this.vpInstalledBedrock = []; }
    },
    vpSwitchBedrock: function() {
      this.vpTab = 'bedrock';
      this.vpLoadBedrock();
    },
    // 游戏版本 → 弹出加载器选择弹窗（对齐 WPF LoaderSelectionPage）
    vpInstall: function(v) {
      // AI 模式：自动跳过加载器/命名弹窗，直接安装
      if (window.__mnl_ai_mode) {
        var aiLoader = window.__mnl_ai_mode;
        window.__mnl_ai_mode = false;
        var self = this;
        var dirName = v.id;
        v.installing = true;
        this.vpVersionProgress[dirName] = 0;
        this.vpVersionStatus[dirName] = '准备中';
        if (window.MNL && window.MNL.versions && window.MNL.versions.install) {
          window.MNL.versions.install(v.id, aiLoader === 'Vanilla' ? '' : aiLoader, '').then(function(r) {
            var result = typeof r === 'string' ? JSON.parse(r) : r;
            v.installing = false;
            if (result && result.success) {
              self.vpVersionProgress[dirName] = 100;
              self.vpVersionStatus[dirName] = '已完成';
              window.dispatchEvent(new CustomEvent('mnl:versions-changed'));
            } else { delete self.vpVersionProgress[dirName]; delete self.vpVersionStatus[dirName]; }
          })["catch"](function() { v.installing = false; delete self.vpVersionProgress[dirName]; delete self.vpVersionStatus[dirName]; });
        }
        return;
      }
      this.vpLoaderTarget = v;
      this.vpSelectedLoaders = [];
      this.vpLoaderShow = true;
    },
    vpLoaderToggle: function(loader) {
      var idx = this.vpSelectedLoaders.indexOf(loader);
      if (idx >= 0) {
        this.vpSelectedLoaders.splice(idx, 1);
      } else {
        // 选择原版时清空其他加载器
        if (loader === 'Vanilla') this.vpSelectedLoaders = ['Vanilla'];
        else {
          // 选择具体加载器时移除原版
          var vIdx = this.vpSelectedLoaders.indexOf('Vanilla');
          if (vIdx >= 0) this.vpSelectedLoaders.splice(vIdx, 1);
          this.vpSelectedLoaders.push(loader);
        }
      }
    },
    vpLoaderConfirm: function() {
      var v = this.vpLoaderTarget;
      if (!v) return;
      this.vpLoaderShow = false;
      // 弹出版本命名对话框
      this.vpNameInput = '';
      this.vpNameError = '';
      this.vpNameShow = true;
      // 预检查默认名（版本 ID）是否冲突
      this.vpCheckName();
    },
    vpCheckName: function() {
      var self = this;
      var name = (this.vpNameInput || '').trim();
      if (!name) {
        // 留空时检查原始版本 ID 是否冲突
        var v = this.vpLoaderTarget;
        if (v && v.id) {
          this._checkNameConflict(v.id);
        } else {
          this.vpNameError = '';
        }
        return;
      }
      // 校验无效字符
      if (/[<>:"/\\|?*]/.test(name)) {
        this.vpNameError = '名称包含无效字符：< > : " / \\ | ? *';
        return;
      }
      this._checkNameConflict(name);
    },
    _checkNameConflict: function(name) {
      var self = this;
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.getInstalledJson) {
          window.MNL.versions.getInstalledJson().then(function(list) {
            var conflict = (list || []).some(function(v) { return v.id === name; });
            if (conflict) {
              self.vpNameError = '版本 "' + name + '" 已存在，请使用其他名称';
            } else {
              self.vpNameError = '';
            }
          })["catch"](function() { self.vpNameError = ''; });
        }
      } catch(e) {}
    },
    vpNameConfirm: function() {
      var self = this;
      var v = this.vpLoaderTarget;
      if (!v || this.vpNameError) return;
      this.vpNameShow = false;
      v.installing = true;

      var customName = (this.vpNameInput || '').trim();
      var directoryName = customName || v.id;
      var mojangId = v.id;  // 用于从 Mojang API 下载的原始版本 ID

      // 初始化进度显示（使用自定义名称跟踪）
      this.vpVersionProgress[directoryName] = 0;
      this.vpVersionStatus[directoryName] = '准备中';

      var loaders = this.vpSelectedLoaders.filter(function(l) { return l !== 'Vanilla'; });
      var verUrl = v.url || '';  // 前端已有版本 URL，传给后端跳过清单查询
      var onDone = function(success) {
        v.installing = false;
        if (success) {
          self.vpVersionProgress[directoryName] = 100;
          self.vpVersionStatus[directoryName] = '已完成';
        } else {
          delete self.vpVersionProgress[directoryName];
          delete self.vpVersionStatus[directoryName];
        }
      };
      if (loaders.length === 0) {
        // 原版安装
        window.showToast && window.showToast('正在安装 ' + directoryName + '...', 'info');
        if (window.MNL && window.MNL.versions && window.MNL.versions.install) {
          window.MNL.versions.install(mojangId, '', customName || '', verUrl).then(function(r) {
            var result = typeof r === 'string' ? JSON.parse(r) : r;
            if (result && result.success) {
              window.showToast && window.showToast(directoryName + ' 安装完成', 'success');
              window.dispatchEvent(new CustomEvent('mnl:versions-changed'));
              onDone(true);
            } else {
              window.showToast && window.showToast((result && result.error) || '安装失败', 'error');
              onDone(false);
            }
          })["catch"](function(e) {
            window.showToast && window.showToast('安装失败: ' + (e.message || '未知错误'), 'error');
            onDone(false);
          });
        } else { setTimeout(function() { onDone(false); }, 1500); }
      } else {
        // 带加载器安装（一次调用安装版本 + 所有加载器）
        var loaderNames = loaders.join(' + ');
        window.showToast && window.showToast('正在安装 ' + directoryName + ' (' + loaderNames + ')...', 'info');
        if (window.MNL && window.MNL.versions && window.MNL.versions.install) {
          window.MNL.versions.install(mojangId, loaders.join(','), customName || '', verUrl).then(function(r) {
            var result = typeof r === 'string' ? JSON.parse(r) : r;
            if (result && result.success) {
              window.showToast && window.showToast(directoryName + ' (' + loaderNames + ') 安装完成', 'success');
              window.dispatchEvent(new CustomEvent('mnl:versions-changed'));
              onDone(true);
            } else {
              window.showToast && window.showToast((result && result.error) || '安装失败', 'error');
              onDone(false);
            }
          })["catch"](function(e) {
            window.showToast && window.showToast('安装失败: ' + (e.message || '未知错误'), 'error');
            onDone(false);
          });
        } else {
          setTimeout(function() { onDone(false); }, 1500);
        }
      }
    },
    vpDoLaunch: async function(id) {
      // 启动前检查 Java 兼容性（参考 PCL McLaunchJava）
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.checkJava) {
          var result = await window.MNL.versions.checkJava(id);
          if (result && result.compatible === false) {
            this.javaCheckResult = result;
            this.javaCheckVersionId = id;
            // 如果没有替代 Java，直接强制使用当前 Java 启动
            if (!result.recommendedPath || !result.hasAlternatives) {
              this.javaCheckForce();
              return;
            }
            this.javaCheckShow = true;
            return;
          }
        }
      } catch(e) { /* 检查失败则直接启动 */ }
      // 兼容或检查失败，直接启动
      try { window.MNL.versions.launch(id, {}); } catch(e) {}
    },
    javaCheckForce: function() {
      this.javaCheckShow = false;
      var id = this.javaCheckVersionId;
      if (id) { try { window.MNL.versions.launch(id, {}); } catch(e) {} }
    },
    javaCheckUseRecommended: async function() {
      this.javaCheckShow = false;
      var id = this.javaCheckVersionId;
      if (this.javaCheckResult && this.javaCheckResult.recommendedPath) {
        // 保存推荐的 Java 路径到 config.ini
        try {
          if (window.MNL && window.MNL.java && window.MNL.java.savePath) {
            await window.MNL.java.savePath(this.javaCheckResult.recommendedPath);
          }
          window.showToast && window.showToast('已切换为 Java ' + this.javaCheckResult.recommendedMajor, 'success');
        } catch(e) {}
      }
      if (id) { try { window.MNL.versions.launch(id, {}); } catch(e) {} }
    },
    vpDoRepair: function(id) {
      window.showToast && window.showToast('正在修复 ' + id + '...', 'info');
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.repair) {
          window.MNL.versions.repair(id);
        }
      } catch(e) { window.showToast && window.showToast('修复失败: ' + e.message, 'error'); }
    },
    vpCancelInstall: function(id) {
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.cancelInstall) {
          window.MNL.versions.cancelInstall(id);
        }
        delete this.vpVersionProgress[id];
        delete this.vpVersionStatus[id];
        // 找到对应版本并重置 installing 状态
        if (this.vpVersions) {
          for (var i = 0; i < this.vpVersions.length; i++) {
            if (this.vpVersions[i].id === id) {
              this.vpVersions[i].installing = false;
              break;
            }
          }
        }
        window.showToast && window.showToast('已取消', 'info');
      } catch(e) { console.error('取消下载失败:', e); }
    },
    vpDoDelete: function(id) {
      var self = this;
      this.confirmTitle = '删除版本';
      this.confirmMsg = '确定要删除版本 "' + id + '" 吗？此操作不可撤销。';
      this.confirmAction = function() {
        try {
          window.MNL.versions.delete(id);
          self.vpInstalled = self.vpInstalled.filter(function(x) { return x.id !== id; });
          window.showToast && window.showToast(id + ' 已删除', 'success');
          window.dispatchEvent(new CustomEvent('mnl:versions-changed'));
        } catch(e) {
          window.showToast && window.showToast('删除失败', 'error');
        }
      };
      this.confirmShow = true;
    },
    doConfirm: function() {
      if (this.confirmAction) this.confirmAction();
      this.confirmAction = null;
    },
    // ===== 分类搜索（mods/packs/shaders/datapacks） =====
    catSearchGo: function() {
      this.catPage = 1;
      this.catLoad();
    },
    catReset: function() {
      this.catSearch = ''; this.catSource = ''; this.catVersion = ''; this.catType = '';
      this.catSearchGo();
    },
    catLoad: function() {
      this.catLoading = true;
      var self = this;

      // BBSMC 使用独立 API
      if (this.currentTab === 'bbsmc') {
        if (window.MNL && window.MNL.downloads && window.MNL.downloads.fetchBbsmcJson) {
          window.MNL.downloads.fetchBbsmcJson(this.catType, this.catSearch, this.catPage)
            .then(function(data) {
              self.catItems = data.items || [];
              self.catTotalPages = data.totalPages || (data.hasMore ? self.catPage + 1 : self.catPage);
              if (self.catTotalPages < 1) self.catTotalPages = 1;
              self.catLoading = false;
            })["catch"](function() {
              self.catItems = []; self.catLoading = false;
            });
        } else {
          this.catItems = []; this.catLoading = false;
        }
        return;
      }

      // 国内整合包：同时查询 Modrinth/CurseForge + bbsmc.net modpacks（中国社区）
      if (this.currentTab === 'domestic-packs') {
        this._loadDomesticPacks();
        return;
      }
      // 其他分类使用标准搜索
      if (window.MNL && window.MNL.downloads && window.MNL.downloads.searchItemsJson) {
        window.MNL.downloads.searchItemsJson(this.currentTab, this.catSearch, this.catSource, this.catVersion, this.catType, this.catPage)
          .then(function(data) {
            self.catItems = data.items || [];
            self.catTotalPages = data.hasMore ? self.catPage + 1 : self.catPage;
            if (self.catTotalPages < 1) self.catTotalPages = 1;
            self.catLoading = false;
          })["catch"](function() {
            self.catItems = []; self.catLoading = false;
          });
      } else {
        this.catItems = []; this.catLoading = false;
      }
    },
    // === 国内整合包：Modrinth/CurseForge + bbsmc.net modpacks 联合查询 ===
    _loadDomesticPacks: function() {
      var self = this;
      var results = [];

      // 同时查询两个数据源
      var p1 = window.MNL && window.MNL.downloads && window.MNL.downloads.searchItemsJson
        ? window.MNL.downloads.searchItemsJson('modpacks', this.catSearch, this.catSource, this.catVersion, this.catType, this.catPage)
          .then(function(d) { return d && d.items ? d.items : []; })["catch"](function() { return []; })
        : Promise.resolve([]);

      var p2 = window.MNL && window.MNL.downloads && window.MNL.downloads.fetchBbsmcJson
        ? window.MNL.downloads.fetchBbsmcJson('modpacks', this.catSearch, this.catPage)
          .then(function(d) { return d && d.items ? d.items : []; })["catch"](function() { return []; })
        : Promise.resolve([]);

      Promise.all([p1, p2]).then(function(arr) {
        var items1 = arr[0] || [];
        var items2 = arr[1] || [];
        // bbsmc.net 的结果标记来源
        items2.forEach(function(it) { it.source = 'BBSMC'; });
        // 合并去重（按 projectId/name）
        var merged = items1.slice();
        var seen = {};
        merged.forEach(function(it) { seen[it.projectId || it.name] = true; });
        items2.forEach(function(it) {
          var key = it.projectId || it.name;
          if (!seen[key]) { seen[key] = true; merged.push(it); }
        });
        // 分页截取（每页20条）
        var pageSize = 20;
        var offset = (self.catPage - 1) * pageSize;
        self.catItems = merged.slice(offset, offset + pageSize);
        self.catTotalPages = merged.length > offset + pageSize ? self.catPage + 1 : self.catPage;
        if (self.catTotalPages < 1) self.catTotalPages = 1;
        self.catLoading = false;
      })["catch"](function() {
        self.catItems = []; self.catLoading = false;
      });
    },
    catPageGo: function(p) {
      p = parseInt(p) || 1;
      if (p < 1) p = 1;
      if (p > this.catTotalPages) p = this.catTotalPages;
      this.catPageInput = p;
      this.catPage = p;
      this.catLoad();
    },
    // ===== 分类搜索下载流程 =====
    catDownload: function(item) {
      var self = this;
      // 整合包 → 直接走安装流程（下载 mrpack + ModpackInstaller 安装）
      if (this.currentTab === 'packs' || this.currentTab === 'domestic-packs') {
        if (!window.MNL || !window.MNL.modpacks || !window.MNL.modpacks.install) {
          window.showToast && window.showToast('整合包安装功能不可用', 'error');
          return;
        }
        window.showToast && window.showToast('正在安装整合包: ' + item.name, 'info');
        window.MNL.modpacks.install(item.projectId, item.source).then(function(r) {
          if (r && r.success !== false) {
            window.showToast && window.showToast('整合包安装已开始，请等待完成通知', 'success');
          } else {
            window.showToast && window.showToast('安装失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        }).catch(function(e) {
          window.showToast && window.showToast('安装失败: ' + e.message, 'error');
        });
        return;
      }
      if (!window.MNL || !window.MNL.recommendations || !window.MNL.recommendations.getDownloadOptions) {
        window.showToast && window.showToast('下载功能不可用', 'error');
        return;
      }
      this.dlShow = true;
      this.dlLoading = true;
      this.dlProjectId = item.projectId;
      this.dlItemName = item.name;
      this.dlItemIcon = item.iconUrl || '';
      this.dlItemTypeName = this.catConfig.title;
      this.dlDropdownOpen = false;
      this.dlVersions = [];
      this.dlVersionIdx = 0;
      this.dlSelectedVersionName = '';
      this.dlFileName = '';
      this.dlDefaultTargetDir = '';
      this.dlGlobalDir = '';
      this.dlPreviewPath = '...';

      var dlType = this.dlType;
      var source = this.currentTab === 'bbsmc' ? 'bbsmc' : '';
      window.MNL.recommendations.getDownloadOptions(item.projectId, item.name, dlType, source)
        .then(function(data) {
          if (data && data.success) {
            self.dlVersions = data.availableVersions || [];
            var recIdx = data.recommendedIndex != null ? data.recommendedIndex : 0;
            self.dlVersionIdx = recIdx;
            if (self.dlVersions.length > 0 && recIdx < self.dlVersions.length) {
              var v = self.dlVersions[recIdx];
              self.dlSelectedVersionName = v.isBadDisplay ? v.fileName : v.versionName;
            }
            self.dlFileName = data.defaultFileName || (item.name + '.jar');
            self.dlMinecraftPath = data.minecraftPath || '';
            self.dlSubDir = data.subDir || 'mods';
            self.dlInstalledMcVersions = data.installedVersions || [];
            // 自动选择当前版本或第一个已安装版本
            var curVer = data.currentVersion || '';
            var foundIdx = curVer ? self.dlInstalledMcVersions.indexOf(curVer) : -1;
            self.dlMcVersionIdx = foundIdx >= 0 ? foundIdx : (self.dlInstalledMcVersions.length > 0 ? 0 : -1);
            self.dlMcDropdownOpen = false;
            self.dlDefaultTargetDir = data.defaultTargetDir || '';
            self.dlGlobalDir = self.dlMinecraftPath ? self.dlMinecraftPath + '\\' + self.dlSubDir : data.defaultTargetDir || '';
            self.dlDirMode = 'auto';
            self.updateDlPreviewPath();
          } else {
            window.showToast && window.showToast('获取下载选项失败: ' + ((data && data.error) || '未知错误'), 'error');
            self.dlShow = false;
          }
          self.dlLoading = false;
        })
        .catch(function(e) {
          window.showToast && window.showToast('获取下载选项失败', 'error');
          self.dlShow = false;
          self.dlLoading = false;
        });
    },
    selectDlVersion: function(i) {
      this.dlVersionIdx = i;
      this.dlDropdownOpen = false;
      if (i >= 0 && i < this.dlVersions.length) {
        var v = this.dlVersions[i];
        this.dlSelectedVersionName = v.isBadDisplay ? v.fileName : v.versionName;
      }
    },
    // 选择 Minecraft 目标版本（对齐 WPF: 切换版本后重新计算隔离目录）
    selectDlMcVersion: function(i) {
      this.dlMcVersionIdx = i;
      this.dlMcDropdownOpen = false;
      this.updateDlPreviewPath();
    },
    // 根据 MC 版本 + 目录模式重新计算预览路径
    updateDlPreviewPath: function() {
      var base = this.dlMinecraftPath;
      if (!base) { this.dlPreviewPath = this.dlDefaultTargetDir + '\\' + this.dlFileName; return; }
      if (this.dlDirMode === 'global' || this.dlMcVersionIdx < 0) {
        // 全局 mods 目录
        this.dlPreviewPath = base + '\\' + this.dlSubDir + '\\' + this.dlFileName;
      } else if (this.dlMcVersionIdx >= 0 && this.dlMcVersionIdx < this.dlInstalledMcVersions.length) {
        // 版本隔离目录: versions/{version}/game/{subDir}/
        var v = this.dlInstalledMcVersions[this.dlMcVersionIdx];
        this.dlPreviewPath = base + '\\versions\\' + v + '\\game\\' + this.dlSubDir + '\\' + this.dlFileName;
      } else {
        this.dlPreviewPath = this.dlDefaultTargetDir + '\\' + this.dlFileName;
      }
    },
    setDlDirMode: function(mode) {
      this.dlDirMode = mode;
      this.updateDlPreviewPath();
    },
    doDlConfirm: function() {
      var self = this;
      if (this.dlLoading) return;
      if (!this.dlProjectId) {
        window.showToast && window.showToast('下载信息不完整', 'error');
        return;
      }
      this.dlLoading = true;
      // 根据 MC 版本选择 + 目录模式计算实际保存路径
      var savePath;
      var base = this.dlMinecraftPath;
      if (this.dlDirMode === 'global' || this.dlMcVersionIdx < 0) {
        savePath = base ? base + '\\' + this.dlSubDir : this.dlDefaultTargetDir;
      } else if (this.dlMcVersionIdx >= 0 && this.dlMcVersionIdx < this.dlInstalledMcVersions.length) {
        var v = this.dlInstalledMcVersions[this.dlMcVersionIdx];
        savePath = base + '\\versions\\' + v + '\\game\\' + this.dlSubDir;
      } else {
        savePath = this.dlDefaultTargetDir;
      }
      var dlType = this.dlType;
      var source = this.currentTab === 'bbsmc' ? 'bbsmc' : '';
      window.MNL.recommendations.downloadRecommend(
        this.dlProjectId,
        this.dlItemName,
        dlType,
        savePath,
        this.dlFileName,
        this.dlVersionIdx,
        source
      )
      .then(function(data) {
        self.dlLoading = false;
        if (data && data.success) {
          window.showToast && window.showToast('下载任务已添加: ' + self.dlItemName, 'success');
          self.dlShow = false;
          // 跳转到下载任务页面查看进度
          window.navigateTo && window.navigateTo('downloads');
        } else {
          window.showToast && window.showToast('下载失败: ' + ((data && data.error) || '未知错误'), 'error');
        }
      })
      .catch(function() {
        window.showToast && window.showToast('下载失败', 'error');
        self.dlLoading = false;
      });
    },
    catIconError: function(e) {
      e.target.style.display = 'none';
    },
    // ===== 依赖库 =====
    depFilter: function() { /* 响应式计算属性 */ },
    depLoad: function() {
      this.depLoading = true;
      var self = this;
      if (window.MNL && window.MNL.downloads && window.MNL.downloads.getDependenciesJson) {
        window.MNL.downloads.getDependenciesJson().then(function(data) {
          self.depItems = data.items || [];
          self.depLoading = false;
        })["catch"](function() {
          self.depItems = []; self.depLoading = false;
        });
      } else {
        this.depItems = []; this.depLoading = false;
      }
    },
    // 依赖库下载：Sodium/Lithium/Iris 走 Modrinth 版本选择，其他外链打开
    depDownload: function(item) {
      var modrinthIds = { 'Sodium': 'AANobbMI', 'Lithium': 'gvQqBUqZ', 'Iris': 'YL57xq9U' };
      var modrinthId = modrinthIds[item.name];
      if (modrinthId) {
        this.catDownload({ projectId: modrinthId, name: item.name, iconUrl: item.iconUrl });
      } else if (item.sourceUrl) {
        if (window.MNL && window.MNL.shell && window.MNL.shell.openExternal) {
          window.MNL.shell.openExternal(item.sourceUrl);
        } else {
          window.open(item.sourceUrl, '_blank');
        }
        window.showToast && window.showToast('已在浏览器中打开: ' + item.name, 'info');
      } else {
        window.showToast && window.showToast('未找到下载链接', 'warning');
      }
    },
    // ═══ 基岩版版本加载 ═══
    vpLoadBedrock: function() {
      if (this.vpBedrockLoading) return;
      this.vpBedrockLoading = true;
      var self = this;
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.getAvailableBedrockJson) {
          window.MNL.versions.getAvailableBedrockJson().then(function(data) {
            self.vpBedrockVersions = (data && data.length > 0) ? data : (data && data.versions) || [];
            self.vpBedrockLoading = false;
          })["catch"](function() {
            self.vpBedrockVersions = [];
            self.vpBedrockLoading = false;
          });
        } else {
          this.vpBedrockLoading = false;
        }
      } catch(e) { this.vpBedrockLoading = false; }
    },
    vpRefreshBedrock: function() {
      this.vpBedrockVersions = [];
      this.vpLoadBedrock();
    },
    vpInstallBedrock: function(v) {
      if (!v || v.isDownloaded || v.installing) return;
      var self = this;
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.installBedrockJson) {
          window.MNL.versions.installBedrockJson(v.id).then(function(result) {
            if (result && result.success) {
              window.showToast && window.showToast('开始下载基岩版 ' + v.id, 'info');
              self.$set(self.vpBedrockInstalling, v.id, true);
              self.$set(self.vpBedrockProgress, v.id, 0);
              self._startBedrockPolling(v.id);
            } else {
              window.showToast && window.showToast('下载失败: ' + (result && result.error || '未知错误'), 'error');
            }
          })["catch"](function(e) {
            window.showToast && window.showToast('下载请求失败', 'error');
          });
        }
      } catch(e) {}
    },
    _startBedrockPolling: function(versionId) {
      var self = this;
      if (this._vpBedrockTimer) clearInterval(this._vpBedrockTimer);
      this._vpBedrockTimer = setInterval(function() {
        try {
          if (window.MNL && window.MNL.versions && window.MNL.versions.getBedrockDownloadProgressJson) {
            window.MNL.versions.getBedrockDownloadProgressJson().then(function(p) {
              if (!p || !p.isDownloading) {
                if (p && p.progress >= 100) {
                  self.$set(self.vpBedrockInstalling, versionId, false);
                  self.$set(self.vpBedrockProgress, versionId, 100);
                  clearInterval(self._vpBedrockTimer);
                  self._vpBedrockTimer = null;
                  window.showToast && window.showToast('基岩版 ' + versionId + ' 下载完成', 'success');
                }
                return;
              }
              self.$set(self.vpBedrockProgress, versionId, p.progress || 0);
            });
          }
        } catch(e) {}
      }, 500);
    },
    vpCancelBedrockInstall: function(versionId) {
      var self = this;
      try {
        if (window.MNL && window.MNL.versions && window.MNL.versions.cancelBedrockDownloadJson) {
          window.MNL.versions.cancelBedrockDownloadJson().then(function() {
            self.$set(self.vpBedrockInstalling, versionId, false);
            if (self._vpBedrockTimer) { clearInterval(self._vpBedrockTimer); self._vpBedrockTimer = null; }
            window.showToast && window.showToast('已取消下载', 'info');
          });
        }
      } catch(e) {}
    },
  },
  watch: {
    currentTab: function(val) {
      if (val === 'mods' || val === 'packs' || val === 'shaders' || val === 'datapacks' || val === 'domestic-packs' || val === 'bbsmc') {
        this.catItems = [];
        this.catSearch = ''; this.catSource = ''; this.catVersion = ''; this.catType = '';
        this.catLoad();
      } else if (val === 'dependencies') {
        this.depItems = [];
        this.depLoad();
      } else if (val === 'versions') {
        this.vpLoadVersions();
      }
    },
    dlDirMode: function(val) {
      var dir = val === 'auto' ? this.dlDefaultTargetDir : this.dlGlobalDir;
      if (dir) this.dlPreviewPath = dir + '\\' + this.dlFileName;
    },
    dlFileName: function(val) {
      var dir = this.dlDirMode === 'auto' ? this.dlDefaultTargetDir : this.dlGlobalDir;
      if (dir) this.dlPreviewPath = dir + '\\' + val;
    }
  },
  mounted() {
    // 强制清除可能残留的 AI 模式标志，防止手动下载跳过加载器/命名弹窗
    window.__mnl_ai_mode = false;
    // 如果从快捷卡片跳转且指定了标签页，直接切换到对应标签
    if (this.$root && this.$root.pageParams && this.$root.pageParams.tab) {
      this.switchTab(this.$root.pageParams.tab);
      this.$root.pageParams = {};
    }
    // 加载自定义下载路径
    try {
      var self = this;
      MNL.customDownload.getPath().then(function(info) {
        if (info && info.path) self.customDlPath = info.path;
      });
    } catch(e) {}
    // 版本列表不再预加载，等用户点击"游戏版本"时才惰性加载
    // 进入页面后滚动到顶部，防止"打开内容全部在最下方"
    this.$nextTick(function() { try { var p = self && self.$el && self.$el.parentNode; if (p) p.scrollTop = 0; } catch(e) {} });
    // 监听版本下载进度
    var self = this;
    this._onVersionProgress = function(e) {
      var d = e.detail;
      if (d && d.versionId) {
        self.vpVersionProgress[d.versionId] = d.progress || 0;
        if (d.status) self.vpVersionStatus[d.versionId] = d.status;
      }
    };
    this._onVersionComplete = function(e) {
      var d = e.detail;
      if (d && d.versionId) {
        self.vpVersionProgress[d.versionId] = 100;
        self.vpVersionStatus[d.versionId] = '已完成';
      }
    };
    window.addEventListener('mnl:version-download-progress', this._onVersionProgress);
    window.addEventListener('mnl:version-download-complete', this._onVersionComplete);
  },
  beforeUnmount() {
    if (this._customDlTimer) { clearInterval(this._customDlTimer); this._customDlTimer = null; }
    if (this._customDlTimeout) { clearTimeout(this._customDlTimeout); this._customDlTimeout = null; }
    if (this._vpBedrockTimer) { clearInterval(this._vpBedrockTimer); this._vpBedrockTimer = null; }
    if (this._onVersionProgress) { window.removeEventListener('mnl:version-download-progress', this._onVersionProgress); }
    if (this._onVersionComplete) { window.removeEventListener('mnl:version-download-complete', this._onVersionComplete); }
  },
};
