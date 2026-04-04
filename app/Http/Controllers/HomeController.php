<?php

namespace App\Http\Controllers;

use App\Models\Server;
use App\Models\Setting;
use App\Services\Health;
use App\Services\ServerQuery;
use Illuminate\Support\Facades\Cache;
use Illuminate\View\View;

class HomeController extends Controller
{
    public function index(): View
    {
        $bluemapUrls = array_values(array_filter(
            array_map('trim', explode("\n", Setting::getValue('bluemap_url')))
        ));
        $initialIndex = !empty($bluemapUrls) ? array_rand($bluemapUrls) : 0;
        $rawBluemapUrl = $bluemapUrls[$initialIndex] ?? '';
        $bluemapOnline = $rawBluemapUrl !== '' && Health::isReachable($rawBluemapUrl);

        $servers = Server::active()->get();
        $serverStatuses = [];

        foreach ($servers as $server) {
            $cacheKey = 'server_status_' . $server->id;
            $status = Cache::get($cacheKey);
            if ($status === null) {
                $status = ServerQuery::query($server->host, $server->port) ?? ['online' => false];
                Cache::put($cacheKey, $status, 30);
            }
            $serverStatuses[$server->id] = $status;
        }

        return view('home', [
            'bluemapUrls'      => $bluemapUrls,
            'bluemapUrlsJson'  => json_encode(array_values($bluemapUrls)),
            'bluemapUrl'       => $rawBluemapUrl,
            'bluemapOnline'    => $bluemapOnline,
            'initialIndex'     => $initialIndex,
            'servers'          => $servers,
            'serverStatuses'   => $serverStatuses,
            'discordServerId'  => env('DISCORD_SERVER_ID', ''),
        ]);
    }
}
