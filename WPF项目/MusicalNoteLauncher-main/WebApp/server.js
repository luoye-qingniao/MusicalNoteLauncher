/* ============================================================
   server.js - MNL 前端开发服务器
   功能: 静态文件服务 + 网易云音乐 API 代理 + MNL 服务器代理
   启动: node server.js
   访问: http://localhost:8080
   ============================================================ */

const http = require('http');
const https = require('https');
const url = require('url');
const path = require('path');
const fs = require('fs');
const crypto = require('crypto');

const PORT = 8080;
const MUSIC_TARGET = 'https://music.163.com';
const MNL_TARGET = 'http://85.137.246.87';
const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36';

// MIME 类型映射
const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
  '.wasm': 'application/wasm'
};

// 网易云要求的 Cookie（持久化，同一会话复用）
const cookies = `_ntes_nuid=${crypto.randomBytes(16).toString('hex')}; NMTID=00O${crypto.randomBytes(20).toString('hex')}`;

/* ── 静态文件服务 ── */
function serveStatic(req, res) {
  var parsed = url.parse(req.url);
  var filePath = path.join(__dirname, parsed.pathname === '/' ? 'index.html' : parsed.pathname);

  // 安全：禁止目录穿越
  if (!filePath.startsWith(__dirname)) {
    res.writeHead(403);
    res.end('403 Forbidden');
    return;
  }

  var ext = path.extname(filePath).toLowerCase();
  var contentType = MIME[ext] || 'application/octet-stream';

  fs.readFile(filePath, function(err, data) {
    if (err) {
      // SPA 回退：找不到文件时返回 index.html
      if (err.code === 'ENOENT' && ext === '') {
        fs.readFile(path.join(__dirname, 'index.html'), function(e2, d2) {
          if (e2) { res.writeHead(404); res.end('404 Not Found'); return; }
          res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
          res.end(d2);
        });
        return;
      }
      res.writeHead(404);
      res.end('404 Not Found');
      return;
    }
    res.writeHead(200, { 'Content-Type': contentType });
    res.end(data);
  });
}

/* ── 音乐 API 代理 ── */
function proxyMusic(req, res) {
  var backendPath = req.url.replace('/api/163', '');
  var targetUrl = MUSIC_TARGET + backendPath;

  console.log('[163music] ' + req.method + ' ' + backendPath);

  var options = {
    method: req.method,
    headers: {
      'User-Agent': UA,
      'Referer': 'https://music.163.com',
      'Accept': 'application/json, text/plain, */*',
      'Cookie': cookies
    }
  };

  var proxy = https.request(targetUrl, options, function(targetRes) {
    var body = [];
    targetRes.on('data', function(chunk) { body.push(chunk); });
    targetRes.on('end', function() {
      var buffer = Buffer.concat(body);
      res.writeHead(targetRes.statusCode, {
        'Content-Type': targetRes.headers['content-type'] || 'application/json; charset=utf-8',
        'Access-Control-Allow-Origin': '*'
      });
      res.end(buffer);
    });
  });

  proxy.on('error', function(err) {
    console.error('[163music] ERROR: ' + err.message);
    res.writeHead(502);
    res.end(JSON.stringify({ code: 502, message: '代理请求失败' }));
  });

  proxy.setTimeout(25000, function() {
    proxy.destroy();
    res.writeHead(504);
    res.end(JSON.stringify({ code: 504, message: '代理请求超时' }));
  });

  proxy.end();
}

/* ── 音频流代理（让 audio 元素跨域可用，支持 captureStream）── */
function proxyAudio(req, res) {
  // 解析 ?url= 参数
  var u = url.parse(req.url, true);
  var targetUrl = u.query.url;
  if (!targetUrl) {
    res.writeHead(400, { 'Access-Control-Allow-Origin': '*' });
    res.end('Missing url param');
    return;
  }
  // 解析目标 URL
  var parsed;
  try { parsed = url.parse(targetUrl); } catch (e) {
    res.writeHead(400, { 'Access-Control-Allow-Origin': '*' });
    res.end('Bad url');
    return;
  }
  // 只允许 http/https
  if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
    res.writeHead(400, { 'Access-Control-Allow-Origin': '*' });
    res.end('Bad protocol');
    return;
  }
  // 强制使用 https（网易云歌曲 URL 是 https）
  var finalUrl = targetUrl;
  if (parsed.protocol === 'http:') {
    finalUrl = 'https:' + targetUrl.substring(5);
    parsed = url.parse(finalUrl);
  }
  var lib = parsed.protocol === 'https:' ? https : http;

  console.log('[audio] GET ' + finalUrl);

  // 透传 Range（支持随机定位）
  var headers = {
    'User-Agent': UA,
    'Referer': 'https://music.163.com',
    'Accept': '*/*',
    'Cookie': cookies,
    'Connection': 'keep-alive'
  };
  if (req.headers['range']) headers['Range'] = req.headers['range'];
  if (req.headers['if-range']) headers['If-Range'] = req.headers['if-range'];

  var options = {
    method: 'GET',
    headers: headers
  };

  var proxy = lib.request(finalUrl, options, function(targetRes) {
    var status = targetRes.statusCode;
    // 302/301 跟随：简单跟随一次
    if (status === 301 || status === 302 || status === 303 || status === 307) {
      var redirect = targetRes.headers['location'];
      if (redirect) {
        var rParsed;
        try { rParsed = url.parse(redirect); } catch (e) { rParsed = null; }
        if (rParsed) {
          var rLib = rParsed.protocol === 'https:' ? https : http;
          var rProxy = rLib.request(redirect, options, function(rRes) {
            writeAudioResponse(rRes, res);
          });
          rProxy.on('error', function(err) {
            console.error('[audio redirect] ERROR: ' + err.message);
            res.writeHead(502, { 'Access-Control-Allow-Origin': '*' });
            res.end('Proxy error');
          });
          rProxy.end();
          return;
        }
      }
    }
    writeAudioResponse(targetRes, res);
  });

  proxy.on('error', function(err) {
    console.error('[audio] ERROR: ' + err.message);
    if (!res.headersSent) {
      res.writeHead(502, { 'Access-Control-Allow-Origin': '*' });
      res.end('Proxy error');
    }
  });

  proxy.setTimeout(30000, function() {
    proxy.destroy();
    if (!res.headersSent) {
      res.writeHead(504, { 'Access-Control-Allow-Origin': '*' });
      res.end('Timeout');
    }
  });

  proxy.end();
}

function writeAudioResponse(targetRes, res) {
  var outHeaders = {
    'Access-Control-Allow-Origin': '*',
    'Access-Control-Allow-Methods': 'GET, OPTIONS',
    'Access-Control-Allow-Headers': 'Range, If-Range, Content-Type',
    'Access-Control-Expose-Headers': 'Content-Length, Content-Range, Accept-Ranges, Content-Type'
  };
  // 透传音频相关头
  var passthroughs = ['content-type', 'content-length', 'content-range', 'accept-ranges', 'etag', 'last-modified', 'cache-control'];
  passthroughs.forEach(function(h) {
    if (targetRes.headers[h]) outHeaders[h] = targetRes.headers[h];
  });
  // audio 元素需要明确的 audio/mpeg 或 audio/mp4 才不会拒绝播放
  var ct = (outHeaders['content-type'] || '').toLowerCase();
  if (!ct || ct === 'application/octet-stream' || ct === 'binary/octet-stream') {
    var url = targetRes.headers['content-disposition'] || '';
    if (url.indexOf('.mp3') >= 0) outHeaders['content-type'] = 'audio/mpeg';
    else if (url.indexOf('.m4a') >= 0 || url.indexOf('.mp4') >= 0) outHeaders['content-type'] = 'audio/mp4';
    else if (url.indexOf('.aac') >= 0) outHeaders['content-type'] = 'audio/aac';
    else if (url.indexOf('.ogg') >= 0) outHeaders['content-type'] = 'audio/ogg';
    else if (url.indexOf('.flac') >= 0) outHeaders['content-type'] = 'audio/flac';
    else outHeaders['content-type'] = 'audio/mpeg';  // 兜底
  }
  res.writeHead(targetRes.statusCode, outHeaders);
  targetRes.pipe(res);
  // 客户端断开时销毁代理请求
  res.on('close', function() {
    try { targetRes.destroy(); } catch (e) {}
  });
}

/* ── 主路由 ── */
var server = http.createServer(function(req, res) {
  // 封面图片代理（解决 Canvas getImageData 跨域问题）
  if (req.url.startsWith('/api/img')) {
    var u = url.parse(req.url, true);
    var imgUrl = u.query.url;
    if (!imgUrl) { res.writeHead(400, { 'Access-Control-Allow-Origin': '*' }); res.end('Missing url'); return; }
    var lib = imgUrl.startsWith('https') ? https : http;
    lib.get(imgUrl, function(imgRes) {
      res.writeHead(imgRes.statusCode, {
        'Access-Control-Allow-Origin': '*',
        'Content-Type': imgRes.headers['content-type'] || 'image/jpeg',
        'Cache-Control': 'public, max-age=3600'
      });
      imgRes.pipe(res);
    }).on('error', function() { res.writeHead(502); res.end(); });
    return;
  }

  // 音频流代理路由（让 audio 元素能跨域 + captureStream）
  if (req.url.startsWith('/api/audio')) {
    // OPTIONS 预检
    if (req.method === 'OPTIONS') {
      res.writeHead(204, {
        'Access-Control-Allow-Origin': '*',
        'Access-Control-Allow-Methods': 'GET, OPTIONS',
        'Access-Control-Allow-Headers': 'Range, If-Range, Content-Type',
        'Access-Control-Max-Age': '86400'
      });
      res.end();
      return;
    }
    proxyAudio(req, res);
    return;
  }

  // 音乐 API 代理路由
  if (req.url.startsWith('/api/163')) {
    proxyMusic(req, res);
    return;
  }

  // 所有其他请求 → 静态文件
  serveStatic(req, res);
});

server.listen(PORT, function() {
  console.log('===================================');
  console.log('  MNL 开发服务器已启动');
  console.log('  地址: http://localhost:' + PORT);
  console.log('  静态: ' + __dirname);
  console.log('  音乐代理: /api/163/* → music.163.com');
  console.log('  音频代理: /api/audio?url=... （CORS + captureStream 支持）');
  console.log('===================================');
});
