<?php

namespace App\Http\Middleware;

use App\Models\User;
use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

class EnsureAppIsSetUp
{
    public function handle(Request $request, Closure $next): Response
    {
        if (!$request->routeIs('setup.*') && User::count() === 0) {
            return redirect()->route('setup.show');
        }

        return $next($request);
    }
}
