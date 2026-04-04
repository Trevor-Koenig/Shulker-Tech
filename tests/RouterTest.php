<?php

declare(strict_types=1);

namespace Trevor\ShulkerTech\Tests;

use PHPUnit\Framework\TestCase;
use Trevor\ShulkerTech\Router;

class RouterTest extends TestCase
{
    private Router $router;

    protected function setUp(): void
    {
        $this->router = new Router();
        // Reset the HTTP response code to 200 before each test
        http_response_code(200);
    }

    public function testGetRouteDispatchesHandler(): void
    {
        $called = false;
        $this->router->get('/test', function () use (&$called) {
            $called = true;
        });

        ob_start();
        $this->router->dispatch('GET', '/test');
        ob_end_clean();

        $this->assertTrue($called);
    }

    public function testPostRouteDispatchesHandler(): void
    {
        $called = false;
        $this->router->post('/submit', function () use (&$called) {
            $called = true;
        });

        ob_start();
        $this->router->dispatch('POST', '/submit');
        ob_end_clean();

        $this->assertTrue($called);
    }

    public function testGetRouteDoesNotMatchPostMethod(): void
    {
        $called = false;
        $this->router->get('/test', function () use (&$called) {
            $called = true;
        });

        ob_start();
        $this->router->dispatch('POST', '/test');
        ob_end_clean();

        $this->assertFalse($called);
    }

    public function testPostRouteDoesNotMatchGetMethod(): void
    {
        $called = false;
        $this->router->post('/submit', function () use (&$called) {
            $called = true;
        });

        ob_start();
        $this->router->dispatch('GET', '/submit');
        ob_end_clean();

        $this->assertFalse($called);
    }

    public function testGuardIsCalledBeforeHandler(): void
    {
        $log = [];
        $this->router->get(
            '/protected',
            function () use (&$log) { $log[] = 'handler'; },
            function () use (&$log) { $log[] = 'guard'; }
        );

        ob_start();
        $this->router->dispatch('GET', '/protected');
        ob_end_clean();

        $this->assertSame(['guard', 'handler'], $log);
    }

    public function testGuardCanPreventHandlerExecution(): void
    {
        $handlerCalled = false;
        $this->router->get(
            '/secure',
            function () use (&$handlerCalled) { $handlerCalled = true; },
            function () { throw new \RuntimeException('Access denied'); }
        );

        $this->expectException(\RuntimeException::class);
        $this->router->dispatch('GET', '/secure');

        $this->assertFalse($handlerCalled);
    }

    public function testUnknownRouteReturns404(): void
    {
        $this->router->get('/exists', function () {});

        ob_start();
        $this->router->dispatch('GET', '/does-not-exist');
        ob_end_clean();

        $this->assertSame(404, http_response_code());
    }

    public function testNotFoundGuardIsCalledForUnknownRoute(): void
    {
        $guardCalled = false;
        $this->router->get('/exists', function () {});

        ob_start();
        $this->router->dispatch('GET', '/does-not-exist', function () use (&$guardCalled) {
            $guardCalled = true;
        });
        ob_end_clean();

        $this->assertTrue($guardCalled);
    }

    public function testNotFoundGuardCanPrevent404Page(): void
    {
        $this->router->get('/exists', function () {});

        $this->expectException(\RuntimeException::class);
        $this->expectExceptionMessage('Redirecting to login');

        $this->router->dispatch('GET', '/does-not-exist', function () {
            throw new \RuntimeException('Redirecting to login');
        });
    }

    public function testUrlParamsArePassedToHandler(): void
    {
        $receivedParams = null;
        $this->router->get('/users/{id}', function (array $params) use (&$receivedParams) {
            $receivedParams = $params;
        });

        ob_start();
        $this->router->dispatch('GET', '/users/42');
        ob_end_clean();

        $this->assertSame(['id' => '42'], $receivedParams);
    }

    public function testMultipleUrlParamsArePassedToHandler(): void
    {
        $receivedParams = null;
        $this->router->get('/servers/{id}/edit', function (array $params) use (&$receivedParams) {
            $receivedParams = $params;
        });

        ob_start();
        $this->router->dispatch('GET', '/servers/7/edit');
        ob_end_clean();

        $this->assertSame(['id' => '7'], $receivedParams);
    }

    public function testDispatchMethodIsCaseInsensitive(): void
    {
        $called = false;
        $this->router->get('/test', function () use (&$called) {
            $called = true;
        });

        ob_start();
        $this->router->dispatch('get', '/test');
        ob_end_clean();

        $this->assertTrue($called);
    }
}
