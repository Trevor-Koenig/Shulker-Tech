@extends('layouts.app', ['title' => 'Setup — Shulker Tech'])

@section('content')
<div class="auth-page">
    <div class="auth-card">
        <div class="auth-card__brand">
            <span class="nav__logo-shulker">SHULKER</span><span class="nav__logo-tech"> TECH</span>
        </div>
        <h2 style="text-align:center; margin-bottom: 1.5rem;">First-Run Setup</h2>

        @if(!empty($error))
            <div class="alert alert--danger">{{ $error }}</div>
        @endif

        @if($errors->any())
            <div class="alert alert--danger">{{ $errors->first() }}</div>
        @endif

        <form method="POST" action="/setup" class="form">
            @csrf
            <div class="form__group">
                <label class="form__label" for="setup_token">Setup Token</label>
                <input type="password" id="setup_token" name="setup_token" class="form__input" required autofocus>
                <small class="form__hint">Set via ADMIN_SETUP_TOKEN in your .env file.</small>
            </div>
            <div class="form__group">
                <label class="form__label" for="username">Username</label>
                <input type="text" id="username" name="username" class="form__input"
                       required value="{{ old('username') }}">
            </div>
            <div class="form__group">
                <label class="form__label" for="email">Email</label>
                <input type="email" id="email" name="email" class="form__input"
                       required value="{{ old('email') }}">
            </div>
            <div class="form__group">
                <label class="form__label" for="password">Password</label>
                <input type="password" id="password" name="password" class="form__input" required minlength="8">
            </div>
            <button type="submit" class="btn btn--primary btn--full">Create Admin Account</button>
        </form>
    </div>
</div>
@endsection
