<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('settings', function (Blueprint $table) {
            $table->string('key', 100)->primary();
            $table->text('value')->nullable();
            $table->string('type', 20)->default('text');
            $table->string('label', 100);
            $table->string('description', 255)->nullable();
        });

        DB::table('settings')->insert([
            'key'         => 'bluemap_url',
            'value'       => '',
            'type'        => 'textarea',
            'label'       => 'BlueMap URLs',
            'description' => 'One URL per line. A random one will be shown as the hero background on each page load. Leave blank to disable.',
        ]);
    }

    public function down(): void
    {
        Schema::dropIfExists('settings');
    }
};
