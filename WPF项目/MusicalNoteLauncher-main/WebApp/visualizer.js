// ====== visualizer.js — 音域回响 Sonic Topography ======
// Mineradio sonic-topography-preset.js 完整复刻
// 基于 yin-yizhen/sonic-topography 1.1.1 (commit 3ff303e) 算法

(function (global) {
  'use strict';

  // ── 常量（与 Mineradio 一致）──
  var RIPPLE_MAX = 10;
  var RIPPLE_LIFETIME = 4.8;
  var RIPPLE_SOFT_FADE_START = 2.1;
  var METEOR_MAX = 20;
  var TRAIL_MAX = 200;
  var FLOATING_COUNT = 80;
  var TERRAIN_BASE_SIZE = 168;
  var GRID_SIZE = 96;           // TERRAIN_MIN_GRID_SIZE
  var BOX_RATIO = 0.9 / 1.05;
  var TARGET_FPS = 30;

  // 频段划分（512 FFT bins @ 44100Hz）
  var BAND_SUBBASS    = [0, 4];     // 0-86Hz
  var BAND_BASS       = [4, 14];    // 86-300Hz
  var BAND_LOWMID     = [14, 60];   // 300-1290Hz
  var BAND_MID        = [60, 180];  // 1290-3870Hz
  var BAND_HIGHMID    = [180, 360]; // 3870-7740Hz
  var BAND_PRESENCE   = [360, 420]; // 7740-9030Hz
  var BAND_BRILLIANCE = [420, 480]; // 9030-10320Hz
  var BAND_AIR        = [480, 511]; // 10320-11025Hz

  var state = {
    renderer: null,
    scene: null,
    camera: null,
    root: null,
    terrain: null,
    terrainMat: null,
    starRiver: null,
    starRiverOpacity: 0,
    meteors: null,
    meteorMat: null,
    trails: null,
    trailMat: null,
    analyser: null,
    audioCtx: null,
    source: null,
    audioData: null,
    isPlaying: false,
    sonicTime: 0,
    autoYaw: 0,
    lastKickActive: false,
    lastSnareActive: false,
    smoothAudio: { subBass: 0, bass: 0, lowMid: 0, mid: 0, highMid: 0, presence: 0, brilliance: 0, air: 0 },
    ripples: [],
    rippleIdx: 0,
    meteorsData: [],
    meteorIdx: 0,
    lastMeteorAt: -999,
    trailsData: [],
    trailIdx: 0,
    dummyPos: null,
    dummyQuat: null,
    dummyScale: null,
    dummyMat4: null,
    dummyEuler: null,
    running: false,
    rafId: null,
    container: null,
    lastFrameTime: 0,
    frameAccum: 0,
    // 舞台歌词（Three.js 3D 文字，照搬 Mineradio）
    lyricGroup: null,
    lyricMesh: null,
    lyricOutgoing: null,
    lyricAge: 0,
    lyricTargetOpacity: 0,
    lyricCurrentText: '',
    _lyricPendingText: null,
    // 鼠标拖动旋转（照搬 Mineradio 手势旋转）
    dragActive: false,
    dragLastX: 0,
    dragLastY: 0,
    dragLastT: 0,
    dragYaw: 0,
    dragPhi: 0.40,
    dragRadius: 36,
    targetRadius: 36,      // 滚轮目标（平滑过渡）
    yawVelocity: 0,
    phiVelocity: 0
  };

  function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }
  function clamp01(v) { return clamp(Number.isFinite(v) ? v : 0, 0, 1); }
  function lerp(a, b, t) { return a + (b - a) * t; }
  function smoothstep01(v) { var t = clamp01(v); return t * t * (3 - 2 * t); }
  function blend01(value) { return clamp(Number.isFinite(value) ? value : 0, 0, 1); }

  // ===== 地形顶点着色器（完整版，与 Mineradio 一致）=====
  function buildTerrainVertexShader() {
    return [
      'precision highp float;',
      'uniform float uTime;',
      'uniform float uSubBass;',
      'uniform float uBass;',
      'uniform float uLowMid;',
      'uniform float uMid;',
      'uniform float uHighMid;',
      'uniform float uSmoothness;',
      'uniform float uDensity;',
      'uniform float uEnergy;',
      'uniform float uAmplitude;',
      'uniform vec4 uRipples[' + RIPPLE_MAX + '];',
      'varying vec2 vUv;',
      'varying float vElevation;',
      'varying float vDistance;',
      'varying vec2 vRippleAnim;',
      'varying vec3 vNormal;',
      'varying float vRelativeY;',
      'varying vec2 vInstancePos;',
      // snoise 2D
      'vec3 mod289(vec3 x){return x-floor(x*(1.0/289.0))*289.0;}',
      'vec2 mod289(vec2 x){return x-floor(x*(1.0/289.0))*289.0;}',
      'vec3 permute(vec3 x){return mod289(((x*34.0)+1.0)*x);}',
      'float snoise(vec2 v){',
      '  const vec4 C=vec4(0.211324865405187,0.366025403784439,-0.577350269189626,0.024390243902439);',
      '  vec2 i=floor(v+dot(v,C.yy));',
      '  vec2 x0=v-i+dot(i,C.xx);',
      '  vec2 i1=(x0.x>x0.y)?vec2(1.0,0.0):vec2(0.0,1.0);',
      '  vec4 x12=x0.xyxy+C.xxzz; x12.xy-=i1;',
      '  i=mod289(i);',
      '  vec3 p=permute(permute(i.y+vec3(0.0,i1.y,1.0))+i.x+vec3(0.0,i1.x,1.0));',
      '  vec3 m=max(0.5-vec3(dot(x0,x0),dot(x12.xy,x12.xy),dot(x12.zw,x12.zw)),0.0);',
      '  m=m*m; m=m*m;',
      '  vec3 x=2.0*fract(p*C.www)-1.0;',
      '  vec3 h=abs(x)-0.5;',
      '  vec3 ox=floor(x+0.5);',
      '  vec3 a0=x-ox;',
      '  m*=1.79284291400159-0.85373472095314*(a0*a0+h*h);',
      '  vec3 g; g.x=a0.x*x0.x+h.x*x0.y; g.yz=a0.yz*x12.xz+h.yz*x12.yw;',
      '  return 130.0*dot(m,g);',
      '}',
      'float random(vec2 st){return fract(sin(dot(st.xy,vec2(12.9898,78.233)))*43758.5453123);}',
      'void main(){',
      '  vUv=uv;',
      '  vNormal=normal;',
      '  vec4 instancePos=instanceMatrix*vec4(0.0,0.0,0.0,1.0);',
      '  vec2 pos2D=instancePos.xz;',
      '  vInstancePos=pos2D;',
      '  float centerDist=length(pos2D);',
      '  vDistance=centerDist;',
      '  float rnd=random(pos2D);',
      // 噪声
      '  vec2 movingPos=pos2D*0.05+vec2(uTime*0.1,uTime*0.05);',
      '  float baseNoise=(snoise(movingPos)+1.0)*0.5;',
      '  float wave=sin(pos2D.x*0.15+pos2D.y*0.1-uTime*0.6)*0.5+0.5;',
      '  float globalFalloff=smoothstep(60.0,30.0,centerDist);',
      '  float idleElevation=mix(baseNoise,wave,uSmoothness*0.5+0.2)*0.8*globalFalloff;',
      // 次低频
      '  float subRegion=smoothstep(25.0,0.0,centerDist);',
      '  float subLift=uSubBass*subRegion*5.0;',
      // 低频
      '  float bassNoise=snoise(pos2D*0.1-vec2(0.0,uTime*0.2));',
      '  float bassRegion=smoothstep(35.0,5.0,centerDist+bassNoise*5.0);',
      '  float bassLift=uBass*bassRegion*(smoothstep(0.0,1.0,rnd+uDensity*0.5))*4.0;',
      // 中低频
      '  float lowMidNoise=snoise(pos2D*0.05+vec2(uTime*0.1,0.0));',
      '  float lowMidLift=uLowMid*(lowMidNoise*0.5+0.5)*2.5;',
      // 中频
      '  float riverFlow=sin(pos2D.x*0.2+pos2D.y*0.2+snoise(pos2D*0.1)*2.0-uTime*2.0);',
      '  float midLift=uMid*max(0.0,riverFlow)*3.0;',
      // 中高频尖峰
      '  float highMidRegion=smoothstep(10.0,45.0,centerDist);',
      '  float highMidLift=0.0;',
      '  if(fract(rnd*13.3)>0.8){highMidLift=uHighMid*highMidRegion*fract(rnd*7.7)*2.5;}',
      '  float audioElevation=subLift+bassLift+lowMidLift+midLift+highMidLift;',
      // 能量尖峰
      '  if(rnd>0.99){audioElevation+=uEnergy*5.0;}',
      '  audioElevation*=globalFalloff;',
      '  audioElevation=max(0.0,audioElevation-0.2);',
      '  audioElevation*=uAmplitude;',
      '  float elevation=idleElevation+audioElevation;',
      // 涟漪
      '  float rippleElevation=0.0;',
      '  float rippleIntensityNormal=0.0;',
      '  float rippleIntensityWhite=0.0;',
      '  for(int i=0;i<' + RIPPLE_MAX + ';i++){',
      '    vec4 rd=uRipples[i];',
      '    if(rd.w!=0.0){',
      '      float strength=abs(rd.w);',
      '      bool whiteRipple=rd.w<0.0;',
      '      float dist=length(pos2D-rd.xy);',
      '      float timeSince=uTime-rd.z;',
      '      float curSpeed=whiteRipple?18.0:13.0;',
      '      float curWidth=whiteRipple?1.35:5.5;',
      '      float curFadeDist=whiteRipple?12.0:26.0;',
      '      float elevationScale=whiteRipple?1.15:3.35;',
      '      float waveRadius=timeSince*curSpeed;',
      '      float d=dist-waveRadius;',
      '      float rippleWave=exp(-d*d/curWidth);',
      '      float fade=exp(-waveRadius/curFadeDist);',
      '      float lifeFade=1.0-smoothstep(2.10,4.80,timeSince);',
      '      float rPulse=rippleWave*fade*lifeFade*strength;',
      '      rippleElevation+=rPulse*elevationScale;',
      '      if(whiteRipple){rippleIntensityWhite+=rPulse;}else{rippleIntensityNormal+=rPulse;}',
      '    }',
      '  }',
      '  elevation+=rippleElevation;',
      '  vRippleAnim=vec2(clamp(rippleIntensityNormal,0.0,1.0),clamp(rippleIntensityWhite,0.0,1.0));',
      '  vElevation=elevation;',
      // 拉伸
      '  float yPos=position.y+0.5;',
      '  vRelativeY=yPos;',
      '  float totalHeight=1.0+elevation;',
      '  vec3 pos=position;',
      '  pos.y=-0.5+yPos*totalHeight;',
      '  vec4 worldPosition=modelMatrix*instanceMatrix*vec4(pos,1.0);',
      '  gl_Position=projectionMatrix*viewMatrix*worldPosition;',
      '}'
    ].join('\n');
  }

  // ===== 地形片元着色器（完整版：twinkle / flash / UV-edge）=====
  function buildTerrainFragmentShader() {
    return [
      'precision highp float;',
      'uniform float uTime;',
      'uniform float uPresence;',
      'uniform float uBrilliance;',
      'uniform float uAir;',
      'uniform float uWarmth;',
      'uniform float uBrightness;',
      'uniform float uSharpness;',
      'uniform vec3 uBaseColor1;',
      'uniform vec3 uBaseColor2;',
      'uniform vec3 uFogColor;',
      'uniform vec3 uCoolCore;',
      'uniform vec3 uCoolEdge;',
      'uniform vec3 uWarmCore;',
      'uniform vec3 uWarmEdge;',
      'uniform vec3 uRippleColor;',
      'uniform float uGlowIntensity;',
      'varying vec2 vUv;',
      'varying float vElevation;',
      'varying float vDistance;',
      'varying vec2 vRippleAnim;',
      'varying vec3 vNormal;',
      'varying float vRelativeY;',
      'varying vec2 vInstancePos;',
      'float random(vec2 st){return fract(sin(dot(st.xy,vec2(12.9898,78.233)))*43758.5453123);}',
      'void main(){',
      '  bool isTop=vNormal.y>0.5;',
      '  float distFromTop=1.0-vRelativeY;',
      '  float rnd=random(vInstancePos);',
      '  float centerDist=length(vInstancePos);',
      '  float normElevation=clamp(vElevation/8.0,0.0,1.0);',
      '  vec3 cBase1=uBaseColor1;',
      '  vec3 cBase2=uBaseColor2;',
      // 冷暖混合
      '  float warmBlend=smoothstep(0.0,1.0,uWarmth*1.5+(0.5-centerDist/80.0));',
      '  vec3 zoneCore=mix(uCoolCore,uWarmCore,warmBlend);',
      '  vec3 zoneEdge=mix(uCoolEdge,uWarmEdge,warmBlend);',
      '  vec3 targetGlow=mix(zoneCore,zoneEdge,fract(rnd*11.0));',
      '  float distFade=1.0-smoothstep(40.0,75.0,centerDist);',
      '  vec3 brightCool=mix(uCoolCore,vec3(1.0),0.24);',
      '  targetGlow=mix(targetGlow,brightCool,uBrightness*0.6);',
      '  vec3 currentGlow=mix(cBase2,targetGlow,normElevation)*uGlowIntensity*distFade;',
      '  currentGlow=mix(currentGlow,uRippleColor,clamp(vRippleAnim.x*0.82,0.0,0.72));',
      '  currentGlow=mix(currentGlow,vec3(1.0),vRippleAnim.y);',
      '  vec3 bodyColor=mix(cBase1,cBase2,vRelativeY*distFade);',
      '  vec3 finalColor;',
      '  if(isTop){',
      '    float topIntensity=smoothstep(0.0,0.4,normElevation);',
      '    float twinkleDistFalloff=smoothstep(60.0,30.0,centerDist);',
      '    float twinkleMultiplier=mix(twinkleDistFalloff,1.0,smoothstep(0.01,0.1,normElevation));',
      // air sparkle
      '    if(fract(rnd*31.0)>0.95&&normElevation<0.1){topIntensity+=uAir*2.0*twinkleMultiplier;}',
      '    finalColor=mix(cBase2,currentGlow,topIntensity);',
      // UV边缘发光
      '    float edgeX=smoothstep(0.05,0.01,vUv.x)+smoothstep(0.95,0.99,vUv.x);',
      '    float edgeY=smoothstep(0.05,0.01,vUv.y)+smoothstep(0.95,0.99,vUv.y);',
      '    float edge=min(edgeX+edgeY,1.0);',
      '    finalColor+=currentGlow*edge*0.8*(topIntensity+0.3);',
      // presence flash
      '    float flashChance=smoothstep(0.3,1.0,uPresence);',
      '    if(fract(rnd*53.0)>0.98-flashChance*0.1){',
      '      float flashSync=sin(uTime*40.0+rnd*100.0)*0.5+0.5;',
      '      finalColor+=mix(vec3(1.0),vec3(0.5,1.0,1.0),rnd)*flashSync*uPresence*(1.0+uSharpness*2.0)*twinkleMultiplier;',
      '    }',
      // brilliance 边缘闪烁
      '    if(edge>0.5&&fract(rnd*89.0+uTime*2.0)>0.98){finalColor+=vec3(1.0)*uBrilliance*3.0*twinkleMultiplier;}',
      '  }else{',
      // 侧面
      '    float verticalFalloff=mix(1.0,3.0,uSharpness);',
      '    float sideGlow=smoothstep(0.5/verticalFalloff,0.0,distFromTop)*normElevation;',
      '    if(normElevation<0.02)sideGlow=0.0;',
      '    finalColor=mix(bodyColor,currentGlow,sideGlow*1.5);',
      '    float rimGlow=smoothstep(0.03,0.0,distFromTop)*normElevation;',
      '    finalColor+=currentGlow*rimGlow;',
      '  }',
      // 涟漪叠加
      '  finalColor+=uRippleColor*vRippleAnim.x*0.86;',
      '  finalColor+=vec3(1.0)*vRippleAnim.y*1.2;',
      // 雾
      '  float aerialFog=smoothstep(30.0,65.0,vDistance);',
      '  vec3 atmosphericColor=mix(cBase1,cBase2,0.4);',
      '  finalColor=mix(finalColor,atmosphericColor,aerialFog*0.35);',
      '  float alphaFade=1.0-smoothstep(55.0,78.0,vDistance);',
      '  float alphaBlend=1.0-alphaFade;',
      '  finalColor=mix(finalColor,uFogColor,alphaBlend*0.45);',
      '  gl_FragColor=vec4(finalColor,alphaFade);',
      '}'
    ].join('\n');
  }

  // ── 星空背景 ──
  function buildStarVertexShader() {
    return [
      'precision highp float;',
      'uniform float uTime;',
      'varying float vAlpha;',
      'float random(vec2 st){return fract(sin(dot(st.xy,vec2(12.9898,78.233)))*43758.5453123);}',
      'void main(){',
      '  vec3 pos=position;',
      '  float r=random(pos.xy);',
      '  vAlpha=0.3+r*0.7;',
      '  vec4 worldPosition=modelMatrix*vec4(pos,1.0);',
      '  gl_Position=projectionMatrix*viewMatrix*worldPosition;',
      '  gl_PointSize=clamp(r*3.0,0.5,2.5);',
      '}'
    ].join('\n');
  }

  function buildStarFragmentShader() {
    return [
      'precision mediump float;',
      'varying float vAlpha;',
      'void main(){',
      '  float d=length(gl_PointCoord-0.5)*2.0;',
      '  float alpha=smoothstep(1.0,0.0,d)*vAlpha*0.6;',
      '  gl_FragColor=vec4(1.0,1.0,1.0,alpha);',
      '}'
    ].join('\n');
  }

  // ── 辅助函数 ──
  function makeRippleUniforms() {
    var arr = [];
    for (var i = 0; i < RIPPLE_MAX; i++) arr.push(new THREE.Vector4(0, 0, -100, 0));
    return arr;
  }

  function makeTerrainUniforms() {
    return {
      uTime: { value: 0 },
      uSubBass: { value: 0 },
      uBass: { value: 0 },
      uLowMid: { value: 0 },
      uMid: { value: 0 },
      uHighMid: { value: 0 },
      uPresence: { value: 0 },
      uBrilliance: { value: 0 },
      uAir: { value: 0 },
      uWarmth: { value: 0 },
      uBrightness: { value: 0 },
      uSharpness: { value: 0 },
      uSmoothness: { value: 0.5 },
      uDensity: { value: 0.5 },
      uEnergy: { value: 0 },
      uAmplitude: { value: 1 },
      uRipples: { value: makeRippleUniforms() },
      uBaseColor1: { value: new THREE.Color(0.01, 0.02, 0.04) },
      uBaseColor2: { value: new THREE.Color(0.03, 0.05, 0.09) },
      uFogColor: { value: new THREE.Color(0.01, 0.02, 0.04) },
      uCoolCore: { value: new THREE.Color(0.0, 0.3, 1.0) },
      uCoolEdge: { value: new THREE.Color(0.6, 0.2, 1.0) },
      uWarmCore: { value: new THREE.Color(1.0, 0.2, 0.1) },
      uWarmEdge: { value: new THREE.Color(1.0, 0.6, 0.0) },
      uRippleColor: { value: new THREE.Color(0.2, 0.9, 1.0) },
      uGlowIntensity: { value: 1.2 }
    };
  }

  function initData() {
    var i;
    state.ripples = [];
    for (i = 0; i < RIPPLE_MAX; i++) state.ripples.push({ x: 0, z: 0, start: -100, strength: 0, white: false });
    state.meteorsData = [];
    for (i = 0; i < METEOR_MAX; i++) state.meteorsData.push({ active: false, x: 0, y: -1000, z: 0, speed: 0, strength: 0 });
    state.trailsData = [];
    for (i = 0; i < TRAIL_MAX; i++) {
      state.trailsData.push({ active: false, x: 0, y: -1000, z: 0, vx: 0, vy: 0, vz: 0, life: 0, maxLife: 1, scale: 1 });
    }
  }

  // ── 形状构建 ──
  function buildTerrainMesh() {
    var spacing = TERRAIN_BASE_SIZE / GRID_SIZE;
    var boxWidth = spacing * BOX_RATIO;
    var geo = new THREE.BoxGeometry(boxWidth, 1, boxWidth);
    var mat = new THREE.ShaderMaterial({
      uniforms: makeTerrainUniforms(),
      vertexShader: buildTerrainVertexShader(),
      fragmentShader: buildTerrainFragmentShader(),
      transparent: true,
      depthWrite: true,
      depthTest: true
    });
    var instanceCount = GRID_SIZE * GRID_SIZE;
    var mesh = new THREE.InstancedMesh(geo, mat, instanceCount);
    mesh.frustumCulled = false;
    var offset = (GRID_SIZE * spacing) / 2;
    var n = 0;
    for (var x = 0; x < GRID_SIZE; x++) {
      for (var z = 0; z < GRID_SIZE; z++) {
        state.dummyMat4.makeTranslation(x * spacing - offset, 0.5, z * spacing - offset);
        mesh.setMatrixAt(n++, state.dummyMat4);
      }
    }
    mesh.instanceMatrix.needsUpdate = true;
    return mesh;
  }

  function buildStarRiver() {
    var count = 500;
    var positions = new Float32Array(count * 3);
    var colors = new Float32Array(count * 3);
    var basePositions = new Float32Array(count * 3);
    for (var i = 0; i < count; i++) {
      // 环绕 Y 轴的同心圆环分布（星河围绕地形）
      var angle = Math.random() * Math.PI * 2;
      var ringIdx = Math.floor(Math.random() * 5);
      var radius = 8 + ringIdx * 7 + Math.random() * 6;  // 5 层环 8~40
      var x = Math.cos(angle) * radius;
      var z = Math.sin(angle) * radius;
      var y = (ringIdx - 2.0) * 1.8 + (Math.random() - 0.5) * 2.0;  // 5 层高度
      positions[i * 3] = x;
      positions[i * 3 + 1] = y;
      positions[i * 3 + 2] = z;
      basePositions[i * 3] = x;
      basePositions[i * 3 + 1] = y;
      basePositions[i * 3 + 2] = z;
      // 颜色渐变：内环偏暖金，外环偏青蓝
      var t = ringIdx / 4;
      colors[i * 3] = 0.55 + t * 0.25;            // R
      colors[i * 3 + 1] = 0.72 + t * 0.18;        // G
      colors[i * 3 + 2] = 0.85 + (1 - t) * 0.15;  // B
    }
    var geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));

    // 径向渐变点纹理（与 Mineradio 一致）
    var cv = document.createElement('canvas'); cv.width = cv.height = 64;
    var ctx = cv.getContext('2d');
    var g = ctx.createRadialGradient(32, 32, 0, 32, 32, 31);
    g.addColorStop(0.00, 'rgba(255,255,255,0.96)');
    g.addColorStop(0.42, 'rgba(255,255,255,0.78)');
    g.addColorStop(0.72, 'rgba(255,255,255,0.22)');
    g.addColorStop(1.00, 'rgba(255,255,255,0)');
    ctx.fillStyle = g; ctx.fillRect(0, 0, 64, 64);
    var dotTex = new THREE.CanvasTexture(cv);
    dotTex.minFilter = THREE.LinearFilter; dotTex.magFilter = THREE.LinearFilter;

    var mat = new THREE.PointsMaterial({
      size: 1.2,
      map: dotTex,
      vertexColors: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      depthTest: false,
      transparent: true,
      opacity: 0.55
    });

    var points = new THREE.Points(geo, mat);
    points.renderOrder = 998;
    points.frustumCulled = false;
    // 保存在 threeData 里供 updateStarRiver 使用
    points.userData = { basePositions: basePositions, count: count };
    return points;
  }

  function buildSimpleInstanced(count, size, color, opacity) {
    var geo = new THREE.BoxGeometry(size[0], size[1], size[2]);
    var mat = new THREE.MeshBasicMaterial({
      color: color,
      transparent: true,
      opacity: opacity == null ? 1 : opacity,
      depthWrite: false,
      toneMapped: false
    });
    var mesh = new THREE.InstancedMesh(geo, mat, count);
    mesh.frustumCulled = false;
    for (var i = 0; i < count; i++) {
      state.dummyScale.set(0, 0, 0);
      state.dummyMat4.compose(state.dummyPos.set(0, -1000, 0), state.dummyQuat, state.dummyScale);
      mesh.setMatrixAt(i, state.dummyMat4);
    }
    mesh.instanceMatrix.needsUpdate = true;
    return mesh;
  }

  function buildStars() {
    var count = 400;
    var geo = new THREE.BufferGeometry();
    var positions = new Float32Array(count * 3);
    for (var i = 0; i < count; i++) {
      positions[i*3] = (Math.random() - 0.5) * 300;
      positions[i*3+1] = Math.random() * 120 + 20;
      positions[i*3+2] = (Math.random() - 0.5) * 300;
    }
    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    var mat = new THREE.ShaderMaterial({
      uniforms: { uTime: { value: 0 } },
      vertexShader: buildStarVertexShader(),
      fragmentShader: buildStarFragmentShader(),
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending
    });
    return new THREE.Points(geo, mat);
  }

  // ===== 舞台歌词 Mesh（照搬 Mineradio：Canvas纹理 → ShaderMaterial → 3D Group）=====
  function buildLyricShaderMaterial(tex, colorHex) {
    return new THREE.ShaderMaterial({
      uniforms: {
        uMap: { value: tex },
        uTime: { value: 0 },
        uOpacity: { value: 0.35 },
        uColor: { value: new THREE.Color(colorHex) },
        uBloom: { value: 0 }
      },
      vertexShader: [
        'varying vec2 vUv;',
        'void main(){',
        '  vUv = uv;',
        '  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);',
        '}'
      ].join('\n'),
      fragmentShader: [
        'precision highp float;',
        'uniform sampler2D uMap;',
        'uniform vec3 uColor;',
        'uniform float uOpacity, uTime, uBloom;',
        'varying vec2 vUv;',
        'void main(){',
        '  float mask = texture2D(uMap, vUv).a;',
        // 边缘柔化：离文字中心越远越透明
        '  float edgeDist = length(vUv - 0.5) * 2.0;',
        '  float edgeSoft = 1.0 - smoothstep(0.55, 0.95, edgeDist);',
        '  float finalMask = mask * edgeSoft;',
        '  if(finalMask < 0.02) discard;',
        // 扫光效果（斜向移动亮线）
        '  float sweepPhase = fract(uTime * 0.18);',
        '  float sweep = 1.0 - smoothstep(0.0, 0.08, abs((vUv.x + vUv.y * 0.3) - (sweepPhase * 1.4 - 0.16)));',
        // 颜色：基础色 + 扫光亮白 + bloom辉光
        '  vec3 color = uColor;',
        '  color = mix(color, vec3(1.0), sweep * 0.55);',
        '  color += vec3(0.55, 0.82, 1.0) * sweep * 0.22;',
        '  color += vec3(0.55, 0.82, 1.0) * uBloom * 0.20;',
        // 亮度补偿（防止过暗）
        '  float lum = dot(color, vec3(0.299, 0.587, 0.114));',
        '  color += vec3(max(0.0, 0.25 - lum));',
        '  gl_FragColor = vec4(color, finalMask * uOpacity);',
        '}'
      ].join('\n'),
      transparent: true,
      depthWrite: false,
      depthTest: false,
      side: THREE.DoubleSide
    });
  }

  function buildLyricMesh(text) {
    var canvas = document.createElement('canvas');
    var ctx = canvas.getContext('2d');
    var fontSize = 100;
    ctx.font = 'bold ' + fontSize + 'px -apple-system, BlinkMacSystemFont, "Microsoft YaHei", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';

    var metrics = ctx.measureText(text || '');
    var textWidth = metrics.width;
    var padding = fontSize * 0.5;
    canvas.width = Math.max(512, nextPowerOfTwo(textWidth + padding * 2));
    canvas.height = Math.max(128, nextPowerOfTwo(fontSize * 2.0));

    ctx.font = 'bold ' + fontSize + 'px -apple-system, BlinkMacSystemFont, "Microsoft YaHei", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillStyle = '#ffffff';
    ctx.fillText(text || '', canvas.width / 2, canvas.height / 2);

    var tex = new THREE.CanvasTexture(canvas);
    tex.minFilter = THREE.LinearFilter;
    tex.magFilter = THREE.LinearFilter;
    tex.generateMipmaps = false;

    var textWorldW = 12;
    var textWorldH = textWorldW * (canvas.height / canvas.width);

    // 3层深度挤出 → 立体感（前亮后暗，模拟挤压厚度）
    var layers = [
      { z: 0,     color: '#f4f9ff' },
      { z: 0.055, color: '#c8ddf0' },
      { z: 0.11,  color: '#7aadcc' }
    ];

    var group = new THREE.Group();
    var allUniforms = [];
    var allMaterials = [];

    for (var d = 0; d < layers.length; d++) {
      var mat = buildLyricShaderMaterial(tex, layers[d].color);
      var geo = new THREE.PlaneGeometry(textWorldW, textWorldH);
      var mesh = new THREE.Mesh(geo, mat);
      mesh.position.z = layers[d].z;
      mesh.renderOrder = 998 - d;
      group.add(mesh);
      allUniforms.push(mat.uniforms);
      allMaterials.push(mat);
    }

    group.userData = {
      text: text,
      canvas: canvas,
      tex: tex,
      textWorldW: textWorldW,
      textWorldH: textWorldH,
      state: 'in',
      age: 0,
      allUniforms: allUniforms,
      allMaterials: allMaterials
    };
    return group;
  }

  function nextPowerOfTwo(n) {
    var p = 1;
    while (p < n) p *= 2;
    return p;
  }

  // ── 涟漪 ──
  function syncRippleUniforms(time) {
    if (!state.terrainMat) return;
    var arr = state.terrainMat.uniforms.uRipples.value;
    for (var i = 0; i < RIPPLE_MAX; i++) {
      var r = state.ripples[i];
      var age = time - r.start;
      var active = r.strength > 0.001 && age >= 0 && age < RIPPLE_LIFETIME;
      if (!active) {
        arr[i].set(0, 0, -100, 0);
        if (r.strength > 0) r.strength = 0;
        continue;
      }
      var fade = 1 - smoothstep01((age - RIPPLE_SOFT_FADE_START) / (RIPPLE_LIFETIME - RIPPLE_SOFT_FADE_START));
      var strength = r.strength * fade;
      arr[i].set(r.x, r.z, r.start, r.white ? -strength : strength);
    }
  }

  function addRipple(x, z, strength, white) {
    var idx = state.rippleIdx;
    var r = state.ripples[idx];
    r.x = x;
    r.z = z;
    r.start = state.sonicTime;
    r.strength = clamp(strength, 0.1, 3.0);
    r.white = !!white;
    state.rippleIdx = (idx + 1) % RIPPLE_MAX;
  }

  // ── 流星 ──
  function addMeteor(strength) {
    var now = state.sonicTime;
    if (now - state.lastMeteorAt < 0.55) return;
    state.lastMeteorAt = now;
    var idx = state.meteorIdx;
    var angle = Math.random() * Math.PI * 2;
    var dist = Math.random() * 25;
    var m = state.meteorsData[idx];
    m.active = true;
    m.x = Math.cos(angle) * dist;
    m.z = Math.sin(angle) * dist;
    m.y = 30 + Math.random() * 10;
    m.speed = 1.0 + Math.random() * 0.5 + strength * 1.5;
    m.strength = strength;
    state.meteorIdx = (idx + 1) % METEOR_MAX;
  }

  function spawnTrail(x, y, z, speedMul) {
    var idx = state.trailIdx;
    var p = state.trailsData[idx];
    p.active = true;
    p.x = x + (Math.random() - 0.5) * 1.5;
    p.y = y + (Math.random() - 0.5) * 1.5;
    p.z = z + (Math.random() - 0.5) * 1.5;
    p.vx = (Math.random() - 0.5) * 2.0;
    p.vy = Math.random() * 2.0 + speedMul * 10.0;
    p.vz = (Math.random() - 0.5) * 2.0;
    p.life = 0;
    p.maxLife = 0.5 + Math.random() * 0.5;
    p.scale = Math.random() * 0.6 + 0.2;
    state.trailIdx = (idx + 1) % TRAIL_MAX;
  }

  function updateMeteorsAndTrails(dt) {
    if (!state.meteors || !state.trails) return;
    var i;
    for (i = 0; i < METEOR_MAX; i++) {
      var m = state.meteorsData[i];
      if (!m.active) {
        state.dummyPos.set(0, -1000, 0);
        state.dummyScale.set(0, 0, 0);
      } else {
        m.y -= m.speed * 60 * dt;
        if (m.y <= 0) {
          m.active = false;
          addRipple(m.x, m.z, Math.min(m.strength, 1.2), true);
          for (var t = 0; t < 10; t++) spawnTrail(m.x, 0.5, m.z, m.speed * 1.5);
          state.dummyPos.set(0, -1000, 0);
          state.dummyScale.set(0, 0, 0);
        } else {
          if (Math.random() > 0.3) spawnTrail(m.x, m.y, m.z, m.speed * 0.2);
          state.dummyPos.set(m.x, Math.max(0, m.y), m.z);
          state.dummyScale.set(1.5, 1.5, 1.5);
        }
      }
      state.dummyQuat.identity();
      state.dummyMat4.compose(state.dummyPos, state.dummyQuat, state.dummyScale);
      state.meteors.setMatrixAt(i, state.dummyMat4);
    }
    state.meteors.instanceMatrix.needsUpdate = true;
    for (i = 0; i < TRAIL_MAX; i++) {
      var p = state.trailsData[i];
      if (!p.active) {
        state.dummyPos.set(0, -1000, 0);
        state.dummyScale.set(0, 0, 0);
      } else {
        p.life += dt;
        if (p.life >= p.maxLife) {
          p.active = false;
          state.dummyScale.set(0, 0, 0);
        } else {
          p.x += p.vx * dt * 10;
          p.y += p.vy * dt * 10;
          p.z += p.vz * dt * 10;
          var s = p.scale * (1.0 - p.life / p.maxLife);
          state.dummyPos.set(p.x, p.y, p.z);
          state.dummyScale.set(s, s, s);
        }
      }
      state.dummyQuat.identity();
      state.dummyMat4.compose(state.dummyPos, state.dummyQuat, state.dummyScale);
      state.trails.setMatrixAt(i, state.dummyMat4);
    }
    state.trails.instanceMatrix.needsUpdate = true;
  }

  // ── 音频频段分析 ──
  function analyzeBand(data, range) {
    var sum = 0, count = range[1] - range[0] + 1;
    for (var i = range[0]; i <= range[1] && i < data.length; i++) sum += data[i];
    return sum / count / 255;
  }

  function analyzeAudio() {
    if (!state.analyser) return fallbackAudio();
    state.analyser.getByteFrequencyData(state.audioData);

    var subBass   = analyzeBand(state.audioData, BAND_SUBBASS);
    var bass      = analyzeBand(state.audioData, BAND_BASS);
    var lowMid    = analyzeBand(state.audioData, BAND_LOWMID);
    var mid       = analyzeBand(state.audioData, BAND_MID);
    var highMid   = analyzeBand(state.audioData, BAND_HIGHMID);
    var presence  = analyzeBand(state.audioData, BAND_PRESENCE);
    var brilliance = analyzeBand(state.audioData, BAND_BRILLIANCE);
    var air       = analyzeBand(state.audioData, BAND_AIR);

    // 鼓点检测（与 Mineradio 一致）
    var kickEnvelope = clamp01(Math.max(subBass * 2.5, bass * 1.5, lowMid * 0.6));
    var energy = clamp01(bass * 0.35 + mid * 0.35 + presence * 0.2 + brilliance * 0.1);
    var treble = clamp01(presence * 0.42 + brilliance * 0.38 + air * 0.2);

    return {
      subBass: clamp01(subBass * 1.8),
      bass: clamp01(bass * 1.5),
      lowMid: lowMid,
      mid: mid,
      highMid: highMid,
      presence: presence,
      brilliance: brilliance,
      air: air,
      treble: treble,
      kickEnvelope: kickEnvelope,
      energy: energy,
      sharpness: clamp01(treble * 0.70 + kickEnvelope * 0.20),
      smoothness: clamp01(1.0 - treble * 0.42 + mid * 0.14),
      density: clamp01(0.45 + treble * 0.35 + kickEnvelope * 0.10)
    };
  }

  function fallbackAudio() {
    var t = Date.now() / 1000 * (state.isPlaying ? 2.2 : 0.5);
    var beat = Math.pow((Math.sin(t) + 1) / 2, 3);
    var bass = beat * 0.8;
    var mid = (Math.sin(t * 0.5) + 1) / 2 * 0.5;
    var treble = (Math.sin(t * 1.5 + 1) + 1) / 2 * 0.3;
    return {
      subBass: bass * 0.7, bass: bass,
      lowMid: mid * 0.7, mid: mid,
      highMid: treble * 0.8, presence: treble * 0.6,
      brilliance: treble, air: treble * 0.3, treble: treble,
      kickEnvelope: beat, energy: clamp01(beat * 1.8 + treble * 0.3),
      sharpness: clamp01(treble * 0.70 + beat * 0.20),
      smoothness: clamp01(1.0 - treble * 0.42 + mid * 0.14),
      density: clamp01(0.45 + treble * 0.35 + beat * 0.10)
    };
  }

  // ── 平滑（与 Mineradio 一致：responseRate 由 motionSpeed 驱动）──
  function smoothAudio(target, dt) {
    var motionSpeed = 50; // DEFAULT_GROUND_MOTION_SPEED
    var responseRate = lerp(2.2, 60, motionSpeed / 100); // ≈ 31.1
    var responseBlend = blend01(1 - Math.exp(-responseRate * Math.max(0.001, dt || 1/60)));
    var s = state.smoothAudio;
    s.subBass += (target.subBass - s.subBass) * responseBlend;
    s.bass += (target.bass - s.bass) * responseBlend;
    s.lowMid += (target.lowMid - s.lowMid) * responseBlend;
    s.mid += (target.mid - s.mid) * responseBlend;
    s.highMid += (target.highMid - s.highMid) * responseBlend;
    s.presence += (target.presence - s.presence) * responseBlend;
    s.brilliance += (target.brilliance - s.brilliance) * responseBlend;
    s.air += (target.air - s.air) * responseBlend;
    return s;
  }

  // ── Uniform 同步 ──
  function syncTerrainUniforms(audio, dt, time) {
    if (!state.terrainMat) return;
    var smoothed = smoothAudio(audio, dt);
    var u = state.terrainMat.uniforms;

    // kick → 低频 注入（与 Mineradio deriveKickFollowLowBands 一致）
    var safeKick = clamp(audio.kickEnvelope, 0, 0.75);
    var normalizedKick = safeKick / 0.75;
    var subBassFinal = clamp(audio.subBass * 0.22 + normalizedKick * 1.28, 0, 1.2);
    var bassFinal = clamp(audio.bass * 0.20 + normalizedKick * 1.15, 0, 1.15);

    // Amplitude 计算
    var amplitude = 50; // DEFAULT_GROUND_AMPLITUDE
    var ampMul = amplitude <= 50 ? amplitude / 50 : 1 + Math.pow((amplitude - 50) / 50, 2) * 14;

    u.uTime.value = time;
    u.uSubBass.value = subBassFinal;
    u.uBass.value = bassFinal;
    u.uLowMid.value = smoothed.lowMid;
    u.uMid.value = smoothed.mid;
    u.uHighMid.value = smoothed.highMid;
    u.uPresence.value = smoothed.presence;
    u.uBrilliance.value = smoothed.brilliance;
    u.uAir.value = smoothed.air;
    u.uEnergy.value = audio.energy;
    u.uAmplitude.value = ampMul;
    u.uSharpness.value = audio.sharpness;
    u.uSmoothness.value = audio.smoothness;
    u.uDensity.value = audio.density;

    // 冷暖
    var low = subBassFinal + bassFinal + smoothed.lowMid + smoothed.mid;
    var high = smoothed.presence + smoothed.brilliance + smoothed.air;
    var sum = Math.max(0.001, low + high);
    u.uWarmth.value = clamp(low / sum, 0, 1);
    u.uBrightness.value = clamp(high / sum, 0, 1);

    syncRippleUniforms(time);

    if (state.floatingMat) {
      var fu = state.floatingMat.uniforms;
      fu.uTime.value = time;
      fu.uSubBass.value = subBassFinal;
      fu.uBass.value = bassFinal;
      fu.uLowMid.value = smoothed.lowMid;
      fu.uMid.value = smoothed.mid;
      fu.uHighMid.value = smoothed.highMid;
      fu.uPresence.value = smoothed.presence;
      fu.uBrilliance.value = smoothed.brilliance;
      fu.uAir.value = smoothed.air;
      fu.uEnergy.value = audio.energy;
      fu.uAmplitude.value = ampMul;
      fu.uSharpness.value = audio.sharpness;
      fu.uSmoothness.value = audio.smoothness;
      fu.uDensity.value = audio.density;
      fu.uWarmth.value = u.uWarmth.value;
      fu.uBrightness.value = u.uBrightness.value;
    }
  }

  // ── 实时主题色同步（封面提取 → 着色器 uniform）──
  function colorUniformLerp(uniform, target, alpha) {
    if (!uniform || !uniform.value || !uniform.value.isColor) return;
    uniform.value.copy(target);
  }

  function setThemeColors(pr, pg, pb, sr, sg, sb, hr, hg, hb) {
    // 缓存待应用的颜色（解决 init 异步竞态）
    state._pendingColors = [pr, pg, pb, sr, sg, sb, hr, hg, hb];
    _applyThemeColors();
  }

  function _applyThemeColors() {
    var pc = state._pendingColors;
    if (!pc) return;
    var pr = pc[0], pg = pc[1], pb = pc[2], sr = pc[3], sg = pc[4], sb = pc[5], hr = pc[6], hg = pc[7], hb = pc[8];
    if (!state.terrainMat) return; // 材质还没建好，先在 setThemeColors 已缓存，init 完会再触发

    var primary = new THREE.Color(pr, pg, pb);
    var secondary = new THREE.Color(sr, sg, sb);
    var highlight = new THREE.Color(hr, hg, hb);
    var baseColor = new THREE.Color(0x05070c);

    var base1 = primary.clone().lerp(baseColor, 0.84);
    var base2 = base1.clone().lerp(highlight, 0.14);
    var coolCore = primary.clone().lerp(new THREE.Color(0xffffff), 0.08);
    var warmCore = secondary.clone().lerp(new THREE.Color(0xffb15a), 0.18);
    var ripple = highlight.clone().lerp(new THREE.Color(0xffffff), 0.10);
    var coolEdge = coolCore.clone().lerp(base1, 0.34);
    var warmEdge = warmCore.clone().lerp(base1, 0.26);

    var mat = state.terrainMat;
    if (mat) {
      var u = mat.uniforms;
      colorUniformLerp(u.uBaseColor1, base1, 1);
      colorUniformLerp(u.uBaseColor2, base2, 1);
      colorUniformLerp(u.uFogColor, base1, 1);
      colorUniformLerp(u.uCoolCore, coolCore, 1);
      colorUniformLerp(u.uCoolEdge, coolEdge, 1);
      colorUniformLerp(u.uWarmCore, warmCore, 1);
      colorUniformLerp(u.uWarmEdge, warmEdge, 1);
      colorUniformLerp(u.uRippleColor, ripple, 1);
    }
    if (state.meteorMat) state.meteorMat.color.copy(warmCore).lerp(new THREE.Color(0xffffff), 0.7);
    if (state.trailMat) state.trailMat.color.copy(ripple);
  }

  // ── Audio Triggers（与 Mineradio 完全一致）──
  function updateAudioTriggers(audio) {
    var kickActive = audio.kickEnvelope > 0.58;
    if (kickActive && !state.lastKickActive) {
      var angle = Math.random() * Math.PI * 2;
      var dist = Math.random() * 20;
      addRipple(Math.cos(angle) * dist, Math.sin(angle) * dist, Math.min(audio.kickEnvelope * 2.0, 3.0), false);
    }
    state.lastKickActive = audio.kickEnvelope > 0.32;

    var snareActive = audio.presence > 0.52 || audio.brilliance > 0.56;
    if (snareActive && !state.lastSnareActive && Math.random() < 0.55) {
      var angle2 = Math.random() * Math.PI * 2;
      var dist2 = 10 + Math.random() * 35;
      addRipple(Math.cos(angle2) * dist2, Math.sin(angle2) * dist2, Math.min((audio.presence + audio.brilliance) * 1.2, 3.0), true);
    }
    state.lastSnareActive = audio.presence > 0.38 || audio.brilliance > 0.42;

    if (audio.kickEnvelope > 0.62 && Math.random() < 0.045) {
      addMeteor(clamp(audio.kickEnvelope, 0.28, 0.9));
    }
  }

  // ── 星河粒子（CPU 端流动动画，PointsMaterial 100% 兼容 WebView2）──
  function updateStarRiver(audio, dt) {
    var points = state.starRiver;
    if (!points || !points.geometry) return;
    var pos = points.geometry.attributes.position;
    var base = points.userData.basePositions;
    if (!pos || !base) return;

    // 播放状态控制透明度和流速
    var targetOpacity = state.isPlaying ? 0.55 : 0.10;
    state.starRiverOpacity += (targetOpacity - state.starRiverOpacity) * Math.min(1, dt * 2.5);
    points.material.opacity = state.starRiverOpacity;
    points.visible = state.starRiverOpacity > 0.005;

    if (!state.isPlaying) return;

    var speed = 0.4 + clamp01(audio.smoothBass || 0) * 0.8;
    var arr = pos.array;
    for (var i = 0; i < points.userData.count; i++) {
      var bx = base[i * 3];
      var bz = base[i * 3 + 2];
      var radius = Math.sqrt(bx * bx + bz * bz);
      var angle = Math.atan2(bz, bx);

      // 环绕 Y 轴旋转（5 层环速度不同）
      var ringSpeed = speed * (0.7 + (i % 5) * 0.15);
      angle += ringSpeed * dt;
      arr[i * 3] = Math.cos(angle) * radius;
      arr[i * 3 + 2] = Math.sin(angle) * radius;

      // 垂直正弦浮动
      arr[i * 3 + 1] = base[i * 3 + 1] + Math.sin(state.sonicTime * 1.3 + i * 0.3) * 0.35
        + Math.sin(state.sonicTime * 2.1 + i * 0.7) * 0.2;

      // 半径呼吸（低频脉动）
      var rBreathe = radius * (1.0 + clamp01(audio.kickEnvelope || 0) * 0.06);
      arr[i * 3] = Math.cos(angle) * rBreathe;
      arr[i * 3 + 2] = Math.sin(angle) * rBreathe;
    }
    pos.needsUpdate = true;

    // 低频驱动尺寸脉动
    points.material.size = 1.2 + clamp01(audio.smoothBass || 0) * 0.6;
  }

  // ── 相机（照搬 Mineradio：鼠标拖动旋转 + 惯性滑动）──
  function updateCamera(dt) {
    // 惯性衰减（damping 0.93，比 Mineradio 0.90 更持久以匹配大场景）
    var DAMPING = Math.pow(0.93, dt * 60);
    state.yawVelocity *= DAMPING;
    state.phiVelocity *= DAMPING;

    // 停止阈值
    if (Math.abs(state.yawVelocity) < 0.00008) state.yawVelocity = 0;
    if (Math.abs(state.phiVelocity) < 0.00008) state.phiVelocity = 0;

    // 应用惯性
    if (!state.dragActive) {
      state.dragYaw += state.yawVelocity * dt;
      state.dragPhi = clamp(state.dragPhi + state.phiVelocity * dt, 0.08, 0.85);
    }

    // 使用用户控制的 yaw/phi/radius（radius 平滑过渡有惯性手感）
    var yaw = state.dragYaw;
    var phi = state.dragPhi;
    state.dragRadius = lerp(state.dragRadius, state.targetRadius, blend01(1 - Math.exp(-5.0 * dt)));
    var radius = state.dragRadius;

    var bob = Math.sin(state.sonicTime * 0.3) * 0.5;
    var camY = radius * Math.sin(phi) + 3;
    var camRx = radius * Math.cos(phi);
    var camX = camRx * Math.sin(yaw);
    var camZ = camRx * Math.cos(yaw);

    state.camera.position.set(camX, camY + bob, camZ);
    state.camera.lookAt(0, 0, 0);
  }

  // ── 主循环 ──
  function animate(timestamp) {
    if (!state.running) return;
    state.rafId = requestAnimationFrame(animate);

    // 隐藏或 context 丢失时跳过渲染
    if (_visHidden || _ctxLost) return;

    var now = timestamp / 1000;
    var dt = Math.min(0.05, now - state.lastFrameTime);
    state.lastFrameTime = now;

    // 限帧
    state.frameAccum += dt;
    var frameInterval = 1 / TARGET_FPS;
    if (state.frameAccum < frameInterval) return;
    state.frameAccum -= frameInterval;

    // sonicTime = Mineradio 的 motionSpeed 驱动 (0.45 + speed*0.017 ≈ 1.3)
    state.sonicTime += frameInterval * 1.3;

    var audio = analyzeAudio();

    updateCamera(frameInterval);
    syncTerrainUniforms(audio, frameInterval, state.sonicTime);
    updateAudioTriggers(audio);
    updateStarRiver(audio, frameInterval);
    updateMeteorsAndTrails(frameInterval);
    updateLyric3D(frameInterval, state.sonicTime);

    state.renderer.render(state.scene, state.camera);
  }

  // ── 初始化 ──
  function init(containerId, audioElement) {
    var container = document.getElementById(containerId);
    if (!container) return false;
    state.container = container;

    // Dummy helpers
    state.dummyPos = new THREE.Vector3();
    state.dummyQuat = new THREE.Quaternion();
    state.dummyScale = new THREE.Vector3();
    state.dummyMat4 = new THREE.Matrix4();
    state.dummyEuler = new THREE.Euler();

    var dpr = Math.min(window.devicePixelRatio || 1, 1);
    state.renderer = new THREE.WebGLRenderer({
      antialias: true,
      alpha: true,
      powerPreference: 'high-performance'
    });
    state.renderer.setPixelRatio(dpr);
    state.renderer.setSize(container.clientWidth, container.clientHeight);
    state.renderer.setClearColor(0x000000, 0);
    // Canvas CSS 样式（确保点击区域正确）
    state.renderer.domElement.style.background = 'transparent';
    state.renderer.domElement.style.display = 'block';
    state.renderer.domElement.style.width = '100%';
    state.renderer.domElement.style.height = '100%';
    container.appendChild(state.renderer.domElement);

    state.scene = new THREE.Scene();
    state.scene.background = null;

    // 星空背景
    var stars = buildStars();
    state.scene.add(stars);

    // 相机：50° FOV
    state.camera = new THREE.PerspectiveCamera(50, container.clientWidth / container.clientHeight, 0.1, 500);

    // Root group
    state.root = new THREE.Group();
    state.root.position.set(0, -4, 0);
    state.root.scale.setScalar(0.24);
    state.scene.add(state.root);

    initData();

    // 地形
    state.terrain = buildTerrainMesh();
    state.terrainMat = state.terrain.material;
    state.root.add(state.terrain);

    // 星河粒子（挂在 scene 上避免 root scale 0.24 影响点大小和位置）
    state.starRiver = buildStarRiver();
    state.starRiver.position.set(0, 3, 0);
    state.scene.add(state.starRiver);

    // 流星
    state.meteors = buildSimpleInstanced(METEOR_MAX, [0.4, 1.2, 0.4], 0xffffff, 1);
    state.meteorMat = state.meteors.material;
    state.root.add(state.meteors);

    // 轨迹
    state.trails = buildSimpleInstanced(TRAIL_MAX, [0.8, 0.8, 0.8], 0xa8ecff, 0.6);
    state.trailMat = state.trails.material;
    state.root.add(state.trails);

    // 舞台歌词 Group（挂在 root 下，随地形一起被拖动旋转）
    state.lyricGroup = new THREE.Group();
    state.lyricGroup.position.set(0, 25, 0);   // root 坐标 → 世界 y ≈ 2（高于地形）
    state.lyricGroup.scale.setScalar(3.2);      // 抵消 root scale 0.24 → 世界宽约 12*3.2*0.24≈9.2
    state.lyricGroup.renderOrder = 998;
    state.root.add(state.lyricGroup);

    // 如果有缓存的待显示歌词（页面先于 init 触发）
    if (state._lyricPendingText) {
      var pending = state._lyricPendingText;
      state._lyricPendingText = null;
      showStageLyric(pending);
    }

    // 音频分析
    if (audioElement) {
      try {
        state.audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        state.source = state.audioCtx.createMediaElementSource(audioElement);
        state.analyser = state.audioCtx.createAnalyser();
        state.analyser.fftSize = 1024;
        state.analyser.smoothingTimeConstant = 0.8;
        state.source.connect(state.analyser);
        state.source.connect(state.audioCtx.destination);
        state.audioData = new Uint8Array(state.analyser.frequencyBinCount);
        if (state.audioCtx.state === 'suspended') state.audioCtx.resume();
      } catch (e) {
        console.warn('[Visualizer] 音频分析初始化失败，使用模拟模式:', e);
        state.analyser = null;
      }
    }

    window.addEventListener('resize', onResize);
    document.addEventListener('visibilitychange', onVisibility);

    // WebGL context 丢失/恢复
    state.renderer.domElement.addEventListener('webglcontextlost', onContextLost, false);
    state.renderer.domElement.addEventListener('webglcontextrestored', onContextRestored, false);

    // 鼠标交互（点击生成涟漪）
    bindPointerEvents(state.renderer.domElement);

    // 材质全部创建完毕，应用之前缓存的主题色
    _applyThemeColors();

    state.running = true;
    state.lastFrameTime = performance.now() / 1000;
    state.rafId = requestAnimationFrame(animate);
    return true;
  }

  function onResize() {
    if (!state.container || !state.renderer || !state.camera) return;
    var w = state.container.clientWidth;
    var h = state.container.clientHeight;
    state.renderer.setSize(w, h);
    state.camera.aspect = w / h;
    state.camera.updateProjectionMatrix();
  }

  /** 页面不可见时跳过渲染，但保持 rAF 循环以便恢复 */
  var _visHidden = false;
  function onVisibility() {
    _visHidden = document.visibilityState === 'hidden';
  }

  /** WebGL context 丢失 */
  var _ctxLost = false;
  function onContextLost(e) {
    console.warn('[Visualizer] WebGL context 丢失');
    _ctxLost = true;
    if (e) e.preventDefault();
  }

  /** WebGL context 恢复 */
  function onContextRestored() {
    console.log('[Visualizer] WebGL context 恢复');
    _ctxLost = false;
    state.frameAccum = 0;
    state.lastFrameTime = performance.now() / 1000;
  }

  function setPlaying(playing) {
    state.isPlaying = playing;
  }

  function setAudioSource(audioElement) {
    if (state.audioCtx && state.audioCtx.state === 'suspended') {
      state.audioCtx.resume();
    }
  }

  // ===== 舞台歌词：显示/切换（照搬 Mineradio 动画机制）=====
  function showStageLyric(text) {
    if (!text) text = '';
    if (text === state.lyricCurrentText && state.lyricMesh) return;
    state.lyricCurrentText = text;

    // 如果还没有 lyric group（异步等待 init），先缓存
    if (!state.lyricGroup) {
      state._lyricPendingText = text;
      return;
    }

    // 旧的 mesh → outgoing 退场
    if (state.lyricMesh) {
      state.lyricMesh.userData.state = 'out';
      state.lyricMesh.userData.age = 0;
      state.lyricOutgoing = state.lyricMesh;
    }

    // 创建新 mesh（内部已是 Group，含3层深度挤出）
    var mesh = buildLyricMesh(text);
    state.lyricGroup.add(mesh);
    state.lyricMesh = mesh;
    state.lyricAge = 0;
    state.lyricTargetOpacity = 1;
  }

  function updateLyric3D(dt, time) {
    if (!state.lyricMesh) return;
    state.lyricAge += dt;

    var group = state.lyricMesh;
    var uniformsList = group.userData.allUniforms;
    if (!uniformsList || !uniformsList.length) return;

    // 更新所有层的 shader time
    for (var ui = 0; ui < uniformsList.length; ui++) {
      uniformsList[ui].uTime.value = time;
    }

    // 辅助：设置所有层的 uOpacity
    function setAllOpacity(val) {
      for (var i = 0; i < uniformsList.length; i++) {
        uniformsList[i].uOpacity.value = val;
      }
    }

    // ── 入场动画（lyr-in，900ms）──
    if (group.userData.state === 'in') {
      var inAge = group.userData.age;
      group.userData.age += dt;
      var inT = clamp01(inAge / 0.9);
      var inEased = inT < 0.55
        ? (inT / 0.55) * (inT / 0.55) * 0.5
        : 1 - Math.pow(1 - inT, 3) * 0.5;

      // Z 轴飞入 + 缩放
      group.position.z = (1 - inEased) * -8;
      group.scale.setScalar(0.7 + inEased * 0.3);
      group.position.y = (1 - inEased) * 1.5;

      var bob = Math.sin(time * 0.92 + 0.3) * 0.18 + Math.sin(time * 0.41 + 1.7) * 0.10;
      group.position.y += bob;

      // 所有层统一淡入
      var targetOp = state.lyricTargetOpacity;
      for (var i = 0; i < uniformsList.length; i++) {
        uniformsList[i].uOpacity.value = lerp(
          uniformsList[i].uOpacity.value, targetOp,
          blend01(1 - Math.exp(-4.5 * dt))
        );
      }

      if (inT >= 1) {
        group.userData.state = 'bob';
        group.position.z = 0;
        group.position.y = 0;
        group.scale.setScalar(1);
      }
    }

    // ── 浮动呼吸动画（lyr-bob，无限循环）──
    if (group.userData.state === 'bob') {
      for (var i = 0; i < uniformsList.length; i++) {
        uniformsList[i].uOpacity.value = lerp(
          uniformsList[i].uOpacity.value, state.lyricTargetOpacity,
          blend01(1 - Math.exp(-4.5 * dt))
        );
      }

      var bobX = Math.sin(time * 0.92 + 0.3) * 0.22 + Math.sin(time * 0.41 + 1.7) * 0.12;
      var bobY = Math.cos(time * 0.67 + 0.8) * 0.30 + Math.sin(time * 0.38 + 2.1) * 0.14;
      var bobZ = Math.sin(time * 0.55 + 1.4) * 0.16;

      group.position.set(bobX, bobY, bobZ);
      group.rotation.set(
        Math.sin(time * 0.34 + 0.6) * 0.04,
        Math.cos(time * 0.28 + 1.1) * 0.03,
        Math.sin(time * 0.42 + 0.2) * 0.035
      );
      group.scale.setScalar(1 + Math.sin(time * 0.51 + 0.9) * 0.015);
    }

    // ── 退场动画（lyr-out，700ms）──
    if (state.lyricOutgoing) {
      var outGroup = state.lyricOutgoing;
      outGroup.userData.age += dt;
      var outT = clamp01(outGroup.userData.age / 0.7);
      var outEased = outT * outT;

      var outUniforms = outGroup.userData.allUniforms;
      if (outUniforms) {
        for (var i = 0; i < outUniforms.length; i++) {
          outUniforms[i].uOpacity.value = (1 - outEased) * state.lyricTargetOpacity;
        }
      }
      outGroup.position.z -= outEased * 6;
      outGroup.position.y += outEased * 2;
      outGroup.scale.setScalar(1 - outEased * 0.3);

      if (outT >= 1) {
        disposeLyricMeshFull(outGroup);
        state.lyricOutgoing = null;
      }
    }
  }

  function disposeLyricMeshFull(mesh) {
    if (!mesh) return;
    if (mesh.parent) mesh.parent.remove(mesh);
    mesh.traverse(function (obj) {
      if (obj.material) {
        if (obj.material.uniforms && obj.material.uniforms.uMap && obj.material.uniforms.uMap.value) {
          obj.material.uniforms.uMap.value.dispose();
        }
        if (obj.material.map) obj.material.map.dispose();
        obj.material.dispose();
      }
      if (obj.geometry) obj.geometry.dispose();
    });
  }

  // ── 鼠标交互：pointerRipple（与 Mineradio 一致）──
  var mouseDownAt = { t: 0, x: 0, y: 0, hadDrag: false };
  var UI_HIT_SELECTOR = '.viz-top,.viz-bottom,.viz-empty,.viz-loading';

  function isPointerOverUi(e) {
    if (!e) return false;
    var el = document.elementFromPoint(e.clientX, e.clientY);
    if (!el) return false;
    // 检查是否命中了 UI 元素
    return !!(el.closest && el.closest(UI_HIT_SELECTOR));
  }

  function pointerDown(e) {
    if (e.button === 2) return;
    if (isPointerOverUi(e)) return;
    mouseDownAt.t = performance.now();
    mouseDownAt.x = e.clientX;
    mouseDownAt.y = e.clientY;
    mouseDownAt.hadDrag = false;
    // 开始追踪拖动
    state.dragActive = true;
    state.dragLastX = e.clientX;
    state.dragLastY = e.clientY;
    state.dragLastT = performance.now();
    // 停止惯性
    state.yawVelocity = 0;
    state.phiVelocity = 0;
  }

  function pointerMove(e) {
    if (!state.dragActive) return;
    var dx = e.clientX - mouseDownAt.x;
    var dy = e.clientY - mouseDownAt.y;
    // 超过 4px 阈值 → 标记为拖动（不再触发涟漪）
    if (!mouseDownAt.hadDrag && Math.sqrt(dx * dx + dy * dy) > 4) {
      mouseDownAt.hadDrag = true;
    }
    if (!mouseDownAt.hadDrag) return;

    var now = performance.now();
    var dt = Math.max(1/120, (now - state.dragLastT) / 1000);
    var px = e.clientX - state.dragLastX;
    var py = e.clientY - state.dragLastY;

    // 拖动灵敏度（与 Mineradio 0.0032/0.0034 对应，MNL 场景较大故略提高）
    var SENSITIVITY = 0.0055;
    state.dragYaw += px * SENSITIVITY;
    state.dragPhi = clamp(state.dragPhi - py * SENSITIVITY * 0.65, 0.05, 0.88);

    // 惯性速度（松手后继续滑动，比 Mineradio 稍强以匹配大场景）
    state.yawVelocity = clamp(px * SENSITIVITY / dt * 0.55, -7.0, 7.0);
    state.phiVelocity = clamp(-py * SENSITIVITY * 0.65 / dt * 0.55, -5.0, 5.0);

    state.dragLastX = e.clientX;
    state.dragLastY = e.clientY;
    state.dragLastT = now;
  }

  function pointerUp(e) {
    state.dragActive = false;
    if (mouseDownAt.hadDrag) return;
    if (!state.camera) return;
    if (isPointerOverUi(e)) return;
    // 短按 → 涟漪
    var pressMs = Math.max(0, performance.now() - mouseDownAt.t);
    var strength = Math.min(0.25 + (pressMs / 1000) * 2.6, 3.0);
    var rect = state.renderer.domElement.getBoundingClientRect();
    var nx = ((e.clientX - rect.left) / rect.width - 0.5) * 34;
    var nz = (0.5 - (e.clientY - rect.top) / rect.height) * 34;
    addRipple(nx, nz, strength, false);
  }

  function bindPointerEvents(dom) {
    if (!dom) return;
    dom.addEventListener('mousedown', pointerDown);
    window.addEventListener('mousemove', pointerMove);
    window.addEventListener('mouseup', pointerUp);
    // 滚轮缩放（目标值，平滑过渡有惯性手感）
    dom.addEventListener('wheel', function (e) {
      if (isPointerOverUi(e)) return;
      e.preventDefault();
      state.targetRadius = clamp(state.targetRadius - e.deltaY * 0.05, 12, 80);
    }, { passive: false });
  }

  function unbindPointerEvents(dom) {
    if (!dom) return;
    dom.removeEventListener('mousedown', pointerDown);
    window.removeEventListener('mousemove', pointerMove);
    window.removeEventListener('mouseup', pointerUp);
  }

  function destroy() {
    state.running = false;
    if (state.rafId) cancelAnimationFrame(state.rafId);
    window.removeEventListener('resize', onResize);
    document.removeEventListener('visibilitychange', onVisibility);
    unbindPointerEvents(state.renderer && state.renderer.domElement);
    _visHidden = false;
    _ctxLost = false;

    function dispose(obj) {
      if (!obj) return;
      if (obj.geometry) obj.geometry.dispose();
      if (obj.material) obj.material.dispose();
    }
    dispose(state.terrain);
    dispose(state.starRiver);
    dispose(state.meteors);
    dispose(state.trails);
    disposeLyricMeshFull(state.lyricMesh);
    disposeLyricMeshFull(state.lyricOutgoing);
    if (state.lyricGroup && state.lyricGroup.parent) state.lyricGroup.parent.remove(state.lyricGroup);
    if (state.audioCtx) { try { state.audioCtx.close(); } catch (e) {} }
    if (state.renderer) {
      state.renderer.dispose();
      if (state.renderer.domElement && state.container) state.container.removeChild(state.renderer.domElement);
    }
    state.scene = null; state.camera = null;
    state.terrain = null; state.starRiver = null;
    state.meteors = null; state.trails = null;
    state.terrainMat = null;
    state.meteorMat = null; state.trailMat = null;
    state.root = null;
  }

  global.Visualizer = {
    init: init,
    setPlaying: setPlaying,
    setAudioSource: setAudioSource,
    setThemeColors: setThemeColors,
    showStageLyric: showStageLyric,
    pointerRipple: function (nx, nz, strength) { addRipple(nx, nz, strength || 1.2, false); },
    destroy: destroy
  };

})(typeof window !== 'undefined' ? window : globalThis);
