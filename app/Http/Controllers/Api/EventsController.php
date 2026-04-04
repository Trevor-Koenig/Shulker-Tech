<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Server;
use App\Services\ServerQuery;
use Illuminate\Support\Facades\Cache;
use Symfony\Component\HttpFoundation\StreamedResponse;

class EventsController extends Controller
{
    private const INTERVAL_SERVER_STATUS = 5;

    public function stream(): StreamedResponse
    {
        return response()->stream(function () {
            $lastSent = [];

            while (true) {
                if (connection_aborted()) {
                    break;
                }

                if ($this->shouldSend('server-status', self::INTERVAL_SERVER_STATUS, $lastSent)) {
                    $servers = Server::active()->get();
                    $statuses = [];

                    foreach ($servers as $server) {
                        $cacheKey = 'server_status_' . $server->id;
                        $status = Cache::get($cacheKey);

                        if ($status === null) {
                            $status = ServerQuery::query($server->host, $server->port) ?? ['online' => false];
                            Cache::put($cacheKey, $status, self::INTERVAL_SERVER_STATUS);
                        }

                        $statuses[] = [
                            'id'     => $server->id,
                            'name'   => $server->name,
                            'slug'   => $server->slug,
                            'status' => $status,
                        ];
                    }

                    $this->sendEvent('server-status', $statuses);
                }

                echo ": heartbeat\n\n";
                ob_flush();
                flush();
                sleep(5);
            }
        }, 200, [
            'Content-Type'      => 'text/event-stream',
            'Cache-Control'     => 'no-cache',
            'X-Accel-Buffering' => 'no',
        ]);
    }

    private function shouldSend(string $key, int $interval, array &$lastSent): bool
    {
        $now = time();
        if (!isset($lastSent[$key]) || ($now - $lastSent[$key]) >= $interval) {
            $lastSent[$key] = $now;
            return true;
        }
        return false;
    }

    private function sendEvent(string $event, mixed $data): void
    {
        echo "event: {$event}\n";
        echo 'data: ' . json_encode($data) . "\n\n";
        ob_flush();
        flush();
    }
}
