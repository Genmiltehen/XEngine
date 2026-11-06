# XEngine.Interactive

Временный интерактивный harness для ручной проверки движка: окно Silk.NET,
OpenGL 3.3 Core, instanced-отрисовка кругов, камера-сущность и ввод.

Это не продуктовая подсистема рендера. Core о графике не знает; все пакеты
Silk.NET подключены только здесь.

## Запуск

```bash
dotnet build XEngine.slnx
dotnet run --project samples/XEngine.Interactive
```

`dotnet build` и `dotnet test` собирают проект, но не запускают окно, поэтому GPU
для CI не нужен.

## Управление

- `W` / `A` / `S` / `D` — движение игрока (жёлтый круг);
- `Space` — вернуть игрока в центр;
- мышь — курсорный круг (голубой);
- колесо мыши — зум камеры (0.25×…8×);
- закрытие окна завершает процесс.

## Что проверяется

- окно, GL-контекст и swap buffers;
- ECS-компоненты `Circle` и `Camera2D`, зарегистрированные модулем;
- `Transform2D` + `Camera2D` как обычная сущность;
- один instanced draw call для всех кругов (сетка 10×10, игрок и курсор);
- polling ввода в `PreUpdate`, edge detection, оси и накопление `ScrollDelta`;
- HiDPI-перевод координат мыши в framebuffer-пиксели и обратно в мир;
- сохранение aspect при resize;
- освобождение ресурсов при закрытии.

## Как устроен кадр

```text
graphics.PumpEvents()
engine.Update(dt)
  SceneManager.Update
    World.Update
      PreUpdate   InputSystem -> input.Poll(dt)
      Update      DemoControlSystem -> движение, мышь, зум
      PostUpdate  пусто
      Transform   TransformSystem, затем CircleRenderSystem
```

`CircleRenderSystem` живёт в `Transform`-фазе: после `TransformSystem` мировые
матрицы уже актуальны. `SwapBuffers` вызывается в конце системы.

`SceneManager.BuildScene` вызывает `world.UpdateTransforms()` до первого кадра.
Рендер-система пропускает такие вызовы по `world.Time.FrameCount == 0`. Это
работает, пока `UpdateTransforms()` не вызывают вручную посреди обычного кадра.

## Камера и HiDPI

`Camera2D.ViewPort` — полный видимый размер мира. Опорная высота (`ViewPort.Y`)
фиксирована, а `ViewPort.X` подгоняется под aspect framebuffer'а через
`Camera2D.FitTo`. `FitTo` вызывают и контроллер, и рендер, поэтому порядок систем
не влияет на результат.

`InputService` отдаёт позицию мыши в пикселях framebuffer'а. Перевод в мир делает
`CameraView.HiDpiToWorld`, сервис о камере не знает.

## Ввод

`IInputService.ScrollDelta` — накопленное колесо с прошлого `Poll` в тиках (X/Y).
`InputService` подписывается на `IMouse.Scroll` и складывает события в
`ScrollAccumulator`; `Poll` забирает значение и обнуляет его. HiDPI-масштаб к
скроллу не применяется, потому что это не координата.

## Ограничения

- один поток, одно окно;
- нет GPU-тестов, только ручная проверка и xUnit-тесты чистой логики;
- камера читается в `Update` с прошлого кадра — для статичной камеры незаметно;
- порядок систем держится на порядке модулей: `TransformModule` должен идти до
  `SilkTestModule`;
- `DrawElementsInstanced` использует `void*` и требует `AllowUnsafeBlocks`;
- GLSL-шейдеры лежат inline в `CircleRenderer`, без файлов и ассетов.
