<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech\Tests;

use PHPUnit\Framework\TestCase;

class MigrationTest extends TestCase
{
    // Mirrors the splitting logic in Migration::run() so we can test it in isolation.
    private function splitSql(string $sql): array
    {
        $sql = preg_replace('/^\s*--.*$/m', '', $sql);
        return array_filter(array_map('trim', explode(';', $sql)));
    }

    public function testSingleStatementIsPreserved(): void
    {
        $sql    = 'CREATE TABLE `foo` (`id` INT PRIMARY KEY)';
        $result = array_values($this->splitSql($sql));

        $this->assertCount(1, $result);
        $this->assertSame($sql, $result[0]);
    }

    public function testMultipleStatementsAreSplit(): void
    {
        $sql = "CREATE TABLE `a` (`id` INT);\nCREATE TABLE `b` (`id` INT);";

        $result = array_values($this->splitSql($sql));

        $this->assertCount(2, $result);
        $this->assertSame('CREATE TABLE `a` (`id` INT)', $result[0]);
        $this->assertSame('CREATE TABLE `b` (`id` INT)', $result[1]);
    }

    public function testLineCommentsAreStripped(): void
    {
        $sql = "-- this is a comment\nCREATE TABLE `a` (`id` INT);";

        $result = array_values($this->splitSql($sql));

        $this->assertCount(1, $result);
        $this->assertStringNotContainsString('--', $result[0]);
    }

    public function testEmptyStatementsFromTrailingSemicolonAreExcluded(): void
    {
        // Files commonly end with a trailing semicolon + newline, producing an empty token
        $sql    = "CREATE TABLE `a` (`id` INT);\n";
        $result = $this->splitSql($sql);

        $this->assertCount(1, $result);
    }

    public function testBlankLinesAndWhitespaceOnlyTokensAreExcluded(): void
    {
        $sql    = "CREATE TABLE `a` (`id` INT);\n\n   \nCREATE TABLE `b` (`id` INT);";
        $result = array_values($this->splitSql($sql));

        $this->assertCount(2, $result);
    }

    public function testRealSchemaFileProducesMultipleStatements(): void
    {
        $file = __DIR__ . '/../database/001_schema.sql';
        $this->assertFileExists($file);

        $sql    = file_get_contents($file);
        $result = $this->splitSql($sql);

        // The schema has at least: SET FK_CHECKS, 6 CREATE TABLEs, SET FK_CHECKS, INSERTs
        $this->assertGreaterThanOrEqual(8, count($result));
    }
}
