# План реализации `samples/XEngine.Interactive`

Рабочий план временного интерактивного harness. Проект нужен только для ручных
тестов движка: окно, ввод, камера и отрисовка кругов. В продуктовую подсистему
рендера он не превращается.

Связанные документы:

- `docs/plans.md` — lifecycle-хуки `Engine`.
- `legacy/Input` — исходная логика действий, осей и edge detection.
- `legacy/Shaders` — старые GLSL-шейдеры, не обязательны к переносу.
- GunFight, `XEngine/Graphics/OpenGL` — референс по `Shader`, `UnitQuad`,
  `LineBatcher` и ручному управлению GL-ресурсами.

## 1. Цель и границы

Цель:

- окно на Silk.NET;
- ECS-компонент `Circle` и отрисовка кругов через шейдеры;
- instanced draw для всех кругов одним вызовом;
- камера как обычная сущность `Transform2D + Camera2D`;
- ввод как сервис движка и система поллинга;
- конвертация HiDPI ↔ мир через helper, без преобразований внутри `InputService`;
- один поток, одно окно, OpenGL 3.3 Core;
- ручная проверка, без GPU-тестов.

Не входит:

- спрайты, текстуры, текст, NineSlice;
- физика;
- мультиоконность и фоновый рендер;
- production-камера, render graph, batching спрайтов;
- редактор, ассеты, hot-reload;
- автоматические тесты, которым нужен GPU.

## 2. Зависимость от Core

Проект опирается на lifecycle-хуки из `docs/plans.md`:

- `IEngineModule.OnEngineCreated(Engine engine)`;
- `IEngineModule.OnEngineDisposing(Engine engine)`.

Без них окно и GL пришлось бы создавать либо в `ConfigureServices` (тогда
`EngineBuilder.Build()` требует GPU), либо вручную в `Program.cs` в обход модуля.
Хуки позволяют модулю владеть ресурсами и при этом не добавлять `OnFrame`.

`Core` не получает ссылку на Silk.NET. Все пакеты Silk.NET подключаются только
в `samples/XEngine.Interactive`.

## 3. Структура проекта

```text
samples/XEngine.Interactive/
  XEngine.Interactive.csproj
  Program.cs
  SilkTestOptions.cs
  SilkTestModule.cs
  SilkGraphicsService.cs
  Camera/
    Camera2D.cs
    CameraView.cs
  Input/
    IInputService.cs
    InputService.cs
    InputSystem.cs
    Axis.cs
    AxisSettings.cs
  Render/
    Circle.cs
    CircleInstance.cs
    CircleRenderer.cs
    CircleRenderSystem.cs
    Shader.cs
    UnitQuad.cs
  Demo/
    DemoScene.cs
    DemoControlSystem.cs
```

Проект добавляется в `XEngine.slnx` в папку `/samples/`. `dotnet build` и
`dotnet test` собирают его, но не запускают, поэтому GPU для проверок не нужен.

## 4. Хост и порядок кадра

Главный цикл остаётся в `Program.cs`:

```text
engine = EngineBuilder
    .AddModule(new TransformModule())
    .AddModule(new SilkTestModule(options))
    .Build()

engine.Scenes.Replace(new DemoScene(), "demo")

while (!graphics.IsClosing)
{
    graphics.PumpEvents()
    if (graphics.IsClosing) break
    dt = clamp(clock.Delta, 0.1)
    engine.Update(dt)
}
```

Порядок внутри кадра:

```text
graphics.PumpEvents()                 // события окна, DoEvents
engine.Update(dt)
  SceneManager.Update
    World.Update
      PreUpdate   -> InputSystem: input.Poll(dt)
      Update      -> DemoControlSystem: движение, конвертация мыши
      PostUpdate  -> пусто
      Transform   -> TransformSystem, затем CircleRenderSystem
                       -> clear + instanced draw + SwapBuffers
```

Почему так:

- события окна качаются до обновления мира;
- ввод поллится ровно один раз за кадр, до игровых систем;
- трансформы пересчитываются до рендера;
- рендер идёт в `Transform`-фазе, после `TransformSystem`;
- `SwapBuffers` вызывается в конце кадра, без `OnFrame`-хука.

## 5. `SilkGraphicsService`

Сервис владеет платформенными ресурсами:

- `IWindow`;
- `GL`;
- `IInputContext`;
- `FramebufferSize`;
- `IsClosing`;
- `PumpEvents()`;
- `SwapBuffers()`;
- `Dispose()`.

Окно создаётся только в `OnEngineCreated`, не в `ConfigureServices`.

Параметры окна:

- `ContextAPI.OpenGL`;
- `ContextProfile.Core`;
- `ContextVersion.Version33`;
- `ContextFlags.ForwardCompatible`;
- VSync включён;
- `ShouldSwapAutomatically = false` — swap делает рендер-система;
- размер и заголовок из `SilkTestOptions`.

Порядок инициализации:

```text
Window.Create(options)
window.Initialize()
gl = GL.GetApi(window)
input = window.CreateInput()
```

Порядок освобождения:

```text
CircleRenderer.Dispose()
input.Dispose()
gl.Dispose()
window.Dispose()
```

Всё делается на главном потоке. Фоновые задачи и второе окно не поддерживаются.

## 6. Шейдеры и quad по референсу GunFight

Из GunFight берём не код целиком, а структуру:

- `Shader` — компиляция, линковка, кэш uniform-локаций, `Use()`;
- `UnitQuad` — VAO/VBO/EBO для одного quad;
- `CircleRenderer` — аналог `LineBatcher`/`RenderModule`: динамический буфер и
  один draw call.

Отличия OpenTK → Silk.NET:

| GunFight/OpenTK | Silk.NET |
| --- | --- |
| статический `GL` | экземпляр `GL gl` |
| `ShaderType.VertexShader` | `ShaderType.VertexShader` |
| `BufferTarget.ArrayBuffer` | `BufferTargetARB.ArrayBuffer` |
| `BufferUsageHint.DynamicDraw` | `BufferUsageARB.DynamicDraw` |
| `VertexAttribPointerType.Float` | `VertexAttribPointerType.Float` |
| `PrimitiveType.Triangles` | `PrimitiveType.Triangles` |
| `DrawElementsType.UnsignedInt` | `DrawElementsType.UnsignedInt` |
| `GL.DrawElementsInstanced` | `gl.DrawElementsInstanced` |
| `GL.UniformMatrix4(loc, false, ref m)` | проверить перегрузку Silk: `Span<float>`/`ref` + transpose |

Правила:

- ошибки компиляции и линковки не писать в `Debug.WriteLine`, а бросать исключение
  с текстом лога;
- не делать глобальный `GLProvider` со статическим `GL`;
- шейдеры хранить inline в `CircleRenderer` (raw string), без файлов и ассетов;
- матрицы `System.Numerics` row-major: при `UniformMatrix4` либо передавать
  `transpose = true`, либо транспонировать вручную;
- GLSL 330 Core.

## 7. Instanced draw кругов

### 7.1. Геометрия

Unit quad в локальных координатах `[-1..1]`:

```text
(-1, -1), (1, -1), (1, 1), (-1, 1)
```

Индексы: `0, 1, 2, 2, 3, 0`.

Позиция вершины используется и как локальная координата для SDF-круга.

### 7.2. Instance data

На один круг:

```text
vec2 center
float radius
vec4 color
```

Итого 7 float = 28 байт на instance.

Инстансный VBO динамический. При росте количества кругов capacity удваивается,
данные загружаются через `BufferData`/`BufferSubData`, как в `LineBatcher`.

Атрибуты:

| location | данные | divisor |
| --- | --- | --- |
| 0 | `aPos` (vec2, quad) | 0 |
| 1 | `aCenter` (vec2) | 1 |
| 2 | `aRadius` (float) | 1 |
| 3 | `aColor` (vec4) | 1 |

`VertexAttribDivisor` выставляется для location 1–3.

### 7.3. Шейдеры

Вершинный:

```glsl
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aCenter;
layout(location = 2) in float aRadius;
layout(location = 3) in vec4 aColor;

uniform mat4 uViewProjection;

out vec2 vLocal;
out vec4 vColor;

void main()
{
    vLocal = aPos;
    vColor = aColor;
    gl_Position = uViewProjection * vec4(aCenter + aPos * aRadius, 0.0, 1.0);
}
```

Фрагментный:

```glsl
in vec2 vLocal;
in vec4 vColor;
out vec4 FragColor;

void main()
{
    float d = length(vLocal);
    float aa = fwidth(d);
    float alpha = 1.0 - smoothstep(1.0 - aa, 1.0, d);
    if (alpha <= 0.0) discard;
    FragColor = vec4(vColor.rgb, vColor.a * alpha);
}
```

### 7.4. Отрисовка

`CircleRenderSystem`:

1. Проверяет `world.Time.FrameCount > 0`.
2. Берёт `FramebufferSize`; если `0`, кадр пропускается.
3. Ищет первую сущность с `Transform2D + Camera2D`.
4. Строит `CameraView`.
5. Собирает instances из `Query<Transform2D, Circle>`:
   - center = `Transform2D.World.Translation`;
   - radius = `Circle.Radius * max(abs(World scale))`;
   - color = `Circle.Color`.
6. Делает `glViewport`, `glClear`, настройку blend.
7. `shader.Use()`, загрузка `uViewProjection`.
8. Загрузка instance VBO.
9. `gl.DrawElementsInstanced(Triangles, 6, UnsignedInt, 0, count)`.
10. `SwapBuffers()`.

Все круги рисуются одним instanced draw call. Если кругов нет — только clear и
swap.

### 7.5. Состояние GL

- blending: `SrcAlpha` / `OneMinusSrcAlpha`;
- depth test выключен;
- face culling не нужен;
- viewport обновляется каждый кадр из framebuffer size;
- после кадра VAO отвязывается.

### 7.6. Guard для `UpdateTransforms`

`SceneManager.BuildScene` вызывает `world.UpdateTransforms()` сразу после
построения сцены. Это запускает `Transform`-фазу, где уже находится
`CircleRenderSystem`. Без guard рендер произойдёт посреди построения сцены.

Условие `world.Time.FrameCount > 0` отличает обычный кадр от служебного
`UpdateTransforms` при сборке сцены. Это ограничение нужно явно описать в коде и
в README sample.

## 8. Камера

### 8.1. Компонент

```text
Camera2D : IComponent
    Vector2 ViewPort      // полный видимый размер мира, не half-size
    float Zoom            // 1 = без зума, >1 = приближение
    Vector4 ClearColor
```

Камера — нормальная сущность:

```text
entity = world.Create()
world.Add(entity, Transform2D.At(...))
world.Add(entity, new Camera2D { ViewPort = new Vector2(16, 9), Zoom = 1 })
```

`Transform2D.World` обновляется `TransformSystem` в `Transform`-фазе.
`Camera2D` не является сервисом и не кладётся в ресурсы. Если камер несколько,
берётся первая через `Query<Transform2D, Camera2D>().EntityAt(0)`; для sample
этого достаточно.

`Transform2D.Scale` камеры держать `(1, 1)`, а зум задавать через `Zoom`. Иначе
два источника масштаба будут конфликтовать.

### 8.2. Формулы

Обозначения:

```text
C    = cameraTransform.World          // row-vector, local -> world
Cinv = inverse(C)
V    = camera.ViewPort                // полный видимый размер мира
Z    = camera.Zoom
F    = framebufferSize                // HiDPI pixels
```

Мир → HiDPI:

```text
v = world * Cinv
v = v * Z
n = v / (V * 0.5)
p = (n * 0.5 + 0.5) * F
p.Y = F.Y - p.Y
```

HiDPI → мир:

```text
n = p / F
n.Y = 1 - n.Y
v = (n * 2 - 1) * (V * 0.5) / Z
world = v * C
```

Матрица для GPU:

```text
worldToNdc = Cinv * scale(2 * Z / V.X, 2 * Z / V.Y)
uViewProjection = ToMatrix4(worldToNdc)
```

В GL NDC Y направлен вверх, поэтому в матрице Y не переворачивается.
Переворот Y делается только в HiDPI-преобразовании, где экранные координаты
идут сверху вниз.

### 8.3. Центрированный зум

Ключевой момент: `world * Cinv` переводит позицию камеры в ноль **до** масштабирования.
Поэтому `Zoom` масштабирует вокруг позиции камеры, а не вокруг левого верхнего
угла. Если сначала делить на `ViewPort`, а потом вычитать позицию, зум уедет в
угол.

### 8.4. Aspect

`ViewPort` — полный размер, а не half-size. Его aspect должен совпадать с
framebuffer aspect, иначе круги растянутся.

Политика sample:

- `ViewPort.Y` считается опорным;
- при изменении размера окна `ViewPort.X = ViewPort.Y * F.X / F.Y`;
- helper `Camera2D.FitTo(framebufferSize)` или аналогичный обновляет компонент.

### 8.5. `CameraView`

`CameraView` — readonly struct, не ECS-система и не сервис. Собирается из
`in Transform2D`, `in Camera2D` и `Vector2 framebufferSize`.

Методы:

```text
Vector2 WorldToHiDpi(Vector2 world)
Vector2 HiDpiToWorld(Vector2 hiDpi)
Matrix4x4 ViewProjection
```

`MouseDelta` конвертируется через два вызова `HiDpiToWorld` и вычитание, чтобы
не думать отдельно о rotation/scale камеры.

### 8.6. Один кадр задержки

`TransformSystem` обновляет камеру в `Transform`-фазе, а `DemoControlSystem`
работает в `Update`, то есть видит камеру с прошлого кадра. Для статичной камеры
это незаметно. Если камера начнёт двигаться, появится задержка в один кадр;
это осознанное ограничение sample. Позже можно добавить `CameraSystem`,
которая обновляет камеру до игровых систем, или отдельную render-фазу.

## 9. Ввод

Логику берём из `legacy/Input` и адаптируем под Silk.NET.

### 9.1. `IInputService`

```text
void Poll(float dt)

bool IsKeyDown(Key key)
bool WasKeyPressed(Key key)
bool WasKeyReleased(Key key)

bool IsMouseDown(MouseButton button)
bool WasMousePressed(MouseButton button)
bool WasMouseReleased(MouseButton button)

Vector2 MousePosition     // HiDPI: framebuffer pixels
Vector2 MouseDelta        // HiDPI: framebuffer pixels

bool IsActionInactive(string action)
bool IsActionActive(string action)
bool IsActionJustPressed(string action)
bool IsActionJustReleased(string action)

float GetAxis(string axis)
```

`InputService` дополнительно:

```text
void BindAction(string action, Key key)
void AddAxis(string name, AxisSettings settings)
```

### 9.2. HiDPI

`MousePosition` и `MouseDelta` остаются в HiDPI-пикселях framebuffer:
в сервисе нет преобразования в мир и нет знания о камере.

Silk отдаёт позицию мыши в координатах окна. Сервис переводит её в framebuffer
пиксели:

```text
scale = framebufferSize / windowSize
MousePosition = rawMousePosition * scale
MouseDelta = MousePosition - previousMousePosition
```

На первом `Poll` delta равна нулю: previous инициализируется текущей позицией.
Если окно свёрнуто и размеры нулевые, scale не применяется.

### 9.3. Семантика действий

Сохранить legacy-поведение:

- `IsActionActive` — действие было активно и в прошлом, и в текущем кадре;
- `IsActionJustPressed` — активно сейчас, не было активно в прошлом;
- `IsActionJustReleased` — не активно сейчас, было активно в прошлом;
- `IsActionInactive` — ни сейчас, ни в прошлом нет активных клавиш.

`Poll(dt)`:

1. Скопировать текущие состояния в previous.
2. Считать новые состояния клавиш и кнопок мыши.
3. Обновить позицию и delta мыши.
4. Обновить оси.

Для чтения использовать кэшированные `Enum.GetValues<Key>()` и
`Enum.GetValues<MouseButton>()`. Это детерминированнее, чем `SupportedKeys`, и
не зависит от backend.

### 9.4. Оси

Адаптировать `legacy/Input/InputAxis/Axis.cs`:

- `Value`, `Sensitivity`, `Gravity`;
- `MoveToward` реализовать локально;
- `Axis.Update(IInputService, float dt)`.

### 9.5. Система

`InputSystem` в `SystemPhase.PreUpdate`:

```text
input.Poll(dt)
```

События окна качает хост через `PumpEvents()`. `InputSystem` не трогает окно и
не делает `DoEvents`.

### 9.6. Действия и оси sample

- `move.left` = `A`;
- `move.right` = `D`;
- `move.up` = `W`;
- `move.down` = `S`;
- `reset` = `Space`;
- `move.horizontal` = right/left;
- `move.vertical` = up/down.

`InputService` не implements `IDisposable`: input context принадлежит
`SilkGraphicsService`.

## 10. Модуль

`SilkTestModule : IEngineModule`.

`ConfigureComponents`:

- регистрирует `Circle`.

`ConfigureServices`:

- создаёт `SilkGraphicsService`, `InputService`, `CircleRenderer`;
- регистрирует их в `EngineServiceContainer`;
- настраивает действия и оси.

`OnEngineCreated`:

- инициализирует `SilkGraphicsService`;
- инициализирует `CircleRenderer` (шейдеры, quad, instance buffer);
- подключает `InputService` к input context.

`ConfigureWorld`:

- `world.SetResource<IInputService>(_input)`;
- `world.AddSystem(SystemPhase.PreUpdate, new InputSystem(_input))`;
- `world.AddSystem(SystemPhase.Transform, new CircleRenderSystem(_graphics, _renderer))`.

`OnEngineDisposing`:

- `_renderer.Dispose()`;
- отключить input;
- `_graphics.Dispose()`.

Порядок модулей в `Program.cs`:

```text
TransformModule -> SilkTestModule
```

Иначе `CircleRenderSystem` окажется раньше `TransformSystem` в `Transform`-фазе.

### Владение ресурсами

`World.Dispose()` освобождает ресурсы, реализующие `IDisposable`. Поэтому:

- `IInputService` не `IDisposable` и может лежать в ресурсах;
- `CircleRenderer` — `IDisposable`, но **не** кладётся в ресурсы мира;
- `CircleRenderSystem` получает `CircleRenderer` через конструктор;
- `SilkGraphicsService` живёт в `Engine.Services` и освобождается модулем.

Так переход между сценами не уничтожит общий рендерер и input context.

## 11. Демо-сцена

`DemoScene` создаёт:

- камеру: `Transform2D.At(0)` + `Camera2D { ViewPort = (16, 9), Zoom = 1 }`;
- игрока: `Transform2D.At(0)` + `Circle { Radius = 0.5 }`;
- курсор: `Transform2D.At(0)` + `Circle { Radius = 0.2 }`;
- сетку из 100 статических кругов для визуальной проверки instancing;
- `DemoControlSystem` в `SystemPhase.Update`.

`DemoControlSystem`:

- получает `IInputService` из ресурсов мира;
- находит камеру через `Query<Transform2D, Camera2D>`;
- строит `CameraView`;
- двигает игрока осями `move.horizontal` / `move.vertical`;
- ставит курсор в `HiDpiToWorld(input.MousePosition)`;
- по `IsActionJustPressed("reset")` возвращает игрока в центр.

Это проверяет одновременно: polling, edge detection, оси, HiDPI ↔ мир, камеру,
ECS-компонент круга и instanced draw.

## 12. Порядок работ

1. Добавить lifecycle-хуки в Core, тесты, обновить `docs/plans.md`.
2. Создать `samples/XEngine.Interactive.csproj`, добавить в `XEngine.slnx`.
3. Реализовать `SilkTestOptions`, `SilkGraphicsService`, `Program.cs`.
4. Перенести/адаптировать `Shader` и `UnitQuad` с учётом Silk.NET API.
5. Реализовать `CircleRenderer` с dynamic instance VBO и `DrawElementsInstanced`.
6. Добавить `Camera2D`, `CameraView`, формулы и `FitTo`.
7. Перенести ввод из `legacy/Input`, добавить `InputSystem`.
8. Реализовать `Circle`, `CircleRenderSystem`, guard `FrameCount`.
9. Собрать `DemoScene` и `DemoControlSystem`.
10. Добавить README sample и обновить корневой README.
11. Проверить build/test/run.

## 13. Проверка

Автоматически:

- `dotnet build XEngine.slnx`;
- `dotnet test XEngine.slnx` — lifecycle-хуки и остальные Core-тесты, без GPU.

Вручную, `dotnet run --project samples/XEngine.Interactive`:

- окно открывается и закрывается без зависаний;
- фон очищается цветом камеры;
- сетка кругов рисуется одним instanced draw;
- курсорный круг следует за мышью;
- WASD двигает игрока;
- `Space` возвращает игрока в центр;
- при resize круги не растягиваются, aspect сохраняется;
- при сворачивании окна нет падения и деления на ноль;
- после закрытия окна процесс завершается, ресурсы освобождаются.

## 14. Ограничения и риски

- **Module-only невозможен.** Главный цикл остаётся в `Program.cs`; `OnFrame` не
  добавляется, потому что кадровая работа — это `ISystem`.
- **Рендер в `Transform`-фазе — семантический компромисс.** После неё трансформы
  чистые, но фаза называется не по рендеру. Позже можно добавить отдельную
  `Render`-фазу или переименовать `Transform`.
- **Порядок систем.** `CircleRenderSystem` добавляется в `ConfigureWorld`, то есть
  раньше систем, добавленных из `ISceneBuilder`. Если сцена добавит свой
  `Transform`-систем, он выполнится после рендера. `SilkTestModule` должен идти
  последним; при необходимости позже вводятся приоритеты систем.
- **`UpdateTransforms()` при сборке сцены** запускает `Transform`-системы; guard
  `FrameCount > 0` обязателен.
- **Камера в `Update` видит прошлый кадр.** Для статичной камеры это неважно;
  для движущейся нужна отдельная `CameraSystem` или render-фаза.
- **`ViewPort` и aspect.** Если не обновлять `ViewPort.X` при resize, круги
  растянутся. Политика — опорный `ViewPort.Y`.
- **Два источника зума.** `Transform2D.Scale` камеры и `Camera2D.Zoom` нельзя
  смешивать; в sample используется только `Zoom`, scale камеры равен 1.
- **HiDPI.** Мышь и framebuffer живут в физических пикселях; не забыть scale при
  переходе от координат окна.
- **Ресурсы мира освобождаются.** `IDisposable`-рендерер нельзя класть в
  `SetResource`; он передаётся в систему напрямую.
- **Один поток и одно окно.** Мультиоконность и фоновый рендер не поддерживаются.
- **Нет автоматических GPU-тестов.** Sample собирается в CI, но проверяется
  вручную.
- **Ошибки GL fail-fast.** Ошибки компиляции/линковки шейдеров и создания окна
  должны падать с понятным сообщением, а не молча рисовать пустоту.
- **Instancing capacity.** Динамический VBO растёт удвоением; верхний предел не
  задаётся, но при тысячах кругов нужно следить за переаллокациями.

## 15. Открытые вопросы

- Какую именно политику `ViewPort` зафиксировать: фиксированная высота мира
  (например, 9 или 10 единиц) или фиксированная ширина.
- Нужен ли `Camera2D` как компонент в релизной рендер-подсистеме или позже
  станет сервисом/ресурсом.
- Нужна ли отдельная `CameraSystem` для обновления камеры до игровых систем.
- Нужен ли scroll wheel в `IInputService`; в первой версии не обязателен.
- Нужен ли отдельный `Render`-фаза вместо использования `Transform`.
- Стоит ли вынести GLSL в файлы после стабилизации sample.
