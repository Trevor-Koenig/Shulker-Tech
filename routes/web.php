<?php

use App\Http\Controllers\Api\EventsController;
use App\Http\Controllers\Auth\LoginController;
use App\Http\Controllers\Auth\SetupController;
use App\Http\Controllers\HomeController;
use App\Http\Controllers\Wiki\WikiController;
use App\Models\User;
use Illuminate\Support\Facades\Route;

// ── First-run setup ──────────────────────────────────────────────────────────
// Accessible on any domain before any user exists.
Route::middleware('web')->group(function () {
    Route::get('/setup', [SetupController::class, 'show'])->name('setup.show');
    Route::post('/setup', [SetupController::class, 'store'])->name('setup.store');
});

// ── Home subdomain ───────────────────────────────────────────────────────────
Route::domain(config('app.home_domain'))->middleware('web')->group(function () {
    Route::get('/', [HomeController::class, 'index'])->name('home');

    Route::get('/login',  [LoginController::class, 'showLoginForm'])->name('login');
    Route::post('/login', [LoginController::class, 'login']);
    Route::post('/logout', [LoginController::class, 'logout'])->name('logout');

    Route::get('/api/events', [EventsController::class, 'stream']);
});

// ── Wiki subdomain ───────────────────────────────────────────────────────────
Route::domain(config('app.wiki_domain'))->middleware('web')->group(function () {
    Route::get('/', [WikiController::class, 'index'])->name('wiki.home');

    Route::get('/login',  [LoginController::class, 'showLoginForm'])->name('wiki.login');
    Route::post('/login', [LoginController::class, 'login']);
    Route::post('/logout', [LoginController::class, 'logout'])->name('wiki.logout');
});
