<?php
require_once '/var/www/html/api/db_config.php';

function doGet($url) {
    $ch = curl_init($url);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    $r = curl_exec($ch);
    curl_close($ch);
    return json_decode($r, true);
}

echo "=== Community API Test ===\n";

// 1. Get channels
$r = doGet('http://localhost/api/community.php?action=channels');
echo "1. Channels: count=" . count($r['channels']) . "\n";

// 2. Create a channel
$data = json_encode(['name' => 'TestChannel', 'description' => 'Test desc', 'icon_emoji' => '💬']);
$ch = curl_init('http://localhost/api/community.php?action=create_channel');
curl_setopt_array($ch, [
    CURLOPT_POST => true,
    CURLOPT_POSTFIELDS => $data,
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_HTTPHEADER => ['Content-Type: application/json']
]);
$resp = json_decode(curl_exec($ch), true);
curl_close($ch);
echo "2. Create: " . ($resp['success'] ? "OK id={$resp['id']}" : "ERROR") . "\n";

// 3. Get channels again
$r = doGet('http://localhost/api/community.php?action=channels');
echo "3. Channels: count=" . count($r['channels']) . "\n";

// 4. Send a message
$data2 = json_encode(['channel_id' => 1, 'sender_id' => 'TestUser', 'content' => 'Hello World!']);
$ch2 = curl_init('http://localhost/api/community.php?action=send');
curl_setopt_array($ch2, [
    CURLOPT_POST => true,
    CURLOPT_POSTFIELDS => $data2,
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_HTTPHEADER => ['Content-Type: application/json']
]);
$resp2 = json_decode(curl_exec($ch2), true);
curl_close($ch2);
echo "4. Send: " . ($resp2['success'] ? "OK id={$resp2['id']}" : "ERROR") . "\n";

// 5. Get messages
$r = doGet('http://localhost/api/community.php?action=messages&channel_id=1');
echo "5. Messages: count=" . count($r['messages']) . "\n";
foreach ($r['messages'] as $m) {
    echo "   - [{$m['sender_id']}] {$m['content']}\n";
}

echo "=== ALL DONE ===\n";
