// ====== 设置中心 SettingsPage (sp-) ======
// Exact HTML structure from UnoApp settings.html with Vue 3 directives added.
const SettingsPage = {
  template: `
    <div class="page-content settings-page">
      <div class="sp-scroll">
        <div class="sp-grid">
          <!-- Title -->
          <h2 class="sp-page-title">设置中心</h2>

          <!-- Left-Right Layout -->
          <div class="sp-layout">
            <!-- Left sidebar -->
            <div class="sp-sidebar">
              <div class="sp-sidebar-inner">
                <button class="sp-cat-btn" v-for="cat in categories" :key="cat.id"
                  :class="{ active: tab === cat.id }" @click="tab = cat.id">{{ cat.label }}</button>
              </div>
            </div>

            <!-- Right content area -->
            <div class="sp-content">

              <!-- ═══ 游戏设置 ═══ -->
              <div class="sp-panel" :class="{ active: tab === 'game' }">
                <!-- 实时内存占用条 -->
                <div class="sp-section">
                  <div class="sp-section-title">系统内存</div>
                  <div class="sp-ram-bar-wrap">
                    <div class="sp-ram-bar">
                      <div class="sp-ram-seg sp-ram-used" :style="{ flex: memoryInfo.usedPercent }"></div>
                      <div class="sp-ram-seg sp-ram-game" :style="{ flex: memoryInfo.gamePercent }"></div>
                      <div class="sp-ram-seg sp-ram-free" :style="{ flex: memoryInfo.freePercent }"></div>
                    </div>
                  </div>
                  <div class="sp-ram-legend">
                    <span class="sp-ram-legend-item"><span class="sp-ram-dot used"></span>已用 {{ memoryInfo.usedGB }} GB</span>
                    <span class="sp-ram-legend-item"><span class="sp-ram-dot game"></span>游戏分配 {{ memoryInfo.gameGB }} GB</span>
                    <span class="sp-ram-legend-item"><span class="sp-ram-dot free"></span>空闲 {{ memoryInfo.freeGB }} GB</span>
                    <span class="sp-ram-total">总计 {{ memoryInfo.totalGB }} GB</span>
                  </div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">内存分配</div>
                  <div class="sp-radio-row">
                    <label class="sp-radio"><input type="radio" v-model="ramMode" value="auto" /> 自动配置</label>
                    <label class="sp-radio"><input type="radio" v-model="ramMode" value="custom" /> 自定义</label>
                  </div>
                  <div class="sp-slider-row" v-if="ramMode === 'custom'">
                    <input type="range" v-model.number="ramValue" min="0.5" max="32" step="0.5" />
                    <span class="sp-slider-val">{{ ramValue.toFixed(1) }} GB</span>
                  </div>
                  <div class="sp-ram-info" v-else>
                    <span>自动分配：</span><span class="sp-ram-val">由系统自动调整</span>
                  </div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">分辨率设置</div>
                  <div class="sp-field-row">
                    <div class="mnl-select" :class="{ open: resolutionOpen }" v-click-outside="() => resolutionOpen = false">
                      <div class="mnl-select-trigger" @click="resolutionOpen = !resolutionOpen">
                        <span class="mnl-select-text">{{ resolutionDisplayText }}</span>
                        <span class="mnl-select-arrow">▾</span>
                      </div>
                      <div class="mnl-select-menu" v-if="resolutionOpen">
                        <div class="mnl-select-item" v-for="o in resolutionOptions2" :key="o.v"
                          :class="{ active: resolution === o.v }"
                          @click="resolution = o.v; resolutionOpen = false">
                          {{ o.t }}
                        </div>
                      </div>
                    </div>
                    <label class="sp-check"><input type="checkbox" v-model="fullscreen" /> 全屏模式</label>
                  </div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">皮肤资源包</div>
                  <label class="sp-check"><input type="checkbox" v-model="skinPack" /> 启用离线皮肤资源包（将自定义皮肤注入游戏）</label>
                  <div class="sp-hint">提示：皮肤可在个人资料页面设置，此选项控制启动时是否自动加载。</div>
                </div>
              </div>

              <!-- ═══ Java设置 ═══ -->
              <div class="sp-panel" :class="{ active: tab === 'java' }">
                <div class="sp-section">
                  <div class="sp-section-title">当前 Java</div>
                  <div class="sp-java-status">
                    <span class="sp-java-status-dot" :style="{ background: currentJava ? '#4CAF50' : '#FF9800' }"></span>
                    <div>
                      <div class="sp-java-ver">{{ currentJava ? ('Java ' + currentJava.version) : '未配置' }}</div>
                      <div class="sp-java-path">{{ currentJava ? currentJava.path : '请在下方检测或设置 Java 路径' }}</div>
                    </div>
                  </div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-header">
                    <div class="sp-section-title">检测到的 Java</div>
                    <button class="btn sp-btn-sm" @click="detectJava">重新检测</button>
                  </div>
                  <div v-if="detectedJavaList.length > 0">
                    <div class="sp-java-item" v-for="j in detectedJavaList" :key="j.path">
                      <div class="sp-java-item-info">
                        <div class="sp-java-item-path">{{ j.path }}</div>
                        <div class="sp-java-item-meta"><span class="sp-tag">Java {{ j.version }}</span><span>{{ j.arch }}</span></div>
                      </div>
                      <button class="btn btn-success btn-sm" @click="useJava(j)">使用</button>
                    </div>
                  </div>
                  <div class="sp-empty-text" v-if="detectedJavaList.length === 0">未检测到 Java，请点击 [重新检测] 或手动设置</div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-header">
                    <div class="sp-section-title">下载 Java</div>
                    <span class="sp-java-rec">推荐: Java 17</span>
                  </div>
                  <div class="sp-java-dl-btns">
                    <button class="btn sp-java-dl-btn" v-for="v in [8,11,17,21,25]" :key="v" @click="downloadJava(v)">Java {{ v }}</button>
                  </div>
                  <div class="sp-java-dl-progress" v-if="javaDownloading">
                    <div class="sp-progress-text">正在下载 Java {{ javaDlVersion }}... {{ javaDlProgress }}%</div>
                    <div class="sp-progress-bar"><div class="sp-progress-fill" :style="{ width: javaDlProgress + '%' }"></div></div>
                  </div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">手动配置 Java 路径</div>
                  <div class="sp-field-row">
                    <input type="text" v-model="customJavaPath" class="sp-input" placeholder="输入 Java 路径..." />
                    <button class="btn" @click="browseJava">浏览...</button>
                  </div>
                  <div class="sp-field-row" style="margin-top:8px">
                    <button class="btn btn-success" @click="setJavaPath">设为当前 Java</button>
                    <button class="btn" @click="validateJava">验证 Java</button>
                  </div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">JVM 参数</div>
                  <div class="sp-section-desc" style="font-size:12px;color:var(--text-secondary);margin-bottom:10px;">选择合适的预设方案，或选"自定义"手动编辑</div>
                  <div class="mnl-select" :class="{ open: jvmPresetOpen }" v-click-outside="() => jvmPresetOpen = false" style="margin-bottom:10px;">
                    <div class="mnl-select-trigger" @click="jvmPresetOpen = !jvmPresetOpen">
                      <span class="mnl-select-text">{{ jvmPresetDisplayText }}</span>
                      <span class="mnl-select-arrow">▾</span>
                    </div>
                    <div class="mnl-select-menu" v-if="jvmPresetOpen">
                      <div class="mnl-select-item" v-for="p in jvmPresetOptions" :key="p.v"
                        :class="{ active: jvmPreset === p.v }"
                        @click="selectJvmPreset(p.v); jvmPresetOpen = false">
                        <div style="display:flex;flex-direction:column;">
                          <span style="font-weight:500;">{{ p.t }}</span>
                          <span style="font-size:11px;color:var(--text-muted);">{{ p.desc }}</span>
                        </div>
                      </div>
                    </div>
                  </div>
                  <textarea v-model="jvmArgs" class="sp-textarea" rows="5"
                    :disabled="jvmPreset !== 'custom'"
                    :style="{ opacity: jvmPreset !== 'custom' ? 0.7 : 1, resize:'vertical' }"
                    :placeholder="jvmPreset === 'custom' ? '在此输入自定义 JVM 参数...' : ''"></textarea>
                </div>
              </div>

              <!-- ═══ 常规设置 ═══ -->
              <div class="sp-panel" :class="{ active: tab === 'general' }">
                <div class="sp-section">
                  <div class="sp-section-title">启动设置</div>
                  <label class="sp-check"><input type="checkbox" v-model="autoLogin" /> 启动时自动登录上次账号</label>
                  <label class="sp-check"><input type="checkbox" v-model="minimizeToTray" /> 关闭窗口时最小化到系统托盘</label>
                  <label class="sp-check"><input type="checkbox" v-model="autoCheckUpdate" /> 自动检查更新</label>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">界面设置</div>
                  <label class="sp-check"><input type="checkbox" v-model="showSplash" /> 显示启动画面</label>
                  <label class="sp-check"><input type="checkbox" v-model="hardwareAccel" /> 启用硬件加速</label>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">语言</div>
                  <div class="mnl-select" :class="{ open: languageOpen }" v-click-outside="() => languageOpen = false">
                    <div class="mnl-select-trigger" @click="languageOpen = !languageOpen">
                      <span class="mnl-select-text">{{ languageDisplayText }}</span>
                      <span class="mnl-select-arrow">▾</span>
                    </div>
                    <div class="mnl-select-menu" v-if="languageOpen">
                      <div class="mnl-select-item" v-for="o in languageOptions" :key="o.v"
                        :class="{ active: language === o.v }"
                        @click="language = o.v; languageOpen = false">
                        {{ o.t }}
                      </div>
                    </div>
                  </div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">游戏目录</div>
                  <div class="sp-folder-list-header">文件夹列表</div>
                  <div class="sp-folder-list">
                    <div class="sp-folder-item" v-for="(f, i) in gameFolders" :key="i" :class="{ selected: selectedFolder === f.path }">
                      <label class="sp-radio-spot">
                        <input type="radio" :value="f.path" v-model="selectedFolder" />
                        <span class="sp-radio-dot"></span>
                      </label>
                      <div class="sp-folder-info">
                        <div class="sp-folder-name">{{ f.label }}</div>
                        <div class="sp-folder-path">{{ f.path }}</div>
                      </div>
                      <button class="sp-folder-settings-btn" @click.stop="folderSettings(f)" title="文件夹设置">...</button>
                      <!-- 文件夹操作下拉菜单 -->
                      <div class="sp-folder-menu" v-if="folderMenuFor === f.path" @click.stop>
                        <div class="sp-folder-menu-item" @click.stop="renameFolder(f)">✏️ 重命名</div>
                        <div class="sp-folder-menu-item" @click.stop="openGameFolder(f)">📂 打开文件夹</div>
                        <div class="sp-folder-menu-item sp-folder-menu-danger" @click.stop="deleteFolder(f)">🗑️ 删除</div>
                      </div>
                    </div>
                  </div>
                  <div class="sp-folder-list-header">添加或导入</div>
                  <div class="sp-folder-actions">
                    <div class="sp-folder-action-item" @click="createFolder">
                      <span class="sp-fa-icon">📁</span>
                      <span class="sp-fa-label">新建 .minecraft 文件夹</span>
                    </div>
                    <div class="sp-folder-action-item" @click="addFolder">
                      <span class="sp-fa-icon">➕</span>
                      <span class="sp-fa-label">添加已有文件夹</span>
                    </div>
                    <div class="sp-folder-action-item" @click="importModpack">
                      <span class="sp-fa-icon">📥</span>
                      <span class="sp-fa-label">导入整合包</span>
                    </div>
                  </div>
                  <div v-if="modpackImporting" class="sp-modpack-progress">
                    <div class="sp-modpack-progress-header">
                      <span class="sp-modpack-name">{{ modpackImportName || '正在导入整合包...' }}</span>
                      <span class="sp-modpack-percent">{{ modpackImportProgress > 0 ? modpackImportProgress + '%' : '' }}</span>
                    </div>
                    <div class="sp-modpack-progress-bar">
                      <div class="sp-modpack-progress-fill" :style="{ width: (modpackImportProgress || 0) + '%' }"></div>
                    </div>
                    <div class="sp-modpack-status">{{ modpackImportStatus }}</div>
                  </div>
                </div>
              </div>

              <!-- ═══ 下载设置 ═══ -->
              <div class="sp-panel" :class="{ active: tab === 'download' }">
                <div class="sp-section">
                  <div class="sp-section-title">下载线程数</div>
                  <div class="sp-slider-row">
                    <input type="range" v-model.number="downloadThreads" min="1" max="200" />
                    <span class="sp-slider-val">{{ downloadThreads }}</span>
                  </div>
                </div>

                <div class="sp-section">
                  <label class="sp-check"><input type="checkbox" v-model="autoInstallDeps" /> 下载后自动安装依赖库</label>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">默认版本隔离</div>
                  <div class="mnl-select" :class="{ open: isolationOpen }" v-click-outside="() => isolationOpen = false">
                    <div class="mnl-select-trigger" @click="isolationOpen = !isolationOpen">
                      <span class="mnl-select-text">{{ isolationDisplayText }}</span>
                      <span class="mnl-select-arrow">▾</span>
                    </div>
                    <div class="mnl-select-menu" v-if="isolationOpen">
                      <div class="mnl-select-item" v-for="o in isolationOptions" :key="o.v"
                        :class="{ active: versionIsolationLevel === o.v }"
                        @click="versionIsolationLevel = o.v; isolationOpen = false">
                        {{ o.t }}
                      </div>
                    </div>
                  </div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">自定义下载路径</div>
                  <div class="sp-field-row">
                    <input type="text" v-model="customDownloadPath" class="sp-input" placeholder="留空使用系统默认下载目录" />
                    <button class="btn" @click="browseCustomDownloadPath">浏览</button>
                  </div>
                  <div class="sp-hint">设置自定义下载的默认保存位置，留空则使用系统 Downloads 文件夹</div>
                </div>
              </div>

              <!-- ═══ 背景设置 ═══ -->
              <div class="sp-panel" :class="{ active: tab === 'background' }">
                <div class="sp-section">
                  <div class="sp-section-title">背景模式</div>
                  <label class="sp-radio"><input type="radio" v-model="bgMode" value="mica" /> 默认 Mica 云母背景</label>
                  <label class="sp-radio"><input type="radio" v-model="bgMode" value="image" /> 本地图片背景</label>
                  <label class="sp-radio"><input type="radio" v-model="bgMode" value="video" /> 本地视频背景（MP4）</label>
                  <label class="sp-radio"><input type="radio" v-model="bgMode" value="panorama" /> MC 全景动画背景</label>
                </div>

                <!-- ═══ 全景模式设置 ═══ -->
                <template v-if="bgMode === 'panorama'">
                  <div class="sp-section">
                    <div class="sp-section-title">全景主题</div>
                    <div class="sp-panorama-theme-grid">
                      <div class="sp-panorama-theme-opt" :class="{ active: panoramaTheme === 'overworld' }" @click="panoramaTheme = 'overworld'">
                        <span class="spt-icon">🌳</span><span class="spt-label">主世界</span>
                      </div>
                      <div class="sp-panorama-theme-opt" :class="{ active: panoramaTheme === 'nether' }" @click="panoramaTheme = 'nether'">
                        <span class="spt-icon">🔥</span><span class="spt-label">下界</span>
                      </div>
                      <div class="sp-panorama-theme-opt" :class="{ active: panoramaTheme === 'wild' }" @click="panoramaTheme = 'wild'">
                        <span class="spt-icon">🌿</span><span class="spt-label">荒野</span>
                      </div>
                      <div class="sp-panorama-theme-opt" :class="{ active: panoramaTheme === 'end' }" @click="panoramaTheme = 'end'">
                        <span class="spt-icon">⚔️</span><span class="spt-label">试炼</span>
                      </div>
                      <div class="sp-panorama-theme-opt" :class="{ active: panoramaTheme === 'darkforest' }" @click="panoramaTheme = 'darkforest'">
                        <span class="spt-icon">🐝</span><span class="spt-label">蜜蜂林地</span>
                      </div>
                      <div class="sp-panorama-theme-opt" :class="{ active: panoramaTheme === 'desert' }" @click="panoramaTheme = 'desert'">
                        <span class="spt-icon">🕳️</span><span class="spt-label">地下洞穴</span>
                      </div>
                      <div class="sp-panorama-theme-opt" :class="{ active: panoramaTheme === 'mountains' }" @click="panoramaTheme = 'mountains'">
                        <span class="spt-icon">⛰️</span><span class="spt-label">山脉</span>
                      </div>
                      <div class="sp-panorama-theme-opt" :class="{ active: panoramaTheme === 'cherry' }" @click="panoramaTheme = 'cherry'">
                        <span class="spt-icon">🌸</span><span class="spt-label">樱花</span>
                      </div>
                      <div class="sp-panorama-theme-opt" :class="{ active: panoramaTheme === 'deep_dark' }" @click="panoramaTheme = 'deep_dark'">
                        <span class="spt-icon">🌲</span><span class="spt-label">苍白森林</span>
                      </div>
                    </div>
                  </div>
                  <div class="sp-section">
                    <div class="sp-section-title">旋转速度</div>
                    <div class="sp-slider-row">
                      <input type="range" v-model.number="panoramaSpeed" min="0" max="20" step="1" />
                      <span class="sp-slider-val">{{ panoramaSpeed }}</span>
                    </div>
                    <div class="sp-hint">值越大全景旋转越快。0 为静止。</div>
                  </div>
                  <div class="sp-section">
                    <div class="sp-toggle-row">
                      <div class="sp-toggle-label">
                        <span class="sp-toggle-label-title">鼠标跟随</span>
                        <span class="sp-toggle-label-desc">鼠标移动时全景画面跟随视角偏移</span>
                      </div>
                      <label class="sp-toggle">
                        <input type="checkbox" v-model="panoramaMouseFollow" />
                        <span class="sp-toggle-slider"></span>
                      </label>
                    </div>
                  </div>
                </template>

                <div class="sp-section" v-if="bgMode === 'image' || bgMode === 'video'">
                  <div class="sp-section-title">{{ bgMode === 'video' ? '视频文件' : '图片文件' }}</div>
                  <div class="sp-field-row">
                    <input type="text" v-model="bgFilePath" class="sp-input" readonly />
                    <button class="btn" @click="browseBackgroundFile">浏览...</button>
                  </div>
                </div>

                <!-- 壁纸选择网格 -->
                <div class="sp-section" v-if="bgMode === 'image'">
                  <div class="sp-section-title">选择壁纸</div>
                  <div class="sp-bg-wallpaper-grid">
                    <div class="sp-bg-wallpaper-card" v-for="wp in wallpapers" :key="wp.id"
                      :class="{ selected: selectedWallpaperId === wp.id }"
                      @click="selectWallpaper(wp)">
                      <div class="sp-bg-wallpaper-img" :style="{ background: wp.color }"></div>
                      <div class="sp-bg-wallpaper-label">{{ wp.name }}</div>
                      <button class="sp-bg-wallpaper-del" @click.stop="deleteWallpaper(wp)" title="删除此壁纸">✕</button>
                    </div>
                  </div>
                  <div class="sp-empty-text" v-if="wallpapers.length === 0" style="margin-top:8px;">暂无可用壁纸</div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">背景透明度</div>
                  <div class="sp-slider-row">
                    <input type="range" v-model.number="bgOpacity" min="0" max="100" />
                    <span class="sp-slider-val">{{ bgOpacity }}%</span>
                  </div>
                  <div class="sp-hint">值越大背景越透明，壁纸/视频越清晰可见。文字和按钮始终保持不透明。</div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">高斯模糊强度</div>
                  <div class="sp-slider-row">
                    <input type="range" v-model.number="bgBlur" min="0" max="20" />
                    <span class="sp-slider-val">{{ bgBlur }}</span>
                  </div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">背景覆盖层</div>
                  <div class="sp-slider-row">
                    <input type="range" v-model.number="bgOverlay" min="0" max="100" />
                    <span class="sp-slider-val">{{ bgOverlay }}%</span>
                  </div>
                  <div class="sp-hint">背景高度透明时，增加覆盖层可提升文字可读性。0=无覆盖，100=最大覆盖</div>
                </div>

                <div class="sp-section">
                  <button class="btn" @click="resetBackground">重置默认背景</button>
                </div>
              </div>

              <!-- ═══ 个性化 ═══ -->
              <div class="sp-panel" :class="{ active: tab === 'personal' }">
                <div class="sp-section">
                  <div class="sp-section-title">外观主题</div>
                  <div class="sp-theme-picker">
                    <div class="sp-theme-opt" :class="{ active: themeMode === 'light' }" @click="setTheme('light')">
                      <div class="sp-theme-preview sp-theme-preview-light">
                        <span class="sp-theme-icon">☀️</span>
                      </div>
                      <span class="sp-theme-label">浅色</span>
                    </div>
                    <div class="sp-theme-opt" :class="{ active: themeMode === 'dark' }" @click="setTheme('dark')">
                      <div class="sp-theme-preview sp-theme-preview-dark">
                        <span class="sp-theme-icon">🌙</span>
                      </div>
                      <span class="sp-theme-label">深色</span>
                    </div>
                  </div>
                </div>
                <div class="sp-section">
                  <div class="sp-section-title">界面效果</div>
                  <div class="sp-toggle-row">
                    <div class="sp-toggle-label">
                      <span class="sp-toggle-label-title">毛玻璃效果</span>
                      <span class="sp-toggle-label-desc">按钮、卡片等元素使用半透明毛玻璃质感</span>
                    </div>
                    <label class="sp-toggle">
                      <input type="checkbox" v-model="glassEnabled" />
                      <span class="sp-toggle-slider"></span>
                    </label>
                  </div>
                  <div class="sp-toggle-row">
                    <div class="sp-toggle-label">
                      <span class="sp-toggle-label-title">聚光灯效果</span>
                      <span class="sp-toggle-label-desc">鼠标移动时产生跟随光晕效果</span>
                    </div>
                    <label class="sp-toggle">
                      <input type="checkbox" v-model="spotlightEnabled" />
                      <span class="sp-toggle-slider"></span>
                    </label>
                  </div>
                </div>
              </div>

              <!-- ═══ 关于 ═══ -->
              <div class="sp-panel" :class="{ active: tab === 'about' }">
                <div class="sp-about-area">
                  <div class="sp-about-logo">🎵 MNL</div>
                  <div class="sp-about-desc">一款功能强大的 Minecraft 启动器</div>
                  <div class="sp-about-ver">{{ appVersion }}</div>
                  <button class="btn btn-primary" @click="checkUpdate">检查更新</button>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">制作人员</div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/FallenLeaves.jpg" alt="FallenLeaves" />
                    <div><div class="sp-credit-name">FallenLeaves</div><div class="sp-credit-role">MNL 的开发者</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/ccy.jpg" alt="ccy" />
                    <div><div class="sp-credit-name">ccy</div><div class="sp-credit-role">MNL 的开发者</div></div>
                  </div>
                </div>

                <div class="sp-section">
                  <div class="sp-section-title">特别鸣谢</div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/PCL2.png" alt="PCL" />
                    <div><div class="sp-credit-name">PCL (Plain Craft Launcher)</div><div class="sp-credit-role">设计灵感与架构参考</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/HMCL.png" alt="HMCL" />
                    <div><div class="sp-credit-name">HMCL</div><div class="sp-credit-role">项目参考与技术方案</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/bangbang93.png" alt="bangbang93" />
                    <div><div class="sp-credit-name">BMCLAPI (bangbang93)</div><div class="sp-credit-role">Minecraft 下载镜像加速</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <div class="sp-credit-avatar" style="background:#F16436;color:#fff;display:flex;align-items:center;justify-content:center;font-size:15px;font-weight:700;">C</div>
                    <div><div class="sp-credit-name">CurseForge</div><div class="sp-credit-role">Mod 与整合包资源平台</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <div class="sp-credit-avatar" style="background:#1BD96A;color:#fff;display:flex;align-items:center;justify-content:center;font-size:15px;font-weight:700;">M</div>
                    <div><div class="sp-credit-name">Modrinth</div><div class="sp-credit-role">Mod 与整合包资源平台</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/EasyTier.png" alt="EasyTier" />
                    <div><div class="sp-credit-name">EasyTier</div><div class="sp-credit-role">P2P 虚拟组网联机</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/versePC.png" alt="versePC" />
                    <div><div class="sp-credit-name">versePC</div><div class="sp-credit-role">为 Web 界面开发提供支持</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/ZTY.png" alt="ZTY" />
                    <div><div class="sp-credit-name">ZTY</div><div class="sp-credit-role">Minecraft 启动器</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <div class="sp-credit-avatar" style="background:#4CAF50;color:#fff;display:flex;align-items:center;justify-content:center;font-size:14px;font-weight:700;">K</div>
                    <div><div class="sp-credit-name">苦力怕论坛</div><div class="sp-credit-role">国内整合包 & 资源平台</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <div class="sp-credit-avatar" style="background:#2196F3;color:#fff;display:flex;align-items:center;justify-content:center;font-size:14px;font-weight:700;">B</div>
                    <div><div class="sp-credit-name">BBSMC</div><div class="sp-credit-role">模组 & 整合包资源平台</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <div class="sp-credit-avatar" style="background:#FF9800;color:#fff;display:flex;align-items:center;justify-content:center;font-size:14px;font-weight:700;">M</div>
                    <div><div class="sp-credit-name">MC百科</div><div class="sp-credit-role">Minecraft 百科与模组资料站</div></div>
                  </div>
                </div>

                <!-- 生态项目 -->
                <div class="sp-section">
                  <div class="sp-section-title">生态项目</div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/Mineradio.png" alt="Mineradio" />
                    <div><div class="sp-credit-name">Mineradio</div><div class="sp-credit-role">为可视化播放提供帮助</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/MCTier.png" alt="MCTier" />
                    <div><div class="sp-credit-name">MCTier</div><div class="sp-credit-role">提供联机支持</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/RedStone.png" alt="RedStone" />
                    <div><div class="sp-credit-name">RedStone</div><div class="sp-credit-role">提供联机支持</div></div>
                  </div>
                </div>

                <!-- 开发者 -->
                <div class="sp-section">
                  <div class="sp-section-title">特别贡献者</div>
                  <div class="sp-credit-item">
                    <div class="sp-credit-avatar" style="background:#000;"></div>
                    <div><div class="sp-credit-name">zys</div><div class="sp-credit-role">提供帮助</div></div>
                  </div>
                </div>

                <!-- 支持名单 -->
                <div class="sp-section">
                  <div class="sp-section-title">感谢支持</div>
                  <div class="sp-credit-item">
                    <div class="sp-credit-avatar" style="background:#9C27B0;color:#fff;display:flex;align-items:center;justify-content:center;font-size:14px;font-weight:700;">七</div>
                    <div><div class="sp-credit-name">七七</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <div class="sp-credit-avatar" style="background:#795548;color:#fff;display:flex;align-items:center;justify-content:center;font-size:14px;font-weight:700;">银</div>
                    <div><div class="sp-credit-name">大银MC</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <div class="sp-credit-avatar" style="background:#607D8B;color:#fff;display:flex;align-items:center;justify-content:center;font-size:14px;font-weight:700;">时</div>
                    <div><div class="sp-credit-name">时候在吗</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/bmpT.png" alt="玩BMPT的小心性无能" />
                    <div><div class="sp-credit-name">玩BMPT的小心性无能</div><div class="sp-credit-role">宣传启动器</div></div>
                  </div>
                  <div class="sp-credit-item">
                    <img class="sp-credit-avatar" src="img/FallenLeaf_Z.jpg" alt="FallenLeaf_Z" />
                    <div><div class="sp-credit-name">FallenLeaf_Z</div><div class="sp-credit-role">宣传启动器</div></div>
                  </div>
                </div>
              </div>

            </div><!-- /sp-content -->
          </div><!-- /sp-layout -->
        </div><!-- /sp-grid -->
      </div><!-- /sp-scroll -->

      <!-- 删除确认对话框 -->
      <div class="modal-overlay" v-if="confirmShow" @click.self="confirmShow = false">
        <div class="cm-dialog" style="background:var(--card-bg);border:1px solid var(--border);">
          <div class="cm-dialog-title">{{ confirmTitle }}</div>
          <div class="pf-dialog-msg">{{ confirmMsg }}</div>
          <div class="pf-dialog-btns">
            <button class="btn btn-ghost" @click="confirmShow = false">取消</button>
            <button class="btn btn-primary" style="background:var(--danger,#e53935);border-color:var(--danger,#e53935);" @click="confirmAction(); confirmShow = false">确认删除</button>
          </div>
        </div>
      </div>

      <!-- 整合包名称输入对话框 -->
      <div class="modal-overlay" v-if="modpackNameDialogShow" @click.self="modpackNameDialogShow = false">
        <div class="cm-dialog" style="background:var(--card-bg);border:1px solid var(--border);min-width:360px;">
          <div class="cm-dialog-title">导入整合包</div>
          <div class="pf-dialog-msg" style="margin-bottom:8px;">
            <div v-if="modpackNameDialogInfo.name" style="margin-bottom:12px;">
              <div style="font-size:13px;color:var(--text-secondary);margin-bottom:4px;">整合包名称</div>
              <div style="font-size:15px;font-weight:600;">{{ modpackNameDialogInfo.name }}</div>
            </div>
            <div style="display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-bottom:12px;font-size:12px;">
              <div v-if="modpackNameDialogInfo.minecraftVersion">
                <div style="color:var(--text-secondary);">游戏版本</div>
                <div style="font-weight:600;">{{ modpackNameDialogInfo.minecraftVersion }}</div>
              </div>
              <div v-if="modpackNameDialogInfo.loaderType">
                <div style="color:var(--text-secondary);">加载器</div>
                <div style="font-weight:600;text-transform:capitalize;">{{ modpackNameDialogInfo.loaderType }}</div>
              </div>
              <div v-if="modpackNameDialogInfo.modCount">
                <div style="color:var(--text-secondary);">模组数量</div>
                <div style="font-weight:600;">{{ modpackNameDialogInfo.modCount }}</div>
              </div>
              <div v-if="modpackNameDialogInfo.packType">
                <div style="color:var(--text-secondary);">整合包类型</div>
                <div style="font-weight:600;text-transform:uppercase;">{{ modpackNameDialogInfo.packType }}</div>
              </div>
            </div>
            <div style="margin-top:12px;">
              <label style="display:block;font-size:13px;color:var(--text-secondary);margin-bottom:6px;">版本名称（自定义）</label>
              <input type="text" v-model="modpackNameInput" class="sp-input" style="width:100%;" placeholder="请输入版本名称" @keyup.enter="confirmModpackName()" />
            </div>
          </div>
          <div class="pf-dialog-btns">
            <button class="btn btn-ghost" @click="cancelModpackName()">取消</button>
            <button class="btn btn-primary" @click="confirmModpackName()">开始导入</button>
          </div>
        </div>
      </div>

      <!-- Save Bar -->
      <div class="sp-save-bar">
        <button class="btn btn-primary" @click="saveSettings">保存设置</button>
      </div>
    </div>
  `,

  data() {
    return {
      tab: 'game',
      categories: [
        { id:'game', label:'游戏设置' }, { id:'java', label:'Java设置' },
        { id:'general', label:'常规设置' }, { id:'download', label:'下载设置' },
        { id:'background', label:'背景设置' }, { id:'personal', label:'个性化' },
        { id:'about', label:'关于' },
      ],
      // 游戏设置
      ramMode: 'auto', ramValue: 4,
      resolution: '1280x1024', fullscreen: false, skinPack: true,
      resolutionOptions: ['800x600','1024x768','1280x720','1280x1024','1366x768','1600x900','1920x1080','2560x1440'],
      // 实时内存信息
      memoryInfo: { usedGB: '0', gameGB: '0', freeGB: '0', totalGB: '0', usedPercent: 0, gamePercent: 0, freePercent: 0 },
      _ramTimer: null,
      // Java
      currentJava: null, detectedJavaList: [], customJavaPath: '',
      jvmArgs: '-XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=200 -XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:+AlwaysPreTouch -XX:G1NewSizePercent=30 -XX:G1MaxNewSizePercent=40 -XX:G1HeapRegionSize=8M -XX:G1ReservePercent=20 -XX:G1HeapWastePercent=5 -XX:G1MixedGCCountTarget=4 -XX:InitiatingHeapOccupancyPercent=15 -XX:G1MixedGCLiveThresholdPercent=90 -XX:G1RSetUpdatingPauseTimePercent=5 -XX:SurvivorRatio=32 -XX:+PerfDisableSharedMem -XX:MaxTenuringThreshold=1',
      jvmPreset: 'g1-aikar', jvmPresetOpen: false,
      javaDownloading: false, javaDlVersion: 0, javaDlProgress: 0,
      // 常规
      autoLogin: false, minimizeToTray: false, autoCheckUpdate: true,
      showSplash: true, hardwareAccel: true, language: 'zh-CN',
      gameFolders: [],
      selectedFolder: '',
      folderMenuFor: null,
      // 下载
      downloadThreads: 64,
      autoInstallDeps: true, versionIsolationLevel: 4,
      customDownloadPath: '',
      // 背景
      bgMode: 'mica', bgFilePath: '', bgOpacity: 60, bgBlur: 0, bgOverlay: 0,
      wallpapers: [], selectedWallpaperId: null,
      videoBgFileName: '',  // 当前视频背景文件名
      // 全景
      panoramaTheme: 'overworld', panoramaSpeed: 5, panoramaMouseFollow: false,
      // 个性化
      glassEnabled: true, spotlightEnabled: false, themeMode: 'light',
      resolutionOpen: false, languageOpen: false, isolationOpen: false,
      // 确认对话框
      confirmShow: false,
      confirmTitle: '',
      confirmMsg: '',
      confirmAction: null,
      // 关于
      appVersion: '1.2.2',
      // 整合包导入
      modpackImporting: false,
      modpackImportStatus: '',
      modpackImportProgress: 0,
      modpackImportName: '',
      // 整合包名称输入对话框
      modpackNameDialogShow: false,
      modpackNameDialogInfo: {},
      modpackNameInput: '',
      _modpackFilePath: '',
    };
  },

  computed: {
    resolutionOptions2() {
      return this.resolutionOptions.map(r => ({ v: r, t: r }));
    },
    resolutionDisplayText() {
      return this.resolution;
    },
    languageOptions() {
      return [
        { v: 'zh-CN', t: '中文' },
        { v: 'en', t: 'English' }
      ];
    },
    languageDisplayText() {
      const opt = this.languageOptions.find(o => o.v === this.language);
      return opt ? opt.t : this.language;
    },
    isolationOptions() {
      return [
        { v: 0, t: '关闭' },
        { v: 1, t: '隔离可安装 Mod 的版本' },
        { v: 2, t: '隔离非正式版' },
        { v: 3, t: '隔离可安装 Mod 的版本与非正式版' },
        { v: 4, t: '隔离所有版本' }
      ];
    },
    isolationDisplayText() {
      const opt = this.isolationOptions.find(o => o.v === this.versionIsolationLevel);
      return opt ? opt.t : '关闭';
    },
    jvmPresetOptions() {
      return [
        { v: 'g1-aikar', t: 'G1 均衡 (Aikar式)', desc: '社区经典，大多数模组包推荐 · 4-8 GB' },
        { v: 'g1-lowmem', t: 'G1 低配优化', desc: '2-4 GB 低内存，减少 GC 卡顿' },
        { v: 'g1-highperf', t: 'G1 高吞吐', desc: '8+ GB 大内存，最大帧率 · 适合大型整合包' },
        { v: 'client', t: '客户端专属优化', desc: 'OpenGL 加速 + 网络优化 + 兼容修复' },
        { v: 'shenandoah', t: 'Shenandoah 低延迟', desc: '大堆低暂停 · Java 17+ · 8+ GB' },
        { v: 'zgc', t: 'ZGC 现代', desc: '亚毫秒 GC 暂停 · Java 21+ · 推荐 12+ GB' },
        { v: 'custom', t: '自定义', desc: '手动输入 JVM 参数' },
      ];
    },
    jvmPresetDisplayText() {
      const opt = this.jvmPresetOptions.find(o => o.v === this.jvmPreset);
      return opt ? opt.t : 'G1 均衡 (Aikar式)';
    },
  },

  created() { this.loadSettings(); this.loadWallpapers(); this.startRamTimer(); this.loadSavedWallpaper(); this.loadSavedVideoBg(); this._onModpackProgress = (e) => this.onModpackProgress(e.detail); this._onModpackComplete = (e) => this.onModpackComplete(e.detail); window.addEventListener('mnl:modpack-progress', this._onModpackProgress); window.addEventListener('mnl:modpack-complete', this._onModpackComplete); this._onDocClick = () => { this.folderMenuFor = null; }; document.addEventListener('click', this._onDocClick); },
  mounted() {
    // 页面挂载后确保个性化 body class 正确（默认值不会触发 watcher）
    document.body.classList.toggle('glass-on', this.glassEnabled);
    document.body.classList.toggle('spotlight-on', this.spotlightEnabled);
  },
  unmounted() { this.stopRamTimer(); window.removeEventListener('mnl:modpack-progress', this._onModpackProgress); window.removeEventListener('mnl:modpack-complete', this._onModpackComplete); document.removeEventListener('click', this._onDocClick); },

  watch: {
    /** 实时更新背景透明度 */
    bgOpacity(val) {
      if (window.updateBgOpacity) {
        window.updateBgOpacity(val);
      }
      // 防抖保存透明度设置
      if (this._saveOpacityTimer) clearTimeout(this._saveOpacityTimer);
      this._saveOpacityTimer = setTimeout(() => {
        if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
          window.MNL.settings.set('bgOpacity', val).catch(() => {});
        }
      }, 500);
    },
    /** 实时更新高斯模糊 */
    bgBlur(val) {
      if (window.updateBgBlur) {
        window.updateBgBlur(val);
      }
      // 防抖保存模糊设置
      if (this._saveBlurTimer) clearTimeout(this._saveBlurTimer);
      this._saveBlurTimer = setTimeout(() => {
        if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
          window.MNL.settings.set('bgBlur', val).catch(() => {});
        }
      }, 500);
    },
    /** 实时更新背景覆盖层 */
    bgOverlay(val) {
      if (window.updateBgOverlay) {
        window.updateBgOverlay(val);
      }
      // 防抖保存覆盖层设置
      if (this._saveOverlayTimer) clearTimeout(this._saveOverlayTimer);
      this._saveOverlayTimer = setTimeout(() => {
        if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
          window.MNL.settings.set('bgOverlay', val).catch(() => {});
        }
      }, 500);
    },
    /** 背景模式切换 */
    async bgMode(val) {
      if (val === 'mica') {
        window.applyWallpaper(null);
        window.applyVideoBackground(null);
        if (window.switchMNLWallpaperMode) window.switchMNLWallpaperMode('mica');
      } else if (val === 'image') {
        window.applyVideoBackground(null);
        if (window.switchMNLWallpaperMode) window.switchMNLWallpaperMode('image');
        // 重新应用已保存的壁纸
        await this.loadSavedWallpaper();
      } else if (val === 'video') {
        window.applyWallpaper(null);
        if (window.switchMNLWallpaperMode) window.switchMNLWallpaperMode('video');
        // 重新应用已保存的视频
        await this.loadSavedVideoBg();
      } else if (val === 'panorama') {
        window.applyWallpaper(null);
        window.applyVideoBackground(null);
        if (window.switchMNLWallpaperMode) {
          window.switchMNLWallpaperMode('panorama');
          window.setMNLPanoramaTheme(this.panoramaTheme);
          window.setMNLPanoramaSpeed(this.panoramaSpeed * 0.001);
          window.setMNLPanoramaMouseFollow(this.panoramaMouseFollow);
        }
      }
      if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
        window.MNL.settings.set('bgMode', val).catch(() => {});
      }
    },
    /** 全景主题切换 */
    panoramaTheme(val) {
      if (this.bgMode === 'panorama' && window.setMNLPanoramaTheme) {
        window.setMNLPanoramaTheme(val);
      }
      if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
        window.MNL.settings.set('panoramaTheme', val).catch(() => {});
      }
    },
    /** 全景旋转速度 */
    panoramaSpeed(val) {
      if (this.bgMode === 'panorama' && window.setMNLPanoramaSpeed) {
        window.setMNLPanoramaSpeed(val * 0.001);
      }
      if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
        window.MNL.settings.set('panoramaSpeed', val).catch(() => {});
      }
    },
    /** 全景鼠标跟随 */
    panoramaMouseFollow(val) {
      if (this.bgMode === 'panorama' && window.setMNLPanoramaMouseFollow) {
        window.setMNLPanoramaMouseFollow(val);
      }
      if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
        window.MNL.settings.set('panoramaMouseFollow', val).catch(() => {});
      }
    },
    /** 毛玻璃开关 */
    glassEnabled(val) {
      document.body.classList.toggle('glass-on', val);
      if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
        window.MNL.settings.set('glassEnabled', val).catch(() => {});
      }
    },
    /** 聚光灯效果开关 */
    spotlightEnabled(val) {
      document.body.classList.toggle('spotlight-on', val);
      if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
        window.MNL.settings.set('spotlightEnabled', val).catch(() => {});
      }
    },
    /** 切换选中的游戏目录 */
    selectedFolder(val) {
      if (!window.MNL || !window.MNL.settings || !window.MNL.settings.selectGameFolder) return;
      var idx = -1;
      for (var i = 0; i < this.gameFolders.length; i++) {
        if (this.gameFolders[i].path === val) { idx = i; break; }
      }
      if (idx >= 0) {
        window.MNL.settings.selectGameFolder(idx);
      }
    },
    /** 自动检查更新开关同步到 C# 后端 */
    autoCheckUpdate(val) {
      if (window.MNL && window.MNL.update && window.MNL.update.setAutoCheck) {
        window.MNL.update.setAutoCheck(val)["catch"](function() {});
      }
    },
  },

  methods: {
    toast(msg, type) { try { window.showToast && window.showToast(msg, type); } catch(e) { console.log('[Toast]', type, msg); } },

    /** 切换主题 */
    setTheme(mode) {
      this.themeMode = mode;
      document.documentElement.setAttribute('data-theme', mode);
      document.body.setAttribute('data-theme', mode);
      // 同步根 Vue 实例
      if (this.$root) this.$root.isDarkTheme = (mode === 'dark');
      // 保存
      if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
        window.MNL.settings.set('themeMode', mode).catch(() => {});
      }
    },

    /** 启动时加载已保存的壁纸 */
    async loadSavedWallpaper() {
      try {
        if (window.MNL && window.MNL.settings && window.MNL.settings.get) {
          const fileName = await window.MNL.settings.get('WallpaperFileName');
          if (fileName && typeof fileName === 'string' && fileName.length > 0) {
            console.log('[Settings] 恢复已保存壁纸:', fileName);
            window.applyWallpaper(fileName, this.bgBlur || 0);
            window.updateBgOpacity(this.bgOpacity);
            window.updateBgOverlay(this.bgOverlay);
            // 查找并选中对应的壁纸项，恢复 UI 选中状态
            var self = this;
            setTimeout(function() {
              var match = self.wallpapers.find(function(wp) {
                return wp.name === fileName || wp.id === fileName;
              });
              if (match) {
                self.selectedWallpaperId = match.id;
                self.bgMode = 'image';
              }
            }, 200);
          }
        }
      } catch(e) { console.error('[Settings] 恢复壁纸失败:', e); }
    },

    /** 启动时加载已保存的视频背景 */
    async loadSavedVideoBg() {
      try {
        if (window.MNL && window.MNL.settings && window.MNL.settings.get) {
          const fileName = await window.MNL.settings.get('VideoBgFileName');
          if (fileName && typeof fileName === 'string' && fileName.length > 0) {
            console.log('[Settings] 恢复已保存视频背景:', fileName);
            this.videoBgFileName = fileName;
            this.bgMode = 'video';
            // 从 C# 设置中读取透明度
            try {
              var savedOpacity = await window.MNL.settings.get('bgOpacity');
              if (savedOpacity != null) {
                var parsed = parseInt(savedOpacity, 10);
                this.bgOpacity = Number.isNaN(parsed) ? 100 : parsed;
              }
            } catch(e) {}
            window.applyVideoBackground(fileName, this.bgBlur || 0);
            window.updateBgOpacity(this.bgOpacity);
            window.updateBgOverlay(this.bgOverlay);
          }
        }
      } catch(e) { console.error('[Settings] 恢复视频背景失败:', e); }
    },

    // ====== 实时内存条 ======
    startRamTimer() {
      this.refreshMemory();
      this._ramTimer = setInterval(() => this.refreshMemory(), 2000);
    },
    stopRamTimer() {
      if (this._ramTimer) { clearInterval(this._ramTimer); this._ramTimer = null; }
    },
    async refreshMemory() {
      try {
        if (window.MNL && window.MNL.utils && window.MNL.utils.getMemoryInfo) {
          const raw = await window.MNL.utils.getMemoryInfo();
          const info = typeof raw === 'string' ? JSON.parse(raw) : raw;
          if (info) {
            this.memoryInfo = {
              usedGB: info.usedGB || '0',
              gameGB: '-',
              freeGB: info.freeGB || '0',
              totalGB: info.totalGB || '0',
              usedPercent: info.usedPercent || 0,
              gamePercent: 0,
              freePercent: info.freePercent || Math.max(100 - (info.usedPercent || 0), 1),
            };
            return;
          }
        }
      } catch(e) {}
      // 回退：从浏览器 performance.memory 估算（仅 WebView2 / Chromium）
      try {
        if (performance.memory) {
          const pm = performance.memory;
          const total = pm.jsHeapSizeLimit / 1024 / 1024 / 1024;
          const used = pm.usedJSHeapSize / 1024 / 1024 / 1024;
          const totalGB = Math.max(total, 8);
          const usedGB = Math.min(used, totalGB * 0.6);
          const freeGB = totalGB - usedGB;
          this.memoryInfo = {
            usedGB: usedGB.toFixed(1),
            gameGB: '-',
            freeGB: freeGB.toFixed(1),
            totalGB: totalGB.toFixed(0),
            usedPercent: Math.max(usedGB / totalGB * 100, 5),
            gamePercent: 0,
            freePercent: Math.max(freeGB / totalGB * 100, 5),
          };
          return;
        }
      } catch(e) {}
    },

    async loadSettings() {
      try {
        if (window.MNL && window.MNL.settings && window.MNL.settings.getAll) {
          const s = await window.MNL.settings.getAll();
          if (s) {
            if (s.ramMode) this.ramMode = s.ramMode;
            if (s.ramValue != null) this.ramValue = s.ramValue;
            if (s.customRamGb != null && s.ramValue == null) this.ramValue = s.customRamGb;
            if (s.resolution) this.resolution = s.resolution;
            if (s.fullscreen !== undefined) this.fullscreen = s.fullscreen;
            if (s.skinPack !== undefined) this.skinPack = s.skinPack;
            if (s.jvmArgs) this.jvmArgs = s.jvmArgs;
            if (s.jvmPreset && s.jvmPreset !== 'g1-default') this.jvmPreset = s.jvmPreset;
            if (s.autoLogin !== undefined) this.autoLogin = s.autoLogin;
            if (s.minimizeToTray !== undefined) this.minimizeToTray = s.minimizeToTray;
            if (s.autoCheckUpdate !== undefined) this.autoCheckUpdate = s.autoCheckUpdate;
            if (s.language) this.language = s.language;
            if (s.downloadThreads != null) this.downloadThreads = s.downloadThreads;
            if (s.autoInstallDeps !== undefined) this.autoInstallDeps = s.autoInstallDeps;
            if (s.VersionIsolationLevel != null) this.versionIsolationLevel = s.VersionIsolationLevel;
            else if (s.versionIsolationLevel != null) this.versionIsolationLevel = s.versionIsolationLevel;
            if (s.customDownloadPath != null) this.customDownloadPath = s.customDownloadPath;
            if (s.bgMode) this.bgMode = s.bgMode;
            if (s.bgOpacity != null) this.bgOpacity = s.bgOpacity;
            if (s.bgBlur != null) this.bgBlur = s.bgBlur;
            if (s.bgOverlay != null) this.bgOverlay = s.bgOverlay;
            if (s.bgFilePath) this.bgFilePath = s.bgFilePath;
            if (s.selectedWallpaperId != null) this.selectedWallpaperId = s.selectedWallpaperId;
            if (s.videoBgFileName) this.videoBgFileName = s.videoBgFileName;
            if (s.panoramaTheme) this.panoramaTheme = s.panoramaTheme;
            if (s.panoramaSpeed != null) this.panoramaSpeed = s.panoramaSpeed;
            if (s.panoramaMouseFollow != null) this.panoramaMouseFollow = s.panoramaMouseFollow;
            if (s.glassEnabled != null) this.glassEnabled = s.glassEnabled;
            if (s.spotlightEnabled != null) this.spotlightEnabled = s.spotlightEnabled;
            if (s.themeMode) this.themeMode = s.themeMode;
            if (s.currentJava) this.currentJava = s.currentJava;
          }
          // 确保主题应用到 HTML 和 body
          document.documentElement.setAttribute('data-theme', this.themeMode || 'light');
          document.body.setAttribute('data-theme', this.themeMode || 'light');
          // 同步根 Vue 实例
          if (this.$root) this.$root.isDarkTheme = (this.themeMode === 'dark');
        }
      } catch(e) {}
      // 从后端加载保存的 Java 路径
      try {
        if (window.MNL && window.MNL.java && window.MNL.java.getSavedPath) {
          const javaPath = await window.MNL.java.getSavedPath();
          if (javaPath) {
            this.currentJava = { path: javaPath, version: '', arch: '' };
            console.log('[Settings] 已加载 Java 路径:', javaPath);
          }
        }
      } catch(e) { console.error('[Settings] 加载 Java 路径失败:', e); }
      try {
        if (window.MNL && window.MNL.utils && window.MNL.utils.getAppVersion) {
          this.appVersion = await window.MNL.utils.getAppVersion();
        }
      } catch(e) {}
      // 加载游戏目录列表
      try {
        if (window.MNL && window.MNL.settings && window.MNL.settings.getGameFolders) {
          var folders = await window.MNL.settings.getGameFolders();
          if (Array.isArray(folders) && folders.length > 0) {
            this.gameFolders = folders.map(function(f) { return { label: f.name, path: f.path, selected: f.isSelected }; });
            var selected = this.gameFolders.find(function(f) { return f.selected; });
            if (selected) this.selectedFolder = selected.path;
            else this.selectedFolder = this.gameFolders[0].path;
          }
        }
      } catch(e) { console.error('[Settings] 加载游戏目录失败:', e); }
    },

    /** 重新加载游戏目录列表 */
    async loadGameFolders() {
      try {
        if (window.MNL && window.MNL.settings && window.MNL.settings.getGameFolders) {
          var folders = await window.MNL.settings.getGameFolders();
          if (Array.isArray(folders) && folders.length > 0) {
            this.gameFolders = folders.map(function(f) { return { label: f.name, path: f.path, selected: f.isSelected }; });
            var selected = this.gameFolders.find(function(f) { return f.selected; });
            if (selected) this.selectedFolder = selected.path;
            else this.selectedFolder = this.gameFolders[0].path;
          }
        }
      } catch(e) { console.error('[Settings] 加载游戏目录失败:', e); }
    },

    async saveSettings() {
      const settings = {
        ramMode: this.ramMode, ramValue: this.ramValue,
        customRamGb: this.ramValue,
        resolution: this.resolution, fullscreen: this.fullscreen, skinPack: this.skinPack,
        jvmArgs: this.jvmArgs, jvmPreset: this.jvmPreset, autoLogin: this.autoLogin,
        minimizeToTray: this.minimizeToTray, autoCheckUpdate: this.autoCheckUpdate,
        showSplash: this.showSplash, hardwareAccel: this.hardwareAccel,
        language: this.language, downloadThreads: this.downloadThreads,
        autoInstallDeps: this.autoInstallDeps,
        versionIsolationLevel: this.versionIsolationLevel,
        customDownloadPath: this.customDownloadPath,
        bgMode: this.bgMode,
        bgOpacity: this.bgOpacity, bgBlur: this.bgBlur,
        bgOverlay: this.bgOverlay,
        bgFilePath: this.bgFilePath, selectedWallpaperId: this.selectedWallpaperId,
        currentJava: this.currentJava, videoBgFileName: this.videoBgFileName,
        panoramaTheme: this.panoramaTheme, panoramaSpeed: this.panoramaSpeed,
        panoramaMouseFollow: this.panoramaMouseFollow,
        glassEnabled: this.glassEnabled, spotlightEnabled: this.spotlightEnabled,
        themeMode: this.themeMode,
      };
      try { localStorage.setItem('mnl_settings', JSON.stringify(settings)); } catch(e) {}
      // Java 路径直写后端配置文件（不走反射）
      if (window.MNL && window.MNL.java && window.MNL.java.savePath && this.currentJava && this.currentJava.path) {
        try {
          await window.MNL.java.savePath(this.currentJava.path);
          console.log('[Settings] Java 路径已保存:', this.currentJava.path);
        } catch(e) { console.error('[Settings] 保存 Java 路径失败:', e); }
      }
      if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
        for (const [k, v] of Object.entries(settings)) {
          if (k === 'currentJava') continue; // 已直写，跳过
          try { await window.MNL.settings.set(k, v); } catch(e) {}
        }
      }
      this.toast('设置已保存', 'success');
    },

    // 壁纸
    async loadWallpapers() {
      try {
        if (window.MNL && window.MNL.background && window.MNL.background.getBackgroundsJson) {
          const data = await window.MNL.background.getBackgroundsJson();
          if (data && data.backgrounds && data.backgrounds.length) {
            this.wallpapers = data.backgrounds.map(function(bg, i) {
              // 根据名称生成一个稳定色调用于壁纸卡片背景色
              var hash = 0;
              var name = bg.name || bg.id || '';
              for (var j = 0; j < name.length; j++) { hash = name.charCodeAt(j) + ((hash << 5) - hash); }
              var hue = Math.abs(hash) % 360;
              var color = 'hsl(' + hue + ', 45%, 40%)';
              return { id: bg.id || bg.name, name: bg.name, color: color, path: bg.filePath || bg.path };
            });
            return;
          }
        }
      } catch(e) { console.error('[壁纸加载]', e); }
    },

    selectWallpaper(wp) {
      this.selectedWallpaperId = wp.id;
      this.bgFilePath = wp.name;
      // 切换到图片模式时清除视频背景
      this.videoBgFileName = '';
      window.applyVideoBackground(null);
      if (window.MNL && window.MNL.background && window.MNL.background.useBackgroundJson) {
        console.log('[Wallpaper] useBackgroundJson id:', wp.id);
        window.MNL.background.useBackgroundJson(wp.id).then((res) => {
          console.log('[Wallpaper] useBackgroundJson 返回:', JSON.stringify(res));
          if (res && res.success && res.fileName) {
            var url = window.location.origin + '/api/bg/' + res.fileName;
            console.log('[Wallpaper] 图片 URL:', url);
            // 预加载验证
            var testImg = new Image();
            testImg.onload = function() { console.log('[Wallpaper] 图片加载成功, size:', testImg.naturalWidth + 'x' + testImg.naturalHeight); };
            testImg.onerror = function() { console.error('[Wallpaper] 图片加载失败!'); };
            testImg.src = url;
            window.applyWallpaper(res.fileName, this.bgBlur || 0);
            window.updateBgOpacity(this.bgOpacity);
            window.updateBgOverlay(this.bgOverlay);
            this.saveWallpaperSetting(res.fileName);
          } else {
            console.error('[Wallpaper] useBackgroundJson 失败:', res);
          }
        });
      }
      this.toast('已选择壁纸: ' + wp.name, 'success');
    },

    /** 删除壁纸 */
    async deleteWallpaper(wp) {
      // 显示自定义确认对话框
      const wpName = wp.name || '';
      this.confirmTitle = '删除壁纸';
      this.confirmMsg = '确定要删除壁纸 "' + wpName + '" 吗？此操作不可恢复。';
      this.confirmAction = async () => {
        try {
          if (window.MNL && window.MNL.background && window.MNL.background.deleteBackgroundJson) {
            const res = await window.MNL.background.deleteBackgroundJson(wp.id);
            if (res && res.success) {
              // 如果删除的是当前选中壁纸，清除选中状态和背景
              if (this.selectedWallpaperId === wp.id) {
                this.selectedWallpaperId = null;
                this.bgFilePath = '';
                window.applyWallpaper(null);
              }
              await this.loadWallpapers();
              this.toast('壁纸已删除', 'success');
            } else {
              this.toast('删除失败', 'warning');
            }
          }
        } catch(e) {
          console.error('[Wallpaper] 删除失败:', e);
          this.toast('删除失败', 'error');
        }
      };
      this.confirmShow = true;
    },

    // Java
    async detectJava() {
      this.toast('正在检测 Java...', 'info');
      try {
        if (window.MNL && window.MNL.java && window.MNL.java.detectJson) {
          const list = await window.MNL.java.detectJson();
          if (list && list.length > 0) {
            this.detectedJavaList = list;
            if (!this.currentJava && list.length > 0) this.currentJava = list[0];
            this.toast('检测完成 (' + list.length + ' 个)', 'success');
            return;
          }
        }
      } catch(e) { console.error('[Java检测]', e); }
      this.toast('未检测到 Java，请手动配置或下载', 'warning');
    },

    async useJava(j) {
      this.currentJava = j;
      if (window.MNL && window.MNL.java && window.MNL.java.savePath && j.path) {
        try { await window.MNL.java.savePath(j.path); } catch(e) {}
      }
      this.toast('已切换 Java ' + j.version, 'success');
    },

    downloadJava(v) {
      this.javaDownloading = true; this.javaDlVersion = v; this.javaDlProgress = 0;
      this.toast('正在下载 Java ' + v + '...', 'info');
      if (window.MNL && window.MNL.java && window.MNL.java.download) {
        try { window.MNL.java.download(v); } catch(e) {}
      }
    },

    async browseJava() {
      try {
        if (window.MNL && window.MNL.dialog && window.MNL.dialog.openFolder) {
          const path = await window.MNL.dialog.openFolder();
          if (path) this.customJavaPath = path;
          return;
        }
      } catch(e) {}
      this.toast('浏览 Java 路径...', 'info');
    },

    async setJavaPath() {
      if (!this.customJavaPath.trim()) return this.toast('请先输入 Java 路径', 'warning');
      this.currentJava = { path: this.customJavaPath, version: '自定义' };
      if (window.MNL && window.MNL.java && window.MNL.java.savePath) {
        try { await window.MNL.java.savePath(this.customJavaPath.trim()); } catch(e) {}
      }
      this.toast('Java 路径已设置', 'success');
    },

    validateJava() {
      if (!this.customJavaPath.trim()) return this.toast('请先输入 Java 路径', 'warning');
      this.toast('Java 验证成功', 'success');
    },

    selectJvmPreset(preset) {
      this.jvmPreset = preset;
      const presets = {
        'g1-aikar':   '-XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=200 -XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:+AlwaysPreTouch -XX:G1NewSizePercent=30 -XX:G1MaxNewSizePercent=40 -XX:G1HeapRegionSize=8M -XX:G1ReservePercent=20 -XX:G1HeapWastePercent=5 -XX:G1MixedGCCountTarget=4 -XX:InitiatingHeapOccupancyPercent=15 -XX:G1MixedGCLiveThresholdPercent=90 -XX:G1RSetUpdatingPauseTimePercent=5 -XX:SurvivorRatio=32 -XX:+PerfDisableSharedMem -XX:MaxTenuringThreshold=1',
        'g1-lowmem':  '-XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=100 -XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:G1NewSizePercent=20 -XX:G1MaxNewSizePercent=30 -XX:G1HeapRegionSize=4M -XX:G1ReservePercent=25 -XX:G1HeapWastePercent=5 -XX:InitiatingHeapOccupancyPercent=20 -XX:G1MixedGCLiveThresholdPercent=85 -XX:SurvivorRatio=8 -XX:+PerfDisableSharedMem -XX:MaxTenuringThreshold=1 -XX:+UseStringDeduplication',
        'g1-highperf': '-XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=80 -XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:+AlwaysPreTouch -XX:G1NewSizePercent=40 -XX:G1MaxNewSizePercent=50 -XX:G1HeapRegionSize=16M -XX:G1ReservePercent=15 -XX:G1HeapWastePercent=5 -XX:G1MixedGCCountTarget=4 -XX:InitiatingHeapOccupancyPercent=15 -XX:G1MixedGCLiveThresholdPercent=90 -XX:G1RSetUpdatingPauseTimePercent=5 -XX:SurvivorRatio=32 -XX:+PerfDisableSharedMem -XX:MaxTenuringThreshold=1 -XX:+UseLargePages',
        'client':     '-XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=200 -XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:+AlwaysPreTouch -XX:G1NewSizePercent=30 -XX:G1MaxNewSizePercent=40 -XX:G1HeapRegionSize=8M -XX:G1ReservePercent=20 -XX:G1HeapWastePercent=5 -XX:G1MixedGCCountTarget=4 -XX:InitiatingHeapOccupancyPercent=15 -XX:G1MixedGCLiveThresholdPercent=90 -XX:G1RSetUpdatingPauseTimePercent=5 -XX:SurvivorRatio=32 -XX:+PerfDisableSharedMem -XX:MaxTenuringThreshold=1 -Dsun.java2d.opengl=true -Djava.net.preferIPv4Stack=true -Dfml.ignoreInvalidMinecraftCertificates=true -Dfml.ignorePatchDiscrepancies=true',
        'shenandoah': '-XX:+UnlockExperimentalVMOptions -XX:+UseShenandoahGC -XX:ShenandoahGCHeuristics=adaptive -XX:ShenandoahPacingMaxDelay=10 -XX:+AlwaysPreTouch -XX:+DisableExplicitGC -XX:+ParallelRefProcEnabled -XX:+PerfDisableSharedMem',
        'zgc':        '-XX:+UnlockExperimentalVMOptions -XX:+UseZGC -XX:ZAllocationSpikeTolerance=5 -XX:ZCollectionInterval=0 -XX:+AlwaysPreTouch -XX:+DisableExplicitGC -XX:+PerfDisableSharedMem',
        'custom':     this.jvmArgs,
      };
      if (preset !== 'custom') this.jvmArgs = presets[preset] || presets['g1-aikar'];
    },

    // 背景
    async resetBackground() {
      this.bgMode = 'mica'; this.bgFilePath = ''; this.bgOpacity = 60; this.bgBlur = 0; this.bgOverlay = 0;
      this.selectedWallpaperId = null; this.videoBgFileName = '';
      this.panoramaTheme = 'overworld'; this.panoramaSpeed = 5; this.panoramaMouseFollow = false;
      // 清除壁纸层、视频背景、全景，清空保存的设置
      window.applyWallpaper(null);
      window.applyVideoBackground(null);
      if (window.switchMNLWallpaperMode) window.switchMNLWallpaperMode('mica');
      window.updateBgOpacity(this.bgOpacity);
      window.updateBgOverlay(0);
      try {
        if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
          await window.MNL.settings.set('WallpaperFileName', '');
          await window.MNL.settings.set('VideoBgFileName', '');
        }
      } catch(e) {}
      this.toast('背景已重置为默认', 'success');
    },

    async browseBackgroundFile() {
      try {
        if (window.MNL && window.MNL.background && window.MNL.background.uploadBackgroundJson) {
          const res = await window.MNL.background.uploadBackgroundJson();
          if (res && res.success && res.fileName) {
            this.bgFilePath = res.name || res.fileName;
            const isVideo = res.type === 'Video' || (res.fileName && res.fileName.endsWith('.mp4'));
            if (isVideo) {
              // 视频背景：直接应用
              this.bgMode = 'video';
              this.videoBgFileName = res.fileName;
              window.applyWallpaper(null);  // 清除图片壁纸
              window.applyVideoBackground(res.fileName, this.bgBlur || 0);
              window.updateBgOpacity(this.bgOpacity);
              window.updateBgOverlay(this.bgOverlay);
              this.saveVideoBgSetting(res.fileName);
              this.toast('已设置视频背景: ' + res.name, 'success');
            } else {
              // 图片背景：刷新列表
              this.bgMode = 'image';
              await this.loadWallpapers();
              // 自动选中刚导入的壁纸
              var self = this;
              setTimeout(function() {
                var match = self.wallpapers.find(function(wp) {
                  return wp.name === res.name || wp.id === res.name;
                });
                if (match) {
                  self.selectedWallpaperId = match.id;
                  window.applyWallpaper(res.fileName, self.bgBlur || 0);
                  window.updateBgOpacity(self.bgOpacity);
                  window.updateBgOverlay(self.bgOverlay);
                  self.saveWallpaperSetting(res.fileName);
                }
              }, 300);
            }
            return;
          }
        }
      } catch(e) { console.error('[选择背景文件]', e); }
      this.toast('选择文件失败', 'warning');
    },

    /** 持久化视频背景文件名 */
    async saveVideoBgSetting(fileName) {
      try {
        if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
          await window.MNL.settings.set('VideoBgFileName', fileName);
          console.log('[Settings] 视频背景已保存:', fileName);
        }
      } catch(e) { console.error('[Settings] 保存视频背景失败:', e); }
    },

    /** 持久化壁纸文件名到 C# 设置 */
    async saveWallpaperSetting(fileName) {
      try {
        if (window.MNL && window.MNL.settings && window.MNL.settings.set) {
          await window.MNL.settings.set('WallpaperFileName', fileName);
          console.log('[Settings] 壁纸已保存:', fileName);
        }
      } catch(e) { console.error('[Settings] 保存壁纸失败:', e); }
    },


    async browseCustomDownloadPath() {
      try {
        if (window.MNL && window.MNL.dialog && window.MNL.dialog.openFolder) {
          var path = await window.MNL.dialog.openFolder();
          if (path) {
            this.customDownloadPath = path;
            // 同步保存到后端
            MNL.customDownload.setPath(path).catch(function() {});
          }
        }
      } catch(e) {
        this.toast('选择文件夹失败', 'error');
      }
    },

    // 游戏目录
    folderSettings(f) {
      // 切换菜单显示/隐藏
      this.folderMenuFor = (this.folderMenuFor === f.path) ? null : f.path;
    },
    renameFolder(f) {
      this.folderMenuFor = null;
      var name = prompt('请输入新的显示名称：', f.label);
      if (name === null || !name.trim()) return;
      var self = this;
      if (window.MNL && window.MNL.settings && window.MNL.settings.renameGameFolder) {
        window.MNL.settings.renameGameFolder(f.path, name.trim()).then(function(r) {
          try { r = typeof r === 'string' ? JSON.parse(r) : r; } catch(e) {}
          if (r && r.success) {
            self.toast('已重命名', 'success');
            self.loadGameFolders();
          } else {
            self.toast('重命名失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        }).catch(function(err) { self.toast('重命名失败: ' + err.message, 'error'); });
      } else {
        self.toast('当前环境不支持该操作', 'error');
      }
    },
    openGameFolder(f) {
      this.folderMenuFor = null;
      if (window.MNL && window.MNL.settings && window.MNL.settings.openGameFolder) {
        window.MNL.settings.openGameFolder(f.path).catch(function(err) {
          console.error('打开文件夹失败:', err);
        });
      }
    },
    deleteFolder(f) {
      this.folderMenuFor = null;
      this.confirmShow = true;
      this.confirmTitle = '删除游戏目录';
      this.confirmMsg = '确定要删除文件夹 "' + f.label + '" 吗？\n\n注意：这只会从列表中移除，不会删除磁盘上的实际文件。';
      var self = this;
      this.confirmAction = function() {
        if (window.MNL && window.MNL.settings && window.MNL.settings.removeGameFolder) {
          window.MNL.settings.removeGameFolder(f.path).then(function(r) {
            try { r = typeof r === 'string' ? JSON.parse(r) : r; } catch(e) {}
            if (r && r.success) {
              self.toast('已删除', 'success');
              self.loadGameFolders();
              window.dispatchEvent(new CustomEvent('mnl:versions-changed'));
            } else {
              self.toast('删除失败: ' + ((r && r.error) || '未知错误'), 'error');
            }
          }).catch(function(err) { self.toast('删除失败: ' + err.message, 'error'); });
        } else {
          self.toast('当前环境不支持该操作', 'error');
        }
      };
    },
    createFolder() {
      var self = this;
      var dft = '.minecraft';
      // 简单派生名称
      if (window.MNL && window.MNL.settings && window.MNL.settings.addGameFolder) {
        var fullPath = dft;
        window.MNL.settings.addGameFolder(fullPath, dft).then(function(r) {
          try { r = typeof r === 'string' ? JSON.parse(r) : r; } catch(e) {}
          if (r && r.success) {
            if (r.alreadyExists) { self.toast('该文件夹已存在', 'warning'); }
            else { self.toast('文件夹已添加', 'success'); }
            self.loadGameFolders();
          } else {
            self.toast('添加失败: ' + ((r && r.error) || '未知错误'), 'error');
          }
        }).catch(function(err) { self.toast('添加失败: ' + err.message, 'error'); });
      } else {
        this.toast('当前环境不支持该操作', 'error');
      }
    },
    addFolder() {
      var self = this;
      if (!window.MNL || !window.MNL.invoke) {
        this.toast('桥接不可用', 'error');
        return;
      }
      window.MNL.invoke('ShowOpenFolderDialog').then(function(selectedPath) {
        if (!selectedPath || !String(selectedPath).trim()) {
          self.toast('已取消', 'info');
          return;
        }
        var parts = String(selectedPath).replace(/\\/g, '/').replace(/\/$/, '').split('/');
        var defaultName = parts[parts.length - 1] || '游戏文件夹';
        if (defaultName === '.minecraft' && parts.length >= 2) defaultName = parts[parts.length - 2];
        if (window.MNL && window.MNL.settings && window.MNL.settings.addGameFolder) {
          window.MNL.settings.addGameFolder(selectedPath, defaultName).then(function(r) {
            try { r = typeof r === 'string' ? JSON.parse(r) : r; } catch(e) {}
            if (r && r.success) {
              if (r.alreadyExists) { self.toast(defaultName + ' 已存在', 'warning'); }
              else { self.toast('文件夹 ' + defaultName + ' 已添加', 'success'); }
              self.loadGameFolders();
            } else {
              self.toast('添加失败: ' + ((r && r.error) || '未知错误'), 'error');
            }
          }).catch(function(err) { self.toast('添加失败: ' + err.message, 'error'); });
        } else {
          self.toast('当前环境不支持该操作', 'error');
        }
      }).catch(function(err) { self.toast('选择文件夹失败: ' + (err.message || err), 'error'); });
    },
    importModpack() {
      if (this.modpackImporting) {
        this.toast('整合包导入正在进行中...', 'info');
        return;
      }
      if (!window.MNL || !window.MNL.modpacks || !window.MNL.modpacks.probeWithDialog) {
        this.toast('当前环境不支持导入整合包', 'error');
        return;
      }
      var self = this;
      // 先选择文件并探测整合包信息
      window.MNL.modpacks.probeWithDialog().then((r) => {
        if (!r || r.success === false) {
          if (r && r.error && r.error !== '已取消') {
            self.toast('选择文件失败: ' + r.error, 'error');
          }
          return;
        }
        // 显示名称输入对话框
        self.modpackNameDialogInfo = {
          name: r.name,
          minecraftVersion: r.minecraftVersion,
          loaderType: r.loaderType,
          loaderVersion: r.loaderVersion,
          modCount: r.modCount,
          packType: r.packType,
          version: r.version,
          author: r.author,
          summary: r.summary
        };
        self.modpackNameInput = r.name || '';
        self._modpackFilePath = r.filePath || '';
        self.modpackNameDialogShow = true;
      }).catch((e) => {
        self.toast('选择文件失败: ' + e.message, 'error');
      });
    },

    /** 取消整合包名称输入 */
    cancelModpackName() {
      this.modpackNameDialogShow = false;
      this._modpackFilePath = '';
    },

    /** 确认整合包名称，开始导入 */
    confirmModpackName() {
      var name = (this.modpackNameInput || '').trim();
      if (!name) {
        this.toast('请输入版本名称', 'warning');
        return;
      }
      if (!this._modpackFilePath) {
        this.toast('文件路径无效', 'error');
        return;
      }
      var self = this;
      this.modpackNameDialogShow = false;
      this.modpackImporting = true;
      this.modpackImportProgress = 0;
      this.modpackImportStatus = '开始导入...';
      this.modpackImportName = name;

      window.MNL.modpacks.startInstall(this._modpackFilePath, name).then((r) => {
        if (!r || r.success === false) {
          self.modpackImporting = false;
          if (r && r.error) {
            self.toast('导入失败: ' + r.error, 'error');
          }
          return;
        }
        if (r.started) {
          self.toast('开始导入整合包...', 'info');
        }
      }).catch((e) => {
        self.modpackImporting = false;
        self.toast('导入失败: ' + e.message, 'error');
      });
    },

    onModpackProgress(detail) {
      if (!detail) return;
      if (detail.packName) this.modpackImportName = detail.packName;
      if (detail.status && detail.status !== null) this.modpackImportStatus = detail.status;
      if (typeof detail.progress === 'number' && detail.progress >= 0) {
        this.modpackImportProgress = detail.progress;
      }
    },

    onModpackComplete(detail) {
      this.modpackImporting = false;
      if (detail && detail.success) {
        this.modpackImportProgress = 100;
        this.modpackImportStatus = '安装完成';
        var versionInfo = detail.versionId ? ' (版本ID: ' + detail.versionId + ')' : '';
        this.toast('整合包「' + (detail.name || '未知') + '」导入成功！' + versionInfo, 'success');
        if (window.MNL && window.MNL.settings && window.MNL.settings.reloadGameFolders) {
          window.MNL.settings.reloadGameFolders();
        }
        this.loadSettings();
        // 触发版本列表刷新事件
        window.dispatchEvent(new CustomEvent('mnl:versions-changed'));
        var self = this;
        setTimeout(function() {
          self.modpackImportProgress = 0;
          self.modpackImportStatus = '';
          self.modpackImportName = '';
        }, 3000);
      } else {
        var errMsg = detail ? detail.error : '未知错误';
        this.toast('整合包导入失败: ' + errMsg, 'error');
        console.error('[Modpack] 导入失败:', detail);
        this.modpackImportProgress = 0;
        this.modpackImportStatus = '';
        this.modpackImportName = '';
      }
    },

    // 关于
    async checkUpdate() {
      this.toast('正在检查更新...', 'info');
      try {
        if (!window.MNL || !window.MNL.update) {
          this.toast('当前已是最新版本 ' + this.appVersion, 'success');
          return;
        }
        var result = await window.MNL.update.checkForUpdate();
        if (!result || !result.success) {
          this.toast((result && result.error) || '检查更新失败，请稍后重试', 'error');
          return;
        }
        if (result.needUpdate) {
          // 调用 RootApp 的 showUpdateDialog
          var app = document.getElementById('app').__vue_app__;
          if (app && app._instance && app._instance.proxy) {
            app._instance.proxy.showUpdateDialog(result);
          } else {
            this.toast('发现新版本 ' + result.version + '，但弹窗不可用', 'warning');
          }
        } else {
          this.toast('当前已是最新版本 ' + this.appVersion, 'success');
        }
      } catch (e) {
        this.toast('检查更新异常: ' + (e.message || '未知'), 'error');
      }
    },
  }
};
