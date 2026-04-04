<?php

declare(strict_types=1);

require_once __DIR__ . '/../../vendor/autoload.php';

use Trevor\ShulkerTech\Cache;
use Trevor\ShulkerTech\ServerQuery;
use Trevor\ShulkerTech\Models\Server;

// SSE headers — disable all buffering so events flush immediately
header('Content-Type: text/event-stream');
header('Cache-Control: no-cache');
header('X-Accel-Buffering: no'); // tells nginx/proxy not to buffer this response

if (ob_get_level()) {
    ob_end_clean();
}

// How often (seconds) to push each event type
const INTERVAL_SERVER_STATUS = 5;

$lastSent = [];

/**
 * Write a single SSE event to the client.
 * Named events let the JS side listen selectively with addEventListener().
 */
function sendEvent(string $event, mixed $data): void
{
    echo "event: {$event}\n";
    echo 'data: ' . json_encode($data) . "\n\n";
    flush();
}

function shouldSend(string $key, int $interval): bool
{
    global $lastSent;
    $now = time();
    if (!isset($lastSent[$key]) || ($now - $lastSent[$key]) >= $interval) {
        $lastSent[$key] = $now;
        return true;
    }
    return false;
}

// Send a heartbeat comment every loop so the connection stays alive
// and proxies don't close it from inactivity
function sendHeartbeat(): void
{
    echo ": heartbeat\n\n";
    flush();
}

while (true) {
    if (connection_aborted()) {
        break;
    }

    // --- Server status ---
    if (shouldSend('server-status', INTERVAL_SERVER_STATUS)) {
        $servers = Server::allActive();
        $statuses = [];

        foreach ($servers as $srv) {
            $cacheKey = 'server_status_' . $srv['id'];
            $status = Cache::get($cacheKey);

            if ($status === null) {
                $status = ServerQuery::query($srv['host'], (int) ($srv['port'] ?? 25565));
                Cache::set($cacheKey, $status ?? ['online' => false], INTERVAL_SERVER_STATUS);
            }

            $statuses[] = [
                'id' => $srv['id'],
                'name' => $srv['name'],
                'slug' => $srv['slug'],
                'status' => $status ?? ['online' => false],
            ];
        }

        sendEvent('server-status', $statuses);
    }

    // Add future event types here, e.g.:
    // if (shouldSend('announcements', 60)) { ... sendEvent('announcements', $data); }

    sendHeartbeat();
    sleep(5);
}
