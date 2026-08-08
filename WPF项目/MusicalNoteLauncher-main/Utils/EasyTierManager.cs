using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MusicalNoteLauncher.Utils
{
	/// HMCL 兼容陶瓦管理器 (burningtnt/Terracotta --hmcl 模式)
	public class EasyTierManager
	{
		private static readonly string TerracottaDir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			".minecraft", "terracotta");

		private static readonly string CoreExePath = Path.Combine(TerracottaDir, "terracotta.exe");

		private Process _coreProcess;
		private CancellationTokenSource _stateCts;
		private int _apiPort;
		private string _roomCode;
		private bool _isRunning;

		public event Action<string> OnLogOutput;
		public event Action<string, string> OnStateChanged; // state, roomCode
		public event Action<string> OnRoomCodeReady; // HMCL 房间码

		public bool IsRunning => _isRunning;

		public bool IsCoreInstalled()
		{
			return File.Exists(CoreExePath);
		}

		public async Task<bool> DownloadCoreAsync(IProgress<int> progress = null)
		{
			string[] mirrorUrls = new[]
			{
				"http://103.79.186.213/terracotta.exe",
			};

			Exception lastError = null;

			foreach (string url in mirrorUrls)
			{
				try
				{
					OnLogOutput?.Invoke($"正在从 MNL 服务器下载陶瓦核心...");

					using var client = new HttpClient();
					client.Timeout = TimeSpan.FromMinutes(10);
					client.DefaultRequestHeaders.UserAgent.TryParseAdd("MNL");

					using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
					response.EnsureSuccessStatusCode();

					long totalBytes = response.Content.Headers.ContentLength ?? -1;
					using var stream = await response.Content.ReadAsStreamAsync();

					if (!Directory.Exists(TerracottaDir))
						Directory.CreateDirectory(TerracottaDir);

					using (var fileStream = new FileStream(CoreExePath, FileMode.Create, FileAccess.Write))
					{
						byte[] buffer = new byte[65536];
						long downloadedBytes = 0;
						int bytesRead;
						while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
						{
							await fileStream.WriteAsync(buffer, 0, bytesRead);
							downloadedBytes += bytesRead;
							if (totalBytes > 0 && progress != null)
								progress.Report((int)(downloadedBytes * 100 / totalBytes));
						}
					}

					OnLogOutput?.Invoke("陶瓦核心安装完成！");
					return true;
				}
				catch (Exception ex)
				{
					lastError = ex;
					OnLogOutput?.Invoke($"下载失败: {ex.Message}");
				}
			}

			OnLogOutput?.Invoke($"下载陶瓦核心失败：{lastError?.Message}");
			return false;
		}

		/// 简易 tar 提取（只提取文件，不处理目录结构）
		private static void ExtractTar(Stream tarStream, string outputDir)
		{
			byte[] header = new byte[512];
			while (true)
			{
				int read = tarStream.Read(header, 0, 512);
				if (read < 512 || header[0] == 0) break;

				// 读取文件名 (位置 0, 长度 100)
				string fileName = Encoding.ASCII.GetString(header, 0, 100).TrimEnd('\0').Trim();
				if (string.IsNullOrEmpty(fileName)) break;

				// 读取文件大小 (位置 124, 长度 12, 八进制)
				string sizeStr = Encoding.ASCII.GetString(header, 124, 12).TrimEnd('\0').Trim();
				if (!long.TryParse(sizeStr, System.Globalization.NumberStyles.Integer, null, out long fileSize))
					break;

				// 跳过目录项
				if (fileSize > 0 && !string.IsNullOrEmpty(fileName))
				{
					string outputPath = Path.Combine(outputDir, Path.GetFileName(fileName));
					byte[] fileData = new byte[fileSize];
					int bytesRead = 0;
					while (bytesRead < fileSize)
					{
						int r = tarStream.Read(fileData, bytesRead, (int)Math.Min(fileSize - bytesRead, 65536));
						if (r <= 0) break;
						bytesRead += r;
					}
					File.WriteAllBytes(outputPath, fileData);
				}

				// 对齐到 512 字节边界
				long padding = (512 - (fileSize % 512)) % 512;
				if (padding > 0)
					tarStream.Seek(padding, SeekOrigin.Current);
			}
		}

		/// 启动陶瓦 (HMCL --hmcl 模式)
		public async Task<bool> LaunchAsync()
		{
			if (!IsCoreInstalled()) { OnLogOutput?.Invoke("陶瓦核心未安装"); return false; }
			if (_isRunning) Stop();

			try
			{
				string tempDir = Path.Combine(Path.GetTempPath(), $"mnl-terracotta-{Environment.ProcessId}");
				if (Directory.Exists(tempDir))
					Directory.Delete(tempDir, true);
				Directory.CreateDirectory(tempDir);

				var args = $"--mnl \"{tempDir}\"";
				OnLogOutput?.Invoke("正在启动陶瓦核心 (HMCL 模式)...");

				var psi = new ProcessStartInfo
				{
					FileName = CoreExePath,
					Arguments = args,
					WorkingDirectory = TerracottaDir,
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				};

				_coreProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
				_coreProcess.OutputDataReceived += (s, e) =>
				{
					if (!string.IsNullOrEmpty(e.Data))
					{
						OnLogOutput?.Invoke(e.Data);
						// 从 stdout 解析端口号
						TryParsePort(e.Data);
					}
				};
				_coreProcess.ErrorDataReceived += (s, e) =>
				{
					if (!string.IsNullOrEmpty(e.Data))
					{
						OnLogOutput?.Invoke($"[ERROR] {e.Data}");
						TryParsePort(e.Data);
					}
				};
				_coreProcess.Exited += (s, e) =>
				{
					_isRunning = false;
					OnStateChanged?.Invoke("stopped", null);
				};

				_coreProcess.Start();
				_coreProcess.BeginOutputReadLine();
				_coreProcess.BeginErrorReadLine();
				_isRunning = true;

				// 等待 API 端口就绪（最多 15 秒）
				for (int i = 0; i < 30; i++)
				{
					await Task.Delay(500);
					if (_apiPort > 0) break;
				}

				if (_apiPort <= 0)
				{
					OnLogOutput?.Invoke("陶瓦核心启动超时，未能获取 API 端口");
					Stop();
					return false;
				}

				OnLogOutput?.Invoke($"陶瓦核心已启动，API 端口: {_apiPort}");

				// 开始状态轮询
				_stateCts = new CancellationTokenSource();
				_ = PollStateAsync(_stateCts.Token);

				return true;
			}
			catch (Exception ex)
			{
				OnLogOutput?.Invoke($"启动失败：{ex.Message}");
				return false;
			}
		}

		/// 从输出中解析 API 端口
		private void TryParsePort(string line)
		{
			if (_apiPort > 0) return;

			// Terracotta 输出格式: "Listening on http://127.0.0.1:PORT" 或包含 ":PORT" 的行
			foreach (string part in line.Split(' ', ':', '/', '\t'))
			{
				if (int.TryParse(part, out int port) && port >= 1024 && port <= 65535)
				{
					// 验证是否为 API
					_ = Task.Run(async () =>
					{
						try
						{
							using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
							var resp = await client.GetAsync($"http://127.0.0.1:{port}/state");
							if (resp.IsSuccessStatusCode)
							{
								_apiPort = port;
								OnLogOutput?.Invoke($"API 端口检测成功: {port}");
							}
						}
						catch { }
					});
					break;
				}
			}
		}

		/// 轮询 Terracotta HTTP API 状态
		private async Task PollStateAsync(CancellationToken token)
		{
			int lastIndex = -1;

			while (!token.IsCancellationRequested && _isRunning)
			{
				try
				{
					await Task.Delay(500, token);

					if (_apiPort <= 0) continue;

					using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
					using var response = await client.GetAsync(
						$"http://127.0.0.1:{_apiPort}/state", token);

					if (!response.IsSuccessStatusCode) continue;

					string json = await response.Content.ReadAsStringAsync();
					var node = JsonNode.Parse(json);
					if (node == null) continue;

					string state = node["state"]?.GetValue<string>() ?? "";
					int index = node["index"]?.GetValue<int>() ?? -1;

					if (index == lastIndex) continue;
					lastIndex = index;

					OnStateChanged?.Invoke(state, node["room"]?.GetValue<string>());

					// 检查 HostOK → 获取房间码
					if (state == "host-ok" && _roomCode == null)
					{
						_roomCode = node["room"]?.GetValue<string>();
						if (!string.IsNullOrEmpty(_roomCode))
						{
							OnRoomCodeReady?.Invoke(_roomCode);
						}
					}

					OnLogOutput?.Invoke($"状态: {state}" +
						(_roomCode != null ? $" 房间码: {_roomCode}" : ""));
				}
				catch (TaskCanceledException) { break; }
				catch { }
			}
		}

		/// 开始创建房间（房主模式）
		public async Task<bool> StartHostAsync()
		{
			if (_apiPort <= 0) return false;

			try
			{
				OnLogOutput?.Invoke("正在创建陶瓦房间...");
				using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
				using var response = await client.GetAsync(
					$"http://127.0.0.1:{_apiPort}/state/hosting");

				return response.IsSuccessStatusCode;
			}
			catch (Exception ex)
			{
				OnLogOutput?.Invoke($"创建房间失败：{ex.Message}");
				return false;
			}
		}

		/// 加入房间（房客模式）
		public async Task<bool> JoinRoomAsync(string roomCode)
		{
			if (_apiPort <= 0) return false;

			try
			{
				OnLogOutput?.Invoke($"正在加入房间: {roomCode}");
				var playerName = System.Security.Principal.WindowsIdentity.GetCurrent().Name
					.Split('\\').LastOrDefault() ?? "MNL_Player";
				var query = $"?room={Uri.EscapeDataString(roomCode)}&player={Uri.EscapeDataString(playerName)}";

				using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
				using var response = await client.GetAsync(
					$"http://127.0.0.1:{_apiPort}/state/guesting{query}");

				return response.IsSuccessStatusCode;
			}
			catch (Exception ex)
			{
				OnLogOutput?.Invoke($"加入房间失败：{ex.Message}");
				return false;
			}
		}

		public void Stop()
		{
			try
			{
				_stateCts?.Cancel();
				_stateCts?.Dispose();
				_stateCts = null;
			}
			catch { }

			try
			{
				if (_coreProcess != null && !_coreProcess.HasExited)
				{
					_coreProcess.Kill();
					_coreProcess = null;
				}
			}
			catch { }

			_isRunning = false;
			_apiPort = 0;
			_roomCode = null;
			OnStateChanged?.Invoke("stopped", null);
			OnLogOutput?.Invoke("陶瓦已停止");
		}

		public void Cleanup()
		{
			Stop();
			try { if (Directory.Exists(TerracottaDir)) Directory.Delete(TerracottaDir, true); } catch { }
		}
	}
}
