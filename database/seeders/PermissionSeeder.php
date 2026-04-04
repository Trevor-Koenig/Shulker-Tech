<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Spatie\Permission\Models\Permission;
use Spatie\Permission\Models\Role;

class PermissionSeeder extends Seeder
{
    public function run(): void
    {
        // Reset cached roles and permissions
        app()[\Spatie\Permission\PermissionRegistrar::class]->forgetCachedPermissions();

        $permissions = [
            'admin.access'   => 'Can log in to the admin panel',
            'servers.view'   => 'Can view the server list in admin',
            'servers.create' => 'Can add new servers',
            'servers.edit'   => 'Can edit existing servers',
            'servers.delete' => 'Can delete servers',
            'users.view'     => 'Can view the user list',
            'users.create'   => 'Can create new users',
            'users.edit'     => 'Can edit existing users',
            'users.delete'   => 'Can delete users',
            'roles.view'     => 'Can view the role list',
            'roles.create'   => 'Can create new roles',
            'roles.edit'     => 'Can edit existing roles',
            'roles.delete'   => 'Can delete roles',
        ];

        foreach ($permissions as $name => $description) {
            Permission::firstOrCreate(['name' => $name, 'guard_name' => 'web'], ['description' => $description]);
        }

        // Super Admin — all permissions
        $superAdmin = Role::firstOrCreate(['name' => 'Super Admin', 'guard_name' => 'web'], ['description' => 'Full access to everything']);
        $superAdmin->syncPermissions(Permission::all());

        // Admin — everything except role management
        $admin = Role::firstOrCreate(['name' => 'Admin', 'guard_name' => 'web'], ['description' => 'Full access except role management']);
        $admin->syncPermissions(
            Permission::whereNotIn('name', ['roles.create', 'roles.edit', 'roles.delete'])->get()
        );

        // Moderator — read-only
        $moderator = Role::firstOrCreate(['name' => 'Moderator', 'guard_name' => 'web'], ['description' => 'Read-only access to admin panel']);
        $moderator->syncPermissions(
            Permission::whereIn('name', ['admin.access', 'servers.view', 'users.view', 'roles.view'])->get()
        );
    }
}
