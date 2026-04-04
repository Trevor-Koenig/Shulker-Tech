<?php

namespace App\Http\Controllers\Wiki;

use App\Http\Controllers\Controller;
use Illuminate\View\View;

class WikiController extends Controller
{
    public function index(): View
    {
        return view('wiki.home');
    }
}
