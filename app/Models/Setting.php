<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;

class Setting extends Model
{
    protected $primaryKey = 'key';
    protected $keyType = 'string';
    public $incrementing = false;
    public $timestamps = false;

    protected $fillable = [
        'key',
        'value',
        'type',
        'label',
        'description',
    ];

    public static function getValue(string $key, string $default = ''): string
    {
        return static::find($key)?->value ?? $default;
    }

    public static function setValue(string $key, string $value): void
    {
        static::where('key', $key)->update(['value' => $value]);
    }
}
