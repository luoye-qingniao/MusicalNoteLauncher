using Renci.SshNet;

const string Host = "192.168.100.106";
const string User = "xyyd";
const string Pass = "zsq13892152486";
const string RemoteApiDir = "/var/www/mnl-api/api";
const string LocalApiDir = @"f:\mc音符启动器\MNL\MusicalNoteLauncher-main\server\api";
const string LocalSqlFile = @"f:\mc音符启动器\MNL\MusicalNoteLauncher-main\server\migration_v2.sql";

string RunCmd(SshClient c, string cmd)
{
    return c.RunCommand(cmd).Result?.Trim() ?? "";
}

string SudoCmd(SshClient c, string cmd)
{
    return RunCmd(c, $"echo '{Pass}' | sudo -S bash -c \"{cmd.Replace("\"", "\\\"")}\" 2>&1");
}

using var client = new SshClient(Host, User, Pass);
client.Connect();
Console.WriteLine("[OK] 已连接");

// ========== 1. 暂改权限 ==========
Console.WriteLine("\n=== 设置目录权限 ===");
Console.WriteLine(SudoCmd(client, $"chown {User}:{User} {RemoteApiDir}"));

// ========== 2. 上传 PHP（只上传新的，不覆盖 db_config.php） ==========
Console.WriteLine("\n=== 上传 PHP 文件 ===");
using var scp = new ScpClient(Host, User, Pass);
scp.Connect();

string[] newFiles = { "friends.php", "components.php" };
foreach (var fn in newFiles)
{
    var localPath = Path.Combine(LocalApiDir, fn);
    var remotePath = $"{RemoteApiDir}/{fn}";
    Console.WriteLine($"  {fn} -> {remotePath}");
    scp.Upload(new FileInfo(localPath), remotePath);
    Console.WriteLine($"  [OK]");
}
scp.Disconnect();

// ========== 3. 恢复权限 ==========
Console.WriteLine("\n=== 恢复权限 ===");
Console.WriteLine(SudoCmd(client, $"chown www-data:www-data {RemoteApiDir}"));
Console.WriteLine(SudoCmd(client, $"chown www-data:www-data {RemoteApiDir}/*.php"));
Console.WriteLine(SudoCmd(client, $"chmod 644 {RemoteApiDir}/*.php"));

// ========== 4. 执行 SQL 迁移 ==========
Console.WriteLine("\n=== 执行 SQL 迁移 ===");

// 先检查表是否已存在
var existingTables = SudoCmd(client, "mysql -u root -p'zsq13892152486' mnl_launcher -e 'SHOW TABLES;' 2>&1");
Console.WriteLine($"现有表:\n{existingTables}");

if (!existingTables.Contains("friends"))
{
    Console.WriteLine("需要创建新表...");
    using var scp2 = new ScpClient(Host, User, Pass);
    scp2.Connect();
    scp2.Upload(new FileInfo(LocalSqlFile), "/tmp/migration_v2.sql");
    scp2.Disconnect();

    // 去掉头部 USE 语句（数据库已选定），用 source 方式执行
    var migResult = SudoCmd(client, "mysql -u root -p'zsq13892152486' mnl_launcher < /tmp/migration_v2.sql 2>&1");
    Console.WriteLine($"迁移结果: {migResult}");
    RunCmd(client, "rm -f /tmp/migration_v2.sql");

    // 验证
    existingTables = SudoCmd(client, "mysql -u root -p'zsq13892152486' mnl_launcher -e 'SHOW TABLES;' 2>&1");
    Console.WriteLine($"更新后表:\n{existingTables}");
}
else
{
    Console.WriteLine("friends 表已存在，跳过迁移");
}

// ========== 5. 重启 nginx ==========
Console.WriteLine("\n=== 重启 nginx ===");
Console.WriteLine(SudoCmd(client, "systemctl reload nginx"));

// ========== 6. 验证 ==========
Console.WriteLine("\n=== 验证 API ===");
using var http = new HttpClient();
http.Timeout = TimeSpan.FromSeconds(10);

try
{
    var r = await http.GetAsync($"http://{Host}:8080/api/friends.php?action=list&user_id=test");
    Console.WriteLine($"friends.php: HTTP {(int)r.StatusCode} - {await r.Content.ReadAsStringAsync()}");
}
catch (Exception ex) { Console.WriteLine($"friends.php: {ex.Message}"); }

try
{
    var r = await http.GetAsync($"http://{Host}:8080/api/components.php?action=list");
    var body = await r.Content.ReadAsStringAsync();
    Console.WriteLine($"components.php: HTTP {(int)r.StatusCode} ({(body?.Length ?? 0)} bytes)");
    Console.WriteLine(body.Length > 300 ? body[..300] + "..." : body);
}
catch (Exception ex) { Console.WriteLine($"components.php: {ex.Message}"); }

client.Disconnect();
Console.WriteLine("\n[部署完成]");
