// ====== MusicPage v37 - togglePlay 同步执行 + a.load() 显式加载 ======
(function () {
  var DBG = window._dbg || function (msg) { console.log("[MUSIC-DBG]", msg); };
  try {
    DBG("music.js v37 loading");

    const MusicPage = {
      template: `
        <div class="page-content music-page">

          <div class="mp-container">

            <audio class="mp-audio" ref="mpAudio" preload="auto"></audio>

            <div class="mp-search">
              <div class="mp-search-box">
                <span class="mp-search-icon">🔍</span>
                <input type="text" class="mp-search-input" v-model="searchText" placeholder="搜索歌曲、歌手..." @keydown="onSearchKeydown" />
                <button class="mp-search-btn" @click="doSearch">搜索</button>
              </div>
              <div class="mp-login-area">
                <button class="mp-login-btn" v-if="!isLoggedIn" @click="doLogin">
                  <span>🎵</span><span> 登录网易云</span>
                </button>
                <div class="mp-logged-in" v-else @click="doLogout">
                  <div class="mp-avatar">{{ nickname.charAt(0) }}</div>
                  <span class="mp-nickname">{{ nickname }}</span>
                  <span class="mp-arrow"> ▼</span>
                </div>
              </div>
            </div>

            <div class="mp-content">
              <div class="mp-left">
                <div class="mp-panel-header">
                  <span class="mp-panel-title" v-if="currentView === 'recommend'">💿 推荐歌单</span>
                  <span class="mp-panel-title" v-else-if="currentView === 'search'">🔍 搜索结果</span>
                  <span class="mp-panel-title" v-else>📋 {{ selectedPlaylist ? selectedPlaylist.name : '歌单详情' }}</span>
                  <span class="mp-panel-count" v-if="currentView === 'recommend'">{{ playlists.length }} 个歌单</span>
                  <span class="mp-panel-count" v-else>{{ tracks.length }} 首</span>
                </div>
                <div class="mp-scroll-list">

                  <template v-if="currentView === 'recommend'">
                    <div class="mp-track-item mp-playlist-item" v-for="pl in playlists" :key="pl.id" @click="openPlaylist(pl)">
                      <div class="mp-track-cover"><img v-if="pl.coverUrl" :src="pl.coverUrl" alt="" /><span class="mp-cover-icon" v-else>🎵</span></div>
                      <div class="mp-track-info">
                        <div class="mp-track-name">{{ pl.name }}</div>
                        <div class="mp-track-sub">{{ pl.desc || '' }}</div>
                      </div>
                      <div class="mp-track-right">
                        <span class="mp-playlist-icon">📋</span>
                        <span class="mp-track-count">{{ pl.trackCount || 0 }}首</span>
                      </div>
                    </div>
                    <div class="mp-empty-state" v-if="playlists.length === 0">
                      <span class="mp-empty-icon">🎵</span>
                      <span>暂无推荐歌单</span>
                    </div>
                  </template>

                  <template v-if="currentView !== 'recommend'">
                    <div class="mp-track-item mp-song-item" v-for="t in tracks" :key="t.id"
                      @click="playTrack(t)" @dblclick="addToQueue(t)">
                      <div class="mp-track-cover"><img v-if="t.coverUrl" :src="t.coverUrl" alt="" /><span class="mp-cover-icon" v-else>🎵</span></div>
                      <div class="mp-track-info">
                        <div class="mp-track-name">{{ t.name || t.title }}</div>
                        <div class="mp-track-sub">{{ t.artist }}</div>
                      </div>
                      <div class="mp-track-right">
                        <span class="mp-track-duration">{{ t.duration }}</span>
                        <span class="mp-track-cached" v-if="t._cached">📥</span>
                      </div>
                    </div>
                    <div class="mp-empty-state" v-if="tracks.length === 0">
                      <span class="mp-empty-icon">{{ currentView === 'search' ? '📭' : '📋' }}</span>
                      <span>{{ currentView === 'search' ? '未找到相关歌曲' : '该歌单暂无歌曲' }}</span>
                      <span class="mp-empty-hint" v-if="currentView === 'search'">试试其他关键词</span>
                    </div>
                  </template>
                </div>

                <div class="mp-popup" v-if="popup.visible">
                  <div class="mp-popup-inner">
                    <span class="mp-popup-icon">{{ popup.icon }}</span>
                    <span class="mp-popup-text">{{ popup.text }}</span>
                    <span class="mp-popup-sub">{{ popup.sub }}</span>
                  </div>
                </div>
              </div>

              <div class="mp-right">
                <div class="mp-panel-header">
                  <span class="mp-panel-title">📋 播放队列</span>
                  <span class="mp-panel-count">{{ queue.length }} 首</span>
                  <button class="mp-clear-btn" v-if="queue.length > 0" @click="clearQueue">清空</button>
                </div>
                <div class="mp-scroll-list">
                  <div class="mp-empty-state" v-if="queue.length === 0">
                    <span class="mp-empty-icon">📋</span>
                    <span>播放队列为空</span>
                    <span class="mp-empty-hint">双击歌曲添加到队列</span>
                  </div>
                  <div class="mp-queue-item" v-for="(t, i) in queue" :key="'q'+i"
                    :class="{ 'mp-queue-active': currentIndex === i }"
                    @click="playIndex(i)">
                    <span class="mp-queue-icon">{{ currentIndex === i ? '🔊' : '🎵' }}</span>
                    <div class="mp-queue-info">
                      <div class="mp-queue-name">{{ t.name || t.title }}</div>
                      <div class="mp-queue-sub">{{ t.artist }}</div>
                    </div>
                    <span class="mp-queue-duration">{{ t.duration }}</span>
                    <button class="mp-queue-remove" @click.stop="removeFromQueue(i)">✕</button>
                  </div>
                </div>
              </div>
            </div>

            <div class="mp-player">
              <div class="mp-player-info">
                <div class="mp-player-cover"><img v-if="currentTrack && currentTrack.coverUrl" :src="currentTrack.coverUrl" alt="" /><span class="mp-player-nocover" v-else>🎵</span></div>
                <div class="mp-player-meta">
                  <div class="mp-player-title">{{ currentTrack ? (currentTrack.name || currentTrack.title) : '未在播放' }}</div>
                  <div class="mp-player-artist">{{ currentTrack ? currentTrack.artist : '' }}</div>
                </div>
              </div>

              <div class="mp-player-controls">
                <div class="mp-controls-btns">
                  <button class="mp-ctrl-btn" @click="prev" title="上一首">⏮</button>
                  <button class="mp-ctrl-btn mp-ctrl-play" @click="togglePlay" :title="playing ? '暂停' : '播放'">
                    {{ playing ? '⏸' : '▶' }}
                  </button>
                  <button class="mp-ctrl-btn" @click="next" title="下一首">⏭</button>
                  <button class="mp-ctrl-btn mp-ctrl-mode" @click="cyclePlayMode" :title="modeTitle">{{ modeIcon }}</button>
                  <button class="mp-ctrl-btn mp-ctrl-viz" @click="toggleVisualizer" title="进入全屏 3D 可视化播放">
                    <span class="viz-icon-bar"><span></span><span></span><span></span><span></span><span></span></span>
                    <span class="viz-label">🎆 可视化</span>
                  </button>
                </div>
                <div class="mp-progress">
                  <span class="mp-progress-time">{{ curTimeStr }}</span>
                  <div class="mp-progress-bar" ref="progressBar" @mousedown="startDrag">
                    <div class="mp-progress-track">
                      <div class="mp-progress-fill" :style="{ width: progressPct + '%' }"></div>
                      <div class="mp-progress-thumb" :style="{ left: progressPct + '%' }"></div>
                    </div>
                  </div>
                  <span class="mp-progress-time">{{ durTimeStr }}</span>
                </div>
              </div>

              <div class="mp-volume">
                <button class="mp-ctrl-btn" @click="cacheCurrent" title="缓存当前歌曲">📥</button>
                <span class="mp-volume-icon" @click="toggleMute" :title="muted ? '取消静音' : '静音'">
                  {{ volumeIcon }}
                </span>
                <div class="mp-volume-bar" ref="volumeBar" @click="seekVolume" @mousedown="startVolDrag">
                  <div class="mp-volume-track">
                    <div class="mp-volume-fill" :style="{ width: muted ? '0%' : volume + '%' }"></div>
                    <div class="mp-volume-thumb" :style="{ left: muted ? '0%' : volume + '%' }"></div>
                  </div>
                </div>
              </div>
            </div>

          </div>
        </div>
      `,

      data() {
        DBG("data() start");
        return {
          searchText: "",
          playlists: [],
          tracks: [],
          queue: [],
          currentIndex: -1,
          playing: false,
          muted: false,
          volume: 70,
          prevVolume: 70,
          currentTime: 0,
          duration: 0,
          playMode: "list",
          currentView: "recommend",
          selectedPlaylist: null,
          isLoggedIn: false,
          nickname: "音乐爱好者",
          isDragging: false,
          popup: { visible: false, icon: "", text: "", sub: "" },
          // 可视化状态（已废弃：现在跳转到独立 visualizer.html）
          visualizerActive: false,
        };
      },

      computed: {
        currentTrack() { return this.queue[this.currentIndex] || null; },
        progressPct() { return this.duration > 0 ? (this.currentTime / this.duration) * 100 : 0; },
        curTimeStr() { return this.fmtTime(this.currentTime); },
        durTimeStr() { return this.fmtTime(this.duration); },
        modeIcon() { return { list: "➡", loop: "🔁", single: "🔂", random: "🔀" }[this.playMode] || "➡"; },
        modeTitle() { return { list: "列表播放", loop: "列表循环", single: "单曲循环", random: "随机播放" }[this.playMode] || "列表播放"; },
        volumeIcon() { if (this.muted||this.volume===0) return "🔇"; if (this.volume<30) return "🔈"; if (this.volume<70) return "🔉"; return "🔊"; },
      },

      methods: {
        fmtTime: function(s) { if(isNaN(s)||s<0) return "0:00"; var m=Math.floor(s/60),sec=Math.floor(s%60); return m+":"+String(sec).padStart(2,"0"); },
        showPopup: function(icon, text, sub) { this.popup = { visible: true, icon: icon, text: text, sub: sub||"" }; var self=this; clearTimeout(this._pt); this._pt=setTimeout(function(){ self.popup.visible=false; },2000); },
        toast: function(msg, type) { if(window.showToast) window.showToast(msg, type); },
        onSearchKeydown: function(e) { if(e.key==="Enter"){ e.preventDefault(); this.doSearch(); } },

        doLogin: function() {
          var self = this;
          if (window.MNL && window.MNL.music && window.MNL.music.login) {
            window.MNL.music.login().then(function(){ self.isLoggedIn=true; self.toast("登录成功","success"); }).catch(function(){ self.toast("登录失败","error"); });
          } else { this.isLoggedIn=true; this.toast("登录成功，欢迎","success"); }
        },
        doLogout: function() { this.isLoggedIn=false; this.toast("已退出登录","info"); },

        doSearch: function() {
          var q = this.searchText.trim();
          if (!q) { this.currentView="recommend"; return; }
          this.currentView="search"; this.tracks=[];
          this.showPopup("🔍", "搜索中...", q);
          var self=this;
          if (window.MNL && window.MNL.music && window.MNL.music.searchJson) {
            window.MNL.music.searchJson(q).then(function(data){ self.tracks=data; setTimeout(function(){self.popup.visible=false;},0); })
              ["catch"](function(){ setTimeout(function(){self.popup.visible=false;},1000); });
          } else { setTimeout(function(){self.popup.visible=false;},1000); }
        },

        openPlaylist: function(pl) {
          this.selectedPlaylist=pl; this.currentView="playlist"; this.tracks=[];
          this.showPopup("📋", "加载歌单中...", pl.name);
          var self=this;
          if (window.MNL && window.MNL.music && window.MNL.music.getPlaylistDetailJson) {
            window.MNL.music.getPlaylistDetailJson(pl.id).then(function(data){ self.tracks=data; setTimeout(function(){self.popup.visible=false;},0); })
              ["catch"](function(){ setTimeout(function(){self.popup.visible=false;},1000); });
          } else { setTimeout(function(){self.popup.visible=false;},1000); }
        },

        addToQueue: function(t) {
          for(var i=0;i<this.queue.length;i++){ if(this.queue[i].id===t.id){ this.showPopup("📋","已在队列中"); return; } }
          this.queue.push({id:t.id,name:t.name||t.title,artist:t.artist,duration:t.duration,url:t.url,coverUrl:t.coverUrl});
          this.showPopup("📋","已添加");
        },
        removeFromQueue: function(i) { this.queue.splice(i,1); if(this.currentIndex===i){this.currentIndex=-1;this.playing=false;}else if(this.currentIndex>i)this.currentIndex--; },
        clearQueue: function() { this.queue=[]; this.currentIndex=-1; this.playing=false; },

        playTrack: function(t) { if(!t)return; this.addToQueue(t); this.currentIndex=this.queue.length-1; this._playCurrent(); },
        playIndex: function(i) { if(i<0||i>=this.queue.length)return; this.currentIndex=i; this._playCurrent(); },
        /** 保留向后兼容：纯模拟架构下立即 resolve */
        _ensureVizOutput: function() {
          return Promise.resolve();
        },

        _playCurrent: function() {
          var self = this;
          this.playing = true; this.currentTime = 0; this.duration = 0;
          var t = this.currentTrack;
          if (!t) { this.playing = false; return; }
          // 如果有缓存的 url，直接设置并播放
          if (t.url) {
            this.$nextTick(function() {
              var a = self.$refs.mpAudio;
              if (a) {
                if (a.src !== t.url) { a.src = t.url; a.load(); }
                a.play()["catch"](function(e) {
                  console.warn('[music] play failed:', e);
                  self.playing = false;
                  self.toast('播放失败：' + (e.message || '请检查网络'), 'error');
                });
              }
            });
            return;
          }
          // 否则请求 url
          var id = t.id;
          if (window.MNL && window.MNL.music && window.MNL.music.getSongUrl) {
            window.MNL.music.getSongUrl(id).then(function(url) {
              if (url) {
                t.url = url;
                self.$nextTick(function() {
                  var a = self.$refs.mpAudio;
                  if (a) {
                    a.src = url;
                    a.load();
                    a.play()["catch"](function(e) {
                      console.warn('[music] play failed:', e);
                      self.playing = false;
                    });
                  }
                });
              } else {
                self.toast("无法获取播放链接", "error");
                self.playing = false;
              }
            })["catch"](function(e) {
              console.warn('[music] getSongUrl failed:', e);
              self.toast("获取播放链接失败", "error");
              self.playing = false;
            });
          } else {
            self.toast("无可用播放源", "error");
            self.playing = false;
          }
        },
        prev: function() { if(!this.queue.length)return; this.currentIndex=(this.currentIndex-1+this.queue.length)%this.queue.length; this.playIndex(this.currentIndex); },
        next: function() { if(!this.queue.length)return; this.currentIndex=(this.currentIndex+1)%this.queue.length; this.playIndex(this.currentIndex); },
        togglePlay: function() {
          // 同步执行，不等待 _ensureVizOutput（纯模拟架构下无副作用）
          var a = this.$refs.mpAudio;
          if (!a) { this.playing = !this.playing; return; }
          if (this.playing) {
            a.pause();
            this.playing = false;
          } else {
            var t = this.currentTrack;
            // 确保 src 已设置
            if ((!a.src || a.src === window.location.href) && t && t.url) {
              a.src = t.url;
              a.load();  // 显式加载，避免 src 改变后未加载就 play
            }
            if (a.src && a.src !== window.location.href) {
              this.playing = true;
              var self = this;
              a.play()["catch"](function(e) {
                console.warn('[music] play failed:', e);
                self.playing = false;
                self.toast('播放失败：' + (e.message || '未知错误'), 'error');
              });
            }
          }
        },
        cyclePlayMode: function() { var m=["list","loop","single","random"]; this.playMode=m[(m.indexOf(this.playMode)+1)%m.length]; },
        toggleMute: function() { if(this.muted){this.muted=false;this.volume=this.prevVolume;}else{this.muted=true;this.prevVolume=this.volume;this.volume=0;} var a=this.$refs.mpAudio; if(a)a.volume=this.volume/100; },
        startDrag: function(e) {
          e.preventDefault(); this.isDragging=true; var bar=this.$refs.progressBar; if(!bar)return; var self=this;
          var uf=function(ev){ var r=bar.getBoundingClientRect(); self.currentTime=Math.max(0,Math.min(1,(ev.clientX-r.left)/r.width))*self.duration; };
          var sf=function(){ self.isDragging=false; var a=self.$refs.mpAudio; if(a)a.currentTime=self.currentTime; document.removeEventListener("mousemove",uf); document.removeEventListener("mouseup",sf); };
          document.addEventListener("mousemove",uf); document.addEventListener("mouseup",sf); uf(e);
        },
        seekVolume: function(e) { var b=this.$refs.volumeBar; if(!b)return; var r=b.getBoundingClientRect(); this.volume=Math.round(Math.max(0,Math.min(1,(e.clientX-r.left)/r.width))*100); this.muted=false; var a=this.$refs.mpAudio; if(a)a.volume=this.volume/100; },
        startVolDrag: function(e) {
          e.preventDefault(); var bar=this.$refs.volumeBar; if(!bar)return;
          var self=this;
          var uf=function(ev){ var r=bar.getBoundingClientRect(); self.volume=Math.round(Math.max(0,Math.min(1,(ev.clientX-r.left)/r.width))*100); self.muted=false; var a=self.$refs.mpAudio; if(a)a.volume=self.volume/100; };
          var sf=function(){ document.removeEventListener("mousemove",uf); document.removeEventListener("mouseup",sf); };
          document.addEventListener("mousemove",uf); document.addEventListener("mouseup",sf); uf(e);
        },
        cacheCurrent: function() { if(!this.currentTrack){ this.toast("没有正在播放的歌曲","warning"); return; } this.toast("缓存功能开发中...","info"); },

        // ═══ 可视化播放 ═══

        /** 跳转到全屏 3D 音乐可视化页面
         *  架构说明：独立页面拥有自己的 <audio> 元素 + AudioContext，
         *  通过 sessionStorage 同步当前队列与播放状态。
         *  返回时清理 AudioContext，主页音乐继续正常播放不受影响。
         */
        toggleVisualizer: function() {
          DBG('toggleVisualizer: 跳转到全屏可视化页面');
          try {
            // 保存当前队列与状态到 sessionStorage，供 visualizer.html 读取
            var payload = {
              queue: this.queue,
              currentIndex: this.currentIndex,
              playMode: this.playMode,
              volume: this.muted ? 0 : (this.volume / 100)
            };
            sessionStorage.setItem('mnl.viz.state', JSON.stringify(payload));
            // 跳转到独立可视化页面
            window.location.href = 'visualizer.html';
          } catch (e) {
            DBG('toggleVisualizer 错误: ' + e.message);
            this.toast('无法打开可视化页面', 'error');
          }
        },

        /** 绘制可视化（已废弃 - 现在通过独立 visualizer.html 页面渲染） */
        _drawViz: function() {
          // no-op
        },
      },

      mounted: function() {
        var self = this;
        try {
          DBG("mounted() start");
          var audio = self.$refs.mpAudio;
          if (audio) {
            audio.volume = self.volume / 100;
            audio.addEventListener("ended", function() { self.next(); });
            audio.addEventListener("error", function() { self.toast("音频加载失败","error"); });
            audio.addEventListener("timeupdate", function() { if(!self.isDragging&&audio)self.currentTime=audio.currentTime; });
            audio.addEventListener("loadedmetadata", function() { if(audio)self.duration=audio.duration; });
          }
          // ── 加载推荐歌单（JSON API，避免 COM 对象）──
          if (window.MNL && window.MNL.music && window.MNL.music.searchPlaylistsJson) {
            self.showPopup("⏳", "加载中...", "正在连接网易云音乐");
            window.MNL.music.searchPlaylistsJson("推荐")
              .then(function(data) {
                if (data && data.length) self.playlists = data;
              })["catch"](function(err) {
                DBG("playlist err: " + (err && err.message ? err.message : String(err)));
              }).then(function() {
                self.popup.visible = false;
              });
          }
          // ── 从可视化返回时恢复播放状态 ──
          try {
            var resumeRaw = sessionStorage.getItem('mnl.viz.resume');
            if (resumeRaw) {
              sessionStorage.removeItem('mnl.viz.resume');
              var rs = JSON.parse(resumeRaw);
              if (rs.queue && rs.queue.length) {
                self.queue = rs.queue;
                self.currentIndex = rs.currentIndex || 0;
                self.playMode = rs.playMode || 'list';
                self.volume = Math.round((rs.volume || 0.7) * 100);
                if (audio) audio.volume = self.volume / 100;
                // 直接操控 audio 元素恢复播放，不经过 _playCurrent（避免 currentTime 被重置为 0）
                self.$nextTick(function () {
                  var a = self.$refs.mpAudio;
                  var track = self.queue[self.currentIndex];
                  if (!a || !track || !track.url) return;
                  var resumeTime = rs.currentTime || 0;
                  var resumePlaying = !!rs.isPlaying;
                  var onReady = function () {
                    a.removeEventListener('loadedmetadata', onReady);
                    if (resumeTime > 0.5) { a.currentTime = resumeTime; }
                    if (resumePlaying) {
                      a.play().catch(function () {});
                      self.playing = true;
                    }
                  };
                  a.addEventListener('loadedmetadata', onReady);
                  a.src = track.url;
                  a.load();
                });
              }
            }
          } catch (e) { DBG('resume err: ' + e.message); }
          DBG("mounted() done");
        } catch (e) {
          DBG("mounted() ERROR: " + (e.message||String(e)));
        }
      },
    };

    window.MusicPage = MusicPage;
    DBG("v37 OK. MusicPage type=" + typeof MusicPage + " tpl_len=" + (MusicPage.template||"").length);

  } catch (e) {
    DBG("v37 FATAL: " + (e.message||String(e)));
    window.MusicPage = { template: "<div class=\'page-content music-page\'><div style=\'padding:40px;color:red;\'>ERR: "+(e.message||"")+"</div></div>", data:function(){return{};} };
  }
})();
