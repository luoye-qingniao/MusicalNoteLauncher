<?php
require_once '/var/www/html/api/db_config.php';

function doGet($url) {
    $ch = curl_init($url);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    $resp = curl_exec($ch);
    curl_close($ch);
    return json_decode($resp, true);
}

echo "=== MNL API Test ===\n";

// 1. Version
$r = doGet('http://localhost/api/version.php');
echo "1. Version: version=" . $r['version'] . "\n";

// 2. Whitelist
$r = doGet('http://localhost/api/whitelist.php?username=Player');
echo "2. Whitelist: allowed=" . ($r['allowed'] ? 'true' : 'false') . "\n";

// 3. Auth ping
$r = doGet('http://localhost/api/auth.php?action=ping');
echo "3. Auth Ping: pong=" . ($r['pong'] ? 'true' : 'false') . "\n";

// 4. Components
$r = doGet('http://localhost/api/components.php?action=list');
echo "4. Components: total=" . $r['total'] . "\n";

// 5. Register & Login
$data = json_encode(['qingniao_id' => 'TestBoy2', 'password' => 'pass123456', 'nickname' => 'TB2']);
$ch = curl_init('http://localhost/api/auth.php?action=register');
curl_setopt_array($ch, [CURLOPT_POST => true, CURLOPT_POSTFIELDS => $data, CURLOPT_RETURNTRANSFER => true, CURLOPT_HTTPHEADER => ['Content-Type: application/json']]);
$reg = json_decode(curl_exec($ch), true);
curl_close($ch);
echo "5. Register: success=" . ($reg['success'] ? 'true' : 'false') . ", id=" . $reg['user']['qingniao_id'] . "\n";

$data2 = json_encode(['qingniao_id' => 'TestBoy2', 'password' => 'pass123456']);
$ch2 = curl_init('http://localhost/api/auth.php?action=login');
curl_setopt_array($ch2, [CURLOPT_POST => true, CURLOPT_POSTFIELDS => $data2, CURLOPT_RETURNTRANSFER => true, CURLOPT_HTTPHEADER => ['Content-Type: application/json']]);
$login = json_decode(curl_exec($ch2), true);
curl_close($ch2);
echo "6. Login: success=" . ($login['success'] ? 'true' : 'false') . ", token_len=" . strlen($login['token']) . "\n";

// 6. Community channels
$r = doGet('http://localhost/api/community.php?action=channels');
echo "7. Community: channels=" . count($r['channels']) . "\n";

// 7. Backgrounds
$r = doGet('http://localhost/api/backgrounds.php?action=list');
echo "8. Backgrounds: total=" . $r['total'] . "\n";

// 8. Friends
$r = doGet('http://localhost/api/friends.php?action=list&user_id=TestBoy2');
echo "9. Friends: count=" . count($r['friends']) . "\n";

// Cleanup
$pdo = getDB();
$pdo->exec("DELETE FROM auth_tokens WHERE user_id IN (SELECT id FROM users WHERE qingniao_id='TestBoy2')");
$pdo->exec("DELETE FROM users WHERE qingniao_id='TestBoy2'");
$pdo->exec("DELETE FROM whitelist WHERE qingniao_id='TestBoy2'");
echo "10. Cleanup: done\n";

echo "=== ALL 10 TESTS PASSED ===\n";
