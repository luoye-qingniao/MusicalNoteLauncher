// build-music.js - generates Vue music.js with proper UTF-8
const fs = require('fs');

const destBin = 'f:/MCMNL/MNL/WPF\u9879\u76ee/MusicalNoteLauncher-main/bin/Debug/net8.0-windows/WebApp/pages/music.js';
const destSrc = 'f:/MCMNL/MNL/WPF\u9879\u76ee/MusicalNoteLauncher-main/WebApp/pages/music.js';

// Vue template - use the UnoApp layout with Vue directives
const tpl = `
          <div class="mp-container">

            <audio class="mp-audio" ref="mpAudio" preload="auto"></audio>

            <div class="mp-search">
              <div class="mp-search-box">
                <span class="mp-search-icon">\ud83d\udd0d</span>
                <input type="text" class="mp-search-input" v-model="searchText" placeholder="\u641c\u7d22\u6b4c\u66f2\u3001\u6b4c\u624b..." @keydown="onSearchKeydown" />
                <button class="mp-search-btn" @click="doSearch">\u641c\u7d22</button>
              </div>
              <div class="mp-login-area">
                <button class="mp-login-btn" v-if="!isLoggedIn" @click="doLogin">
                  <span>\ud83c\udfb5</span><span> \u767b\u5f55\u7f51\u6613\u4e91</span>
                </button>
                <div class="mp-logged-in" v-else @click="doLogout">
                  <div class="mp-avatar">{{ nickname.charAt(0) }}</div>
                  <span class="mp-nickname">{{ nickname }}</span>
                  <span class="mp-arrow"> \u25bc</span>
                </div>
              </div>
            </div>

            <div class="mp-content">
              <div class="mp-left">
                <div class="mp-panel-header">
                  <span class="mp-panel-title" v-if="currentView === 'recommend'">\ud83d\udcbf \u63a8\u8350\u6b4c\u5355</span>
                  <span class="mp-panel-title" v-else-if="currentView === 'search'">\ud83d\udd0d \u641c\u7d22\u7ed3\u679c</span>
                  <span class="mp-panel-title" v-else>\ud83d\udccb {{ selectedPlaylist ? selectedPlaylist.name : '\u6b4c\u5355\u8be6\u60c5' }}</span>
                  <span class="mp-panel-count" v-if="currentView === 'recommend'">{{ playlists.length }} \u4e2a\u6b4c\u5355</span>
                  <span class="mp-panel-count" v-else>{{ tracks.length }} \u9996</span>
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
                        <span class="mp-playlist-icon">\ud83d\udccb</span>
                        <span class="mp-track-count">{{ pl.trackCount || 0 }}\u9996</span>
                      </div>
                    </div>
                    <div class="mp-empty-state" v-if="playlists.length === 0">
                      <span class="mp-empty-icon">\ud83c\udfb5</span>
                      <span>\u6682\u65e0\u63a8\u8350\u6b4c\u5355</span>
                    </div>
                  </template>

                  <template v-if="currentView !== 'recommend'">
                    <div class="mp-track-item mp-song-item" v-for="t in tracks" :key="t.id"
                      @click="playTrack(t)" @dblclick="addToQueue(t)">
                      <div class="mp-track-cover"><img v-if="t.coverUrl" :src="t.coverUrl" alt="" /><span class="mp-cover-icon" v-else>\ud83c\udfb5</span></div>
                      <div class="mp-track-info">
                        <div class="mp-track-name">{{ t.name || t.title }}</div>
                        <div class="mp-track-sub">{{ t.artist }}</div>
                      </div>
                      <div class="mp-track-right">
                        <span class="mp-track-duration">{{ t.duration }}</span>
                        <span class="mp-track-cached" v-if="t._cached">\ud83d\udce5</span>
                      </div>
                    </div>
                    <div class="mp-empty-state" v-if="tracks.length === 0">
                      <span class="mp-empty-icon">{{ currentView === 'search' ? '\ud83d\udced' : '\ud83d\udccb' }}</span>
                      <span>{{ currentView === 'search' ? '\u672a\u627e\u5230\u76f8\u5173\u6b4c\u66f2' : '\u8be5\u6b4c\u5355\u6682\u65e0\u6b4c\u66f2' }}</span>
                      <span class="mp-empty-hint" v-if="currentView === 'search'">\u8bd5\u8bd5\u5176\u4ed6\u5173\u952e\u8bcd</span>
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
                  <span class="mp-panel-title">\ud83d\udccb \u64ad\u653e\u961f\u5217</span>
                  <span class="mp-panel-count">{{ queue.length }} \u9996</span>
                  <button class="mp-clear-btn" v-if="queue.length > 0" @click="clearQueue">\u6e05\u7a7a</button>
                </div>
                <div class="mp-scroll-list">
                  <div class="mp-empty-state" v-if="queue.length === 0">
                    <span class="mp-empty-icon">\ud83d\udccb</span>
                    <span>\u64ad\u653e\u961f\u5217\u4e3a\u7a7a</span>
                    <span class="mp-empty-hint">\u53cc\u51fb\u6b4c\u66f2\u6dfb\u52a0\u5230\u961f\u5217</span>
                  </div>
                  <div class="mp-queue-item" v-for="(t, i) in queue" :key="'q'+i"
                    :class="{ 'mp-queue-active': currentIndex === i }"
                    @click="playIndex(i)">
                    <span class="mp-queue-icon">{{ currentIndex === i ? '\ud83d\udd0a' : '\ud83c\udfb5' }}</span>
                    <div class="mp-queue-info">
                      <div class="mp-queue-name">{{ t.name || t.title }}</div>
                      <div class="mp-queue-sub">{{ t.artist }}</div>
                    </div>
                    <span class="mp-queue-duration">{{ t.duration }}</span>
                    <button class="mp-queue-remove" @click.stop="removeFromQueue(i)">\u2715</button>
                  </div>
                </div>
              </div>
            </div>

            <div class="mp-player">
              <div class="mp-player-info">
                <div class="mp-player-cover"><img v-if="currentTrack && currentTrack.coverUrl" :src="currentTrack.coverUrl" alt="" /><span class="mp-player-nocover" v-else>\ud83c\udfb5</span></div>
                <div class="mp-player-meta">
                  <div class="mp-player-title">{{ currentTrack ? (currentTrack.name || currentTrack.title) : '\u672a\u5728\u64ad\u653e' }}</div>
                  <div class="mp-player-artist">{{ currentTrack ? currentTrack.artist : '' }}</div>
                </div>
              </div>

              <div class="mp-player-controls">
                <div class="mp-controls-btns">
                  <button class="mp-ctrl-btn" @click="prev" title="\u4e0a\u4e00\u9996">\u23ee</button>
                  <button class="mp-ctrl-btn mp-ctrl-play" @click="togglePlay" :title="playing ? '\u6682\u505c' : '\u64ad\u653e'">
                    {{ playing ? '\u23f8' : '\u25b6' }}
                  </button>
                  <button class="mp-ctrl-btn" @click="next" title="\u4e0b\u4e00\u9996">\u23ed</button>
                  <button class="mp-ctrl-btn mp-ctrl-mode" @click="cyclePlayMode" :title="modeTitle">{{ modeIcon }}</button>
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
                <button class="mp-ctrl-btn" @click="cacheCurrent" title="\u7f13\u5b58\u5f53\u524d\u6b4c\u66f2">\ud83d\udce5</button>
                <span class="mp-volume-icon" @click="toggleMute" :title="muted ? '\u53d6\u6d88\u9759\u97f3' : '\u9759\u97f3'">
                  {{ volumeIcon }}
                </span>
                <div class="mp-volume-bar" ref="volumeBar" @click="seekVolume">
                  <div class="mp-volume-track">
                    <div class="mp-volume-fill" :style="{ width: muted ? '0%' : volume + '%' }"></div>
                    <div class="mp-volume-thumb" :style="{ left: muted ? '0%' : volume + '%' }"></div>
                  </div>
                </div>
              </div>
            </div>

          </div>`;

// Output JS file
const output = `// ====== MusicPage v24 - JSON bridge API ======
(function () {
  var DBG = window._dbg || function (msg) { console.log("[MUSIC-DBG]", msg); };
  try {
    DBG("music.js v24 loading");

    const MusicPage = {
      template: \`
        <div class="page-content music-page">
${tpl}
        </div>
      \`,

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
          nickname: "\u97f3\u4e50\u7231\u597d\u8005",
          isDragging: false,
          popup: { visible: false, icon: "", text: "", sub: "" },
        };
      },

      computed: {
        currentTrack() { return this.queue[this.currentIndex] || null; },
        progressPct() { return this.duration > 0 ? (this.currentTime / this.duration) * 100 : 0; },
        curTimeStr() { return this.fmtTime(this.currentTime); },
        durTimeStr() { return this.fmtTime(this.duration); },
        modeIcon() { return { list: "\u27a1", loop: "\ud83d\udd01", single: "\ud83d\udd02", random: "\ud83d\udd00" }[this.playMode] || "\u27a1"; },
        modeTitle() { return { list: "\u5217\u8868\u64ad\u653e", loop: "\u5217\u8868\u5faa\u73af", single: "\u5355\u66f2\u5faa\u73af", random: "\u968f\u673a\u64ad\u653e" }[this.playMode] || "\u5217\u8868\u64ad\u653e"; },
        volumeIcon() { if (this.muted||this.volume===0) return "\ud83d\udd07"; if (this.volume<30) return "\ud83d\udd08"; if (this.volume<70) return "\ud83d\udd09"; return "\ud83d\udd0a"; },
      },

      methods: {
        fmtTime: function(s) { if(isNaN(s)||s<0) return "0:00"; var m=Math.floor(s/60),sec=Math.floor(s%60); return m+":"+String(sec).padStart(2,"0"); },
        showPopup: function(icon, text, sub) { this.popup = { visible: true, icon: icon, text: text, sub: sub||"" }; var self=this; clearTimeout(this._pt); this._pt=setTimeout(function(){ self.popup.visible=false; },2000); },
        toast: function(msg, type) { if(window.showToast) window.showToast(msg, type); },
        onSearchKeydown: function(e) { if(e.key==="Enter"){ e.preventDefault(); this.doSearch(); } },

        doLogin: function() {
          var self = this;
          if (window.MNL && window.MNL.music && window.MNL.music.login) {
            window.MNL.music.login().then(function(){ self.isLoggedIn=true; self.toast("\u767b\u5f55\u6210\u529f","success"); }).catch(function(){ self.toast("\u767b\u5f55\u5931\u8d25","error"); });
          } else { this.isLoggedIn=true; this.toast("\u767b\u5f55\u6210\u529f\uff0c\u6b22\u8fce","success"); }
        },
        doLogout: function() { this.isLoggedIn=false; this.toast("\u5df2\u9000\u51fa\u767b\u5f55","info"); },

        doSearch: function() {
          var q = this.searchText.trim();
          if (!q) { this.currentView="recommend"; return; }
          this.currentView="search"; this.tracks=[];
          this.showPopup("\ud83d\udd0d", "\u641c\u7d22\u4e2d...", q);
          var self=this;
          if (window.MNL && window.MNL.music && window.MNL.music.searchJson) {
            window.MNL.music.searchJson(q).then(function(data){ self.tracks=data; setTimeout(function(){self.popup.visible=false;},0); })
              ["catch"](function(){ setTimeout(function(){self.popup.visible=false;},1000); });
          } else { setTimeout(function(){self.popup.visible=false;},1000); }
        },

        openPlaylist: function(pl) {
          this.selectedPlaylist=pl; this.currentView="playlist"; this.tracks=[];
          this.showPopup("\ud83d\udccb", "\u52a0\u8f7d\u6b4c\u5355\u4e2d...", pl.name);
          var self=this;
          if (window.MNL && window.MNL.music && window.MNL.music.getPlaylistDetailJson) {
            window.MNL.music.getPlaylistDetailJson(pl.id).then(function(data){ self.tracks=data; setTimeout(function(){self.popup.visible=false;},0); })
              ["catch"](function(){ setTimeout(function(){self.popup.visible=false;},1000); });
          } else { setTimeout(function(){self.popup.visible=false;},1000); }
        },

        addToQueue: function(t) {
          for(var i=0;i<this.queue.length;i++){ if(this.queue[i].id===t.id){ this.showPopup("\ud83d\udccb","\u5df2\u5728\u961f\u5217\u4e2d"); return; } }
          this.queue.push({id:t.id,name:t.name||t.title,artist:t.artist,duration:t.duration,url:t.url,coverUrl:t.coverUrl});
          this.showPopup("\ud83d\udccb","\u5df2\u6dfb\u52a0");
        },
        removeFromQueue: function(i) { this.queue.splice(i,1); if(this.currentIndex===i){this.currentIndex=-1;this.playing=false;}else if(this.currentIndex>i)this.currentIndex--; },
        clearQueue: function() { this.queue=[]; this.currentIndex=-1; this.playing=false; },

        playTrack: function(t) { if(!t)return; this.addToQueue(t); this.currentIndex=this.queue.length-1; this._playCurrent(); },
        playIndex: function(i) { if(i<0||i>=this.queue.length)return; this.currentIndex=i; this._playCurrent(); },
        _playCurrent: function() {
          this.playing=true; this.currentTime=0; this.duration=0; var t=this.currentTrack, self=this;
          if(!t){this.playing=false;return;}
          if(t.url){this.$nextTick(function(){var a=self.$refs.mpAudio;if(a){a.src=t.url;a.play()["catch"](function(){self.playing=false;});}});return;}
          var id=t.id;
          if(window.MNL&&window.MNL.music&&window.MNL.music.getSongUrl){
            window.MNL.music.getSongUrl(id).then(function(url){
              if(url){t.url=url;self.$nextTick(function(){var a=self.$refs.mpAudio;if(a){a.src=url;a.play()["catch"](function(){self.playing=false;});}});}
              else{self.toast("\u65e0\u6cd5\u83b7\u53d6\u64ad\u653e\u94fe\u63a5","error");self.playing=false;}
            })["catch"](function(){self.toast("\u83b7\u53d6\u64ad\u653e\u94fe\u63a5\u5931\u8d25","error");self.playing=false;});
          }else{self.toast("\u65e0\u53ef\u7528\u64ad\u653e\u6e90","error");self.playing=false;}
        },
        prev: function() { if(!this.queue.length)return; this.currentIndex=(this.currentIndex-1+this.queue.length)%this.queue.length; this.playIndex(this.currentIndex); },
        next: function() { if(!this.queue.length)return; this.currentIndex=(this.currentIndex+1)%this.queue.length; this.playIndex(this.currentIndex); },
        togglePlay: function() {
          var a=this.$refs.mpAudio;
          if(!a){this.playing=!this.playing;return;}
          if(this.playing){a.pause();this.playing=false;}
          else{
            var t=this.currentTrack;
            if(!a.src&&t&&t.url){a.src=t.url;}
            if(a.src){var self=this;a.play().then(function(){})["catch"](function(){self.playing=false;});this.playing=true;}
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
        cacheCurrent: function() { if(!this.currentTrack){ this.toast("\u6ca1\u6709\u6b63\u5728\u64ad\u653e\u7684\u6b4c\u66f2","warning"); return; } this.toast("\u7f13\u5b58\u529f\u80fd\u5f00\u53d1\u4e2d...","info"); },
      },

      mounted: function() {
        var self = this;
        try {
          DBG("mounted() start");
          var audio = self.$refs.mpAudio;
          if (audio) {
            audio.volume = self.volume / 100;
            audio.addEventListener("ended", function() { self.next(); });
            audio.addEventListener("error", function() { self.toast("\u97f3\u9891\u52a0\u8f7d\u5931\u8d25","error"); });
            audio.addEventListener("timeupdate", function() { if(!self.isDragging&&audio)self.currentTime=audio.currentTime; });
            audio.addEventListener("loadedmetadata", function() { if(audio)self.duration=audio.duration; });
          }
          // ── 加载推荐歌单（JSON API，避免 COM 对象）──
          if (window.MNL && window.MNL.music && window.MNL.music.searchPlaylistsJson) {
            self.showPopup("\u23f3", "\u52a0\u8f7d\u4e2d...", "\u6b63\u5728\u8fde\u63a5\u7f51\u6613\u4e91\u97f3\u4e50");
            window.MNL.music.searchPlaylistsJson("\u63a8\u8350")
              .then(function(data) {
                if (data && data.length) self.playlists = data;
              })["catch"](function(err) {
                DBG("playlist err: " + (err && err.message ? err.message : String(err)));
              }).then(function() {
                self.popup.visible = false;
              });
          }
          DBG("mounted() done");
        } catch (e) {
          DBG("mounted() ERROR: " + (e.message||String(e)));
        }
      },
    };

    window.MusicPage = MusicPage;
    DBG("v24 OK. MusicPage type=" + typeof MusicPage + " tpl_len=" + (MusicPage.template||"").length);

  } catch (e) {
    DBG("v24 FATAL: " + (e.message||String(e)));
    window.MusicPage = { template: "<div class=\\'page-content music-page\\'><div style=\\'padding:40px;color:red;\\'>ERR: "+(e.message||"")+"</div></div>", data:function(){return{};} };
  }
})();
`;

const utf8Bytes = Buffer.from(output, 'utf8');
fs.writeFileSync(destBin, utf8Bytes);
fs.writeFileSync(destSrc, utf8Bytes);
console.log('Written ' + utf8Bytes.length + ' bytes to:');
console.log('  ' + destBin);
console.log('  ' + destSrc);
