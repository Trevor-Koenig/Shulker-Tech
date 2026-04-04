<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech;

use PDO;
use PDOException;
use RuntimeException;

class Database
{
    private static ?PDO $connection = null;

    public static function getConnection(): PDO
    {
        if (self::$connection === null) {
            $host = $_ENV['DB_HOST'];
            $port = $_ENV['DB_PORT'];
            $name = $_ENV['DB_NAME'];
            $user = $_ENV['DB_USER'];
            $pass = $_ENV['DB_PASSWORD'];

            $dsn = "mysql:host={$host};port={$port};dbname={$name};charset=utf8mb4";

            $options = [
                PDO::ATTR_ERRMODE            => PDO::ERRMODE_EXCEPTION,
                PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
                PDO::ATTR_EMULATE_PREPARES   => false,
            ];

            // Opt-in TLS: enabled when DB_SSL_CA is set.
            if (!empty($_ENV['DB_SSL_CA'])) {
                $options[PDO::MYSQL_ATTR_SSL_CA]          = $_ENV['DB_SSL_CA'];
                $options[PDO::MYSQL_ATTR_SSL_CERT]        = $_ENV['DB_SSL_CERT'] ?? null;
                $options[PDO::MYSQL_ATTR_SSL_KEY]         = $_ENV['DB_SSL_KEY']  ?? null;
                // Set DB_SSL_VERIFY=false to allow self-signed certs.
                $options[PDO::MYSQL_ATTR_SSL_VERIFY_SERVER_CERT] =
                    ($_ENV['DB_SSL_VERIFY'] ?? 'true') !== 'false';
            }

            try {
                self::$connection = new PDO($dsn, $user, $pass, $options);
            } catch (PDOException $e) {
                throw new RuntimeException('Database connection failed: ' . $e->getMessage());
            }
        }

        return self::$connection;
    }
}
