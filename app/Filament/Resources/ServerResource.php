<?php

namespace App\Filament\Resources;

use App\Filament\Resources\ServerResource\Pages;
use App\Models\Server;
use Filament\Forms;
use Filament\Forms\Form;
use Filament\Resources\Resource;
use Filament\Tables;
use Filament\Tables\Table;

class ServerResource extends Resource
{
    protected static ?string $model = Server::class;
    protected static ?string $navigationIcon = 'heroicon-o-server';
    protected static ?int $navigationSort = 1;

    public static function form(Form $form): Form
    {
        return $form->schema([
            Forms\Components\TextInput::make('name')
                ->required()->maxLength(100),
            Forms\Components\TextInput::make('slug')
                ->required()->maxLength(64)->unique(ignoreRecord: true)
                ->helperText('URL-friendly identifier, e.g. "survival"'),
            Forms\Components\Textarea::make('description')
                ->rows(3)->nullable(),
            Forms\Components\TextInput::make('host')
                ->required()->label('Hostname / IP'),
            Forms\Components\TextInput::make('port')
                ->required()->numeric()->default(25565)->minValue(1)->maxValue(65535),
            Forms\Components\TextInput::make('display_order')
                ->required()->numeric()->default(0),
            Forms\Components\Toggle::make('is_active')
                ->default(true)->label('Active'),
        ]);
    }

    public static function table(Table $table): Table
    {
        return $table
            ->columns([
                Tables\Columns\TextColumn::make('name')->sortable()->searchable(),
                Tables\Columns\TextColumn::make('slug')->sortable(),
                Tables\Columns\TextColumn::make('host'),
                Tables\Columns\TextColumn::make('port'),
                Tables\Columns\IconColumn::make('is_active')->boolean()->label('Active'),
                Tables\Columns\TextColumn::make('display_order')->sortable()->label('Order'),
            ])
            ->defaultSort('display_order')
            ->filters([
                Tables\Filters\TernaryFilter::make('is_active')->label('Active'),
            ])
            ->actions([Tables\Actions\EditAction::make()])
            ->bulkActions([Tables\Actions\BulkActionGroup::make([Tables\Actions\DeleteBulkAction::make()])]);
    }

    public static function getPages(): array
    {
        return [
            'index'  => Pages\ListServers::route('/'),
            'create' => Pages\CreateServer::route('/create'),
            'edit'   => Pages\EditServer::route('/{record}/edit'),
        ];
    }
}
