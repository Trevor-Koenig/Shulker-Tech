<?php

return [
    'paths' => [
        resource_path('views'),
    ],

    // Use storage_path() as fallback so this never returns false when the
    // directory doesn't exist yet (realpath() returns false for missing paths).
    'compiled' => env(
        'VIEW_COMPILED_PATH',
        realpath(storage_path('framework/views')) ?: storage_path('framework/views')
    ),
];
