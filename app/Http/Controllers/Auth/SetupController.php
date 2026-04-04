<?php

namespace App\Http\Controllers\Auth;

use App\Http\Controllers\Controller;
use App\Models\User;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\Hash;
use Illuminate\View\View;

class SetupController extends Controller
{
    public function show(): View|RedirectResponse
    {
        if (User::count() > 0) {
            return redirect('/login');
        }
        return view('auth.setup');
    }

    public function store(Request $request): View|RedirectResponse
    {
        if (User::count() > 0) {
            return redirect('/login');
        }

        $token = $request->input('setup_token', '');
        if (!hash_equals(env('ADMIN_SETUP_TOKEN', ''), $token)) {
            return view('auth.setup', ['error' => 'Invalid setup token.']);
        }

        $validated = $request->validate([
            'username' => ['required', 'string', 'max:50'],
            'email'    => ['required', 'email'],
            'password' => ['required', 'min:8'],
        ]);

        $user = User::create([
            'username' => $validated['username'],
            'email'    => $validated['email'],
            'password' => Hash::make($validated['password']),
            'is_active' => true,
        ]);

        $user->assignRole('Super Admin');

        Auth::login($user);

        return redirect(config('app.admin_domain') ? 'https://'.config('app.admin_domain') : '/');
    }
}
