<?php
require_once '/var/www/html/api/db_config.php';

$data = json_encode(['qingniao_id' => 'TestBoy', 'password' => 'abc123456', 'nickname' => 'TestBoy']);
$ch = curl_init('http://localhost/api/auth.php?action=register');
curl_setopt_array($ch, [
    CURLOPT_POST => true,
    CURLOPT_POSTFIELDS => $data,
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_HTTPHEADER => ['Content-Type: application/json']
]);
echo "Register: " . curl_exec($ch) . "\n";
curl_close($ch);

$data2 = json_encode(['qingniao_id' => 'TestBoy', 'password' => 'abc123456']);
$ch2 = curl_init('http://localhost/api/auth.php?action=login');
curl_setopt_array($ch2, [
    CURLOPT_POST => true,
    CURLOPT_POSTFIELDS => $data2,
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_HTTPHEADER => ['Content-Type: application/json']
]);
echo "Login: " . curl_exec($ch2) . "\n";
curl_close($ch2);
