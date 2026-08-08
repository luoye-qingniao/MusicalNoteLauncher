/* ============================================================
   wallpaper-engine.js - MNL 全景动画背景引擎
   ============================================================ */

/**
 * MNL 壁纸引擎：管理全景、图片、视频三种背景模式。
 * 当前仅实现「全景模式」(PanoramaRenderer)。
 * 图片/视频复用已有的 window.applyWallpaper / window.applyVideoBackground。
 */

// ===== 全景渲染器 (Three.js 天空盒) =====

class PanoramaRenderer {
  constructor(canvasId) {
    this.canvas = document.getElementById(canvasId);
    if (!this.canvas) { console.error('[Panorama] 找不到 canvas:', canvasId); return; }

    this.threeRenderer = null;
    this.scene = null;
    this.camera = null;
    this.cube = null;
    this.loaded = false;
    this.autoRotation = 0;
    this.ROTATION_SPEED = 0.005;
    this.mouseFollowEnabled = false;
    this.currentTheme = 'overworld';
    this._animId = null;
    this._running = false;
    this._loadSeq = 0;
    this._mouseX = 0;
    this._mouseY = 0;
    this._lastRenderTime = 0;
    this._contextLost = false;
    this._TARGET_FPS = 30;
    this._FRAME_INTERVAL = 1000 / this._TARGET_FPS;
    this._onMouseMove = this._onMouseMove.bind(this);
    this._animate = this._animate.bind(this);
    this._onResize = this._onResize.bind(this);
    this._onVisibility = this._onVisibility.bind(this);
    this._onContextLost = this._onContextLost.bind(this);
    this._onContextRestored = this._onContextRestored.bind(this);
    this._lastVisibilityState = document.visibilityState;

    // 设备性能检测：低端设备降 pixelRatio
    var mem = navigator.deviceMemory || 4;
    var cores = navigator.hardwareConcurrency || 4;
    this._isLowEnd = mem < 4 || cores < 4;
    this._pixelRatio = this._isLowEnd ? 1 : Math.min(window.devicePixelRatio, 1.5);
  }

  start() {
    if (this._running) return;
    this._updateCanvasSize();
    this._running = true;
    this._initThree();
    window.addEventListener('mousemove', this._onMouseMove);
    window.addEventListener('resize', this._onResize);
    document.addEventListener('visibilitychange', this._onVisibility);
    this.lastTime = performance.now();
    this._lastRenderTime = 0;
    this._lastVisibilityState = document.visibilityState;
    this._animate(this.lastTime);
  }

  stop() {
    if (!this._running) return;
    this._running = false;
    if (this._animId) { cancelAnimationFrame(this._animId); this._animId = null; }
    window.removeEventListener('mousemove', this._onMouseMove);
    window.removeEventListener('resize', this._onResize);
    document.removeEventListener('visibilitychange', this._onVisibility);
    if (this.threeRenderer) {
      this.threeRenderer.dispose();
      this.threeRenderer = null;
    }
    this.loaded = false;
    this._contextLost = false;
  }

  /** 显式设置 canvas 内部缓冲区尺寸（对应 CSS 布局尺寸） */
  _updateCanvasSize() {
    if (!this.canvas) return;
    // 先确保 CSS display 可见，否则 clientWidth 可能是 0
    var w = this.canvas.clientWidth || window.innerWidth;
    var h = this.canvas.clientHeight || window.innerHeight;
    if (w > 0 && h > 0) {
      this.canvas.width = w;
      this.canvas.height = h;
    }
  }

  setTheme(theme) {
    const valid = theme || 'overworld';
    if (this.currentTheme === valid) return;
    this.currentTheme = valid;
    this.loaded = false;
    this._loadTextures();
  }

  setRotationSpeed(speed) { this.ROTATION_SPEED = speed; }

  setMouseFollow(enabled) { this.mouseFollowEnabled = enabled; }

  _onMouseMove(e) {
    this._mouseX = (e.clientX / window.innerWidth - 0.5) * 2;
    this._mouseY = (e.clientY / window.innerHeight - 0.5) * 2;
  }

  _initThree() {
    if (!this.canvas || typeof THREE === 'undefined') return;

    try {
      var w = this.canvas.width || window.innerWidth;
      var h = this.canvas.height || window.innerHeight;
      if (w === 0) w = window.innerWidth;
      if (h === 0) h = window.innerHeight;

      // WebGL 可用性检测
      var testCanvas = document.createElement('canvas');
      var gl = testCanvas.getContext('webgl2') || testCanvas.getContext('webgl');
      if (!gl) {
        console.warn('[Panorama] WebGL 不可用，回退到静态背景');
        this._running = false;
        return;
      }

      this.scene = new THREE.Scene();
      this.camera = new THREE.PerspectiveCamera(75, w / h, 0.1, 1000);
      this.camera.position.set(0, 0, 0);

      this.threeRenderer = new THREE.WebGLRenderer({
        canvas: this.canvas,
        alpha: false,
        antialias: !this._isLowEnd,
        powerPreference: 'high-performance',
        failIfMajorPerformanceCaveat: false
      });
      this.threeRenderer.setSize(w, h);
      this.threeRenderer.setPixelRatio(this._pixelRatio);
      this.threeRenderer.setClearColor(0x0a0a0a);

      // WebGL context 丢失/恢复处理
      var self = this;
      this.canvas.addEventListener('webglcontextlost', this._onContextLost, false);
      this.canvas.addEventListener('webglcontextrestored', this._onContextRestored, false);

      var faceOrder = [1, 3, 4, 5, 0, 2];
      var materials = faceOrder.map(function() {
        return new THREE.MeshBasicMaterial({ side: THREE.BackSide, color: 0x0a0a0a });
      });

      var geometry = new THREE.BoxGeometry(10, 10, 10);
      this.cube = new THREE.Mesh(geometry, materials);
      this.scene.add(this.cube);

      this._loadTextures();
    } catch (e) {
      console.error('[Panorama] Three.js init error:', e);
      this._running = false;
    }
  }

  /** 页面不可见时暂停渲染，可见时恢复 */
  _onVisibility() {
    if (document.visibilityState === 'hidden') {
      this._lastVisibilityState = 'hidden';
    } else if (document.visibilityState === 'visible' && this._lastVisibilityState === 'hidden') {
      this._lastVisibilityState = 'visible';
      this._lastRenderTime = 0;
    } else {
      this._lastVisibilityState = document.visibilityState;
    }
  }

  /** WebGL context 丢失 */
  _onContextLost(e) {
    console.warn('[Panorama] WebGL context 丢失');
    this._contextLost = true;
    if (e) e.preventDefault();
  }

  /** WebGL context 恢复 */
  _onContextRestored() {
    console.log('[Panorama] WebGL context 恢复，重新加载纹理');
    this._contextLost = false;
    this._lastRenderTime = 0;
    if (this.threeRenderer) {
      this.threeRenderer.setPixelRatio(this._pixelRatio);
    }
    this._loadTextures();
  }

  _onResize() {
    if (!this.threeRenderer || !this.canvas) return;
    var w = window.innerWidth;
    var h = window.innerHeight;
    if (w === 0 || h === 0) return;
    this.canvas.width = w;
    this.canvas.height = h;
    this.threeRenderer.setSize(w, h);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  _loadTextures() {
    if (!this.cube) return;
    var seq = ++this._loadSeq;
    var loader = new THREE.TextureLoader();
    var safeTheme = ['overworld', 'nether', 'end', 'wild', 'darkforest', 'desert', 'mountains', 'cherry', 'deep_dark', 'panorama'].indexOf(this.currentTheme) >= 0 ? this.currentTheme : 'overworld';
    var basePath = 'img/panorama/' + safeTheme + '/';
    var faceOrder = [1, 3, 4, 5, 0, 2];
    var loadedCount = 0;
    var self = this;

    this.cube.material.forEach(function(mat, i) {
      loader.load(
        basePath + 'panorama_' + faceOrder[i] + '.jpg',
        function(texture) {
          if (self._loadSeq !== seq) return;
          texture.colorSpace = THREE.SRGBColorSpace;
          texture.minFilter = THREE.LinearFilter;
          texture.magFilter = THREE.LinearFilter;
          mat.map = texture;
          mat.color = new THREE.Color(0xffffff);
          mat.needsUpdate = true;
          loadedCount++;
          if (loadedCount >= 6) self.loaded = true;
        },
        undefined,
        function() {
          if (self._loadSeq !== seq) return;
          loadedCount++;
          if (loadedCount >= 6) self.loaded = true;
        }
      );
    });
  }

  _animate(timestamp) {
    if (!this._running) return;

    // 页面隐藏或 context 丢失时只调度不渲染
    if (this._contextLost || document.visibilityState === 'hidden') {
      this._animId = requestAnimationFrame(this._animate);
      return;
    }

    // FPS 限制
    var elapsed = timestamp - this._lastRenderTime;
    if (elapsed < this._FRAME_INTERVAL) {
      this._animId = requestAnimationFrame(this._animate);
      return;
    }
    this._lastRenderTime = timestamp - (elapsed % this._FRAME_INTERVAL);

    var dt = Math.min(timestamp - this.lastTime, 50);
    this.lastTime = timestamp;

    if (this.threeRenderer && this.cube) {
      this.autoRotation += this.ROTATION_SPEED * dt * 0.06;
      this.cube.rotation.y = this.autoRotation;
      if (this.mouseFollowEnabled) {
        this.cube.rotation.y += this._mouseX * 0.15;
        this.cube.rotation.x = this._mouseY * 0.08;
      } else {
        this.cube.rotation.x = 0;
      }
      this.threeRenderer.render(this.scene, this.camera);
    }

    this._animId = requestAnimationFrame(this._animate);
  }
}

// ===== 全局壁纸引擎管理 =====

var mnlWallpaperEngine = null;
var mnlPanoramaRenderer = null;

/** 初始化全景壁纸 */
function initMNLWallpaper() {
  var glCanvas = document.getElementById('wallpaper-canvas-gl');
  if (!glCanvas) { console.warn('[Wallpaper] 找不到 #wallpaper-canvas-gl'); return; }
  mnlPanoramaRenderer = new PanoramaRenderer('wallpaper-canvas-gl');
}

/** 切换壁纸模式（由全局 bgMode watcher 调用） */
function switchMNLWallpaperMode(mode) {
  var glCanvas = document.getElementById('wallpaper-canvas-gl');
  var wallpaperBg = document.getElementById('wallpaper-bg');
  var videoCanvas = document.getElementById('video-bg-canvas');

  if (!glCanvas) return;

  // 非全景模式：隐藏 WebGL canvas
  if (mode !== 'panorama') {
    glCanvas.style.display = 'none';
    if (mnlPanoramaRenderer) {
      mnlPanoramaRenderer.stop();
    }
  }

  // 非视频模式：隐藏视频 canvas + 停止渲染器
  if (mode !== 'video') {
    if (videoCanvas) videoCanvas.style.display = 'none';
    if (window._videoBgRenderer) {
      window._videoBgRenderer.stop();
      window._videoBgRenderer = null;
    }
  }

  // 模式特定处理
  if (mode === 'panorama') {
    glCanvas.style.display = 'block';
    glCanvas.style.position = 'fixed';
    glCanvas.style.top = '0';
    glCanvas.style.left = '0';
    glCanvas.style.width = '100%';
    glCanvas.style.height = '100%';
    glCanvas.style.zIndex = '0';
    glCanvas.style.pointerEvents = 'none';
    // 应用当前 bgOpacity
    var bodyOpacity = document.body.style.getPropertyValue('--bg-opacity');
    if (bodyOpacity) {
      glCanvas.style.opacity = 1 - parseFloat(bodyOpacity);
    } else {
      glCanvas.style.opacity = '0.6'; // 默认 60%
    }
    // 直接写入缓冲区尺寸（防止 clientWidth 在布局刷新前为 0）
    glCanvas.width = window.innerWidth;
    glCanvas.height = window.innerHeight;
    // 隐藏 CSS wallpaper-bg 防止叠加
    if (wallpaperBg) wallpaperBg.style.display = 'none';
    // 激活玻璃效果（让 sidebar/main-content 变半透明）
    document.body.classList.add('bg-active');
    if (!mnlPanoramaRenderer) initMNLWallpaper();
    if (mnlPanoramaRenderer) mnlPanoramaRenderer.start();
  } else if (mode === 'mica') {
    if (wallpaperBg) wallpaperBg.style.display = '';
    document.body.classList.remove('bg-active');
  } else if (mode === 'video') {
    // 隐藏 wallpaper-bg 防止遮挡视频
    if (wallpaperBg) wallpaperBg.style.display = 'none';
  } else if (mode === 'image') {
    // 图片壁纸：确保 wallpaper-bg 可见
    if (wallpaperBg) wallpaperBg.style.display = '';
  }
}

/** 设置全景主题 */
function setMNLPanoramaTheme(theme) {
  if (mnlPanoramaRenderer) mnlPanoramaRenderer.setTheme(theme);
}

/** 设置旋转速度 */
function setMNLPanoramaSpeed(speed) {
  if (mnlPanoramaRenderer) mnlPanoramaRenderer.setRotationSpeed(speed);
}

/** 设置鼠标跟随 */
function setMNLPanoramaMouseFollow(enabled) {
  if (mnlPanoramaRenderer) mnlPanoramaRenderer.setMouseFollow(enabled);
}

/** 窗口尺寸变化时通知全景渲染器 */
function onMNLWallpaperResize() {
  if (mnlPanoramaRenderer) mnlPanoramaRenderer._onResize();
}
