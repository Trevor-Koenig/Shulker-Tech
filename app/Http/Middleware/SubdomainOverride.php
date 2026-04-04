<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

/**
 * In local dev, SUBDOMAIN_OVERRIDE=admin (or wiki) rewrites the Host header
 * before routing so that Laravel's domain() route groups match correctly.
 *
 * e.g. SUBDOMAIN_OVERRIDE=admin + APP_DOMAIN=localhost
 *      → rewrites Host to "admin.localhost"
 */
class SubdomainOverride
{
    public function handle(Request $request, Closure $next): Response
    {
        $override = env('SUBDOMAIN_OVERRIDE', '');

        if ($override !== '') {
            $domain = config('app.domain', 'localhost');
            $fakeHost = "{$override}.{$domain}";
            $request->headers->set('HOST', $fakeHost);
            $request->server->set('HTTP_HOST', $fakeHost);
            $request->server->set('SERVER_NAME', $fakeHost);
        }

        return $next($request);
    }
}
