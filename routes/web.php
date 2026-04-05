<?php

use App\Http\Controllers\Api\EventsController;
use App\Http\Controllers\Auth\LoginController;
use App\Http\Controllers\Auth\SetupController;
use App\Http\Controllers\HomeController;
use App\Http\Controllers\Wiki\WikiController;
use Illuminate\Support\Facades\Route;

// ── First-run setup ──────────────────────────────────────────────────────────
// Accessible on any domain before any user exists.
Route::middleware('web')->group(function () {
    Route::get('/setup', [SetupController::class, 'show'])->name('setup.show');
    Route::post('/setup', [SetupController::class, 'store'])->name('setup.store');
});

// ── Home subdomain ───────────────────────────────────────────────────────────
// Registered twice: once scoped to the configured domain, once as a bare
// fallback so local dev works even if APP_DOMAIN isn't wired up yet.
$homeRoutes = function () {
    Route::get('/', [HomeController::class, 'index'])->name('home');
    Route::get('/login',  [LoginController::class, 'showLoginForm'])->name('login');
    Route::post('/login', [LoginController::class, 'login']);
    Route::post('/logout', [LoginController::class, 'logout'])->name('logout');
    Route::get('/api/events', [EventsController::class, 'stream']);
};

Route::domain(config('app.home_domain'))->middleware('web')->group($homeRoutes);

// Fallback: no-domain group catches requests when domain routing doesn't match
// (e.g. first-time local dev before APP_DOMAIN is configured).
Route::middleware('web')->group($homeRoutes);

// ── Wiki subdomain ───────────────────────────────────────────────────────────
Route::domain(config('app.wiki_domain'))->middleware('web')->group(function () {
    Route::get('/', [WikiController::class, 'index'])->name('wiki.home');

    Route::get('/login',  [LoginController::class, 'showLoginForm'])->name('wiki.login');
    Route::post('/login', [LoginController::class, 'login']);
    Route::post('/logout', [LoginController::class, 'logout'])->name('wiki.logout');
});
