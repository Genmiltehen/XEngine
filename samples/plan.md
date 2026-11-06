# План реализации `samples/XEngine.Interactive`

> Статус: реализовано. Документ оставлен как история решений; актуальное описание —
> `samples/XEngine.Interactive/README.md`.

Рабочий план временного интерактивного harness. Проект нужен только для ручных
тестов движка: окно, ввод, камера и отрисовка кругов. В продуктовую подсистему
рендера он не превращается.

Связанные документы:

- `docs/TODO.md` — текущие планы всего проекта.
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

Проект опирается на уже существующие lifecycle-хуки `IEngineModule`:

- `OnEngineCreated(Engine engine)` — создание окна, GL и input context;
- `OnEngineDisposing(Engine engine)` — освобождение ресурсов модуля.

Хуки уже реализованы в Core и покрыты `EngineBuilderTests`, поэтому отдельного
шага по доработке Core нет. Они позволяют модулю владеть платформенными
ресурсами и при этом не добавлять `OnFrame`.

`Core` не получает ссылку на Silk.NET. Все пакеты Silk.NET подключаются только
в `samples/XEngine.Interactive`.

## 3. Структура проекта

```text
samples/XEngine.Interactive/
  XEngine.Interactive.csproj
  Program.cs
  Clock.cs
  README.md
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
    ScrollAccumulator.cs
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
tests/XEngine.Interactive.Tests/
  XEngine.Interactive.Tests.csproj
  CameraViewTests.cs
  AxisTests.cs
  ScrollAccumulatorTests.cs
```

Тестовый проект ссылается на sample и xUnit; GPU ему не нужен. Тестируются
только `CameraView` (чистая математика) и `Axis` (через ручной стаб
`IInputService`). `InputService` с реальными устройствами Silk не тестируется.

Оба проекта добавляются в `XEngine.slnx`: sample — в `/samples/`, тесты — в
`/tests/`. `dotnet build` и `dotnet test` собирают их, но не запускают sample,
поэтому GPU для проверок не нужен.

Пакеты: `Silk.NET.Windowing.Glfw`, `Silk.NET.Input.Glfw`, `Silk.NET.OpenGL`
версии `2.23.0` (или совместимой `2.2x`). В `csproj` включить
`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — он нужен для
`DrawElementsInstanced` с `void*`.

## 4. Хост и порядок кадра

Главный цикл остаётся в `Program.cs`:

```text
using Engine engine = EngineBuilder
    .AddModule(new TransformModule())
    .AddModule(new SilkTestModule(options))
    .Build();

SilkGraphicsService graphics = engine.Services.Get<SilkGraphicsService>();
var clock = new Clock();

engine.Scenes.Replace(new DemoScene(), "demo");

while (!graphics.IsClosing)
{
    graphics.PumpEvents();
    if (graphics.IsClosing) break;
    float dt = MathF.Min(clock.Delta, 0.1f);
    engine.Update(dt);
}
```

`Clock` — маленькая обёртка над `Stopwatch`: хранит время предыдущего кадра и
отдаёт `Delta`. `graphics` берётся из `Engine.Services`, потому что
`SilkTestModule` регистрирует `SilkGraphicsService` в `ConfigureServices`.
`using` гарантирует `Engine.Dispose()` после выхода из цикла.

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
- `WindowSize`;
- `IsClosing`;
- `PumpEvents()`;
- `SwapBuffers()`;
- `Dispose()`.

`WindowSize` нужен `InputService` для перевода координат мыши из окна в
framebuffer-пиксели. `SilkGraphicsService` регистрируется в
`EngineServiceContainer`; контейнер вызовет `Dispose()` после сцен и модулей.
`Dispose()` должен быть идемпотентным и null-safe: окно могло не успеть
создаться.

Окно создаётся только в `OnEngineCreated`, не в `ConfigureServices`.

Параметры окна:

- `GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3))`;
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
CircleRenderer.Dispose()       // модуль, пока GL-контекст жив
InputService.Detach()          // модуль
SilkGraphicsService.Dispose()  // EngineServiceContainer
  input.Dispose()
  gl.Dispose()
  window.Dispose()
```

`OnEngineDisposing` диспозит только `CircleRenderer` и отвязывает input.
`SilkGraphicsService` не диспозится там повторно: это делает контейнер.

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
| `GL.DrawElementsInstanced` | `gl.DrawElementsInstanced` с `void*`/`ref` |
| `GL.UniformMatrix4(loc, false, ref m)` | `gl.UniformMatrix4(loc, false, ReadOnlySpan<float>)` |

Правила:

- ошибки компиляции и линковки не писать в `Debug.WriteLine`, а бросать исключение
  с текстом лога;
- не делать глобальный `GLProvider` со статическим `GL`;
- шейдеры хранить inline в `CircleRenderer` (raw string), без файлов и ассетов;
- матрицы `System.Numerics` row-vector. В GLSL используется
  `uViewProjection * vec4(...)`, поэтому матрица загружается с `transpose = false`
  и span’ом из `Matrix4x4` в row-major порядке. GL сам трактует входные данные как
  column-major, то есть фактически получает транспонированную матрицу — именно то,
  что нужно для row-vector. `transpose = true` здесь дал бы неверный результат;
- `DrawElementsInstanced` вызывается в `unsafe`-блоке с `(void*)0`; в `csproj`
  включён `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`;
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

Итого 7 float = 28 байт на instance. `CircleInstance` объявляется как
`[StructLayout(LayoutKind.Sequential)]` с полями `Vector2 Center`,
`float Radius`, `Vector4 Color`; offsets 0/8/12, stride 28.

Инстансный VBO динамический. При росте количества кругов capacity удваивается,
данные загружаются через `BufferData`/`BufferSubData`, как в `LineBatcher`.

Атрибуты:

| location | данные | offset | размер | divisor |
| --- | --- | --- | --- | --- |
| 0 | `aPos` (vec2, quad) | 0 | 8 | 0 |
| 1 | `aCenter` (vec2) | 0 | 8 | 1 |
| 2 | `aRadius` (float) | 8 | 4 | 1 |
| 3 | `aColor` (vec4) | 12 | 16 | 1 |

`VertexAttribDivisor` выставляется для location 1–3. Stride для инстансных
атрибутов — 28.

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
3. Ищет камеру: если `Query<Transform2D, Camera2D>().Count() == 0`, кадр
   пропускается.
4. Вызывает `Camera2D.FitTo(framebufferSize)` (идемпотентно) и строит
   `CameraView`; если `ViewPort` нулевой, кадр пропускается, а при вырожденной
   матрице `CameraView` использует fallback Identity.
5. Собирает instances из `Query<Transform2D, Circle>`:
   - center = `Transform2D.World.Translation`;
   - radius = `Circle.Radius * max(abs(World scale))`;
   - color = `Circle.Color`.
6. Делает `glViewport`, `glClear`, настройку blend.
7. `shader.Use()`, загрузка `uViewProjection`.
8. Загрузка instance VBO.
9. В `unsafe`-блоке: `gl.DrawElementsInstanced(Triangles, 6, UnsignedInt,
   (void*)0, count)`.
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
в README sample. Guard корректен, пока `UpdateTransforms()` вызывается только из
`SceneManager.BuildScene`. Если появится внешний вызов в обычном кадре, рендер
сработает повторно; тогда понадобится явный флаг `IsBuildingScene`.

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
world.Add(entity, Transform2D.At(Vector2.Zero))
world.Add(entity, new Camera2D { ViewPort = new Vector2(16, 9), Zoom = 1 })
```

`Camera2D` регистрируется в `SilkTestModule.ConfigureComponents` вместе с
`Circle` (например, `"sample.Camera2D"`). Без регистрации замороженный реестр
бросит исключение при `world.Add` и `Query<Transform2D, Camera2D>`.

`Transform2D.World` обновляется `TransformSystem` в `Transform`-фазе.
`Camera2D` не является сервисом и не кладётся в ресурсы. Если камер несколько,
берётся первая через `Query<Transform2D, Camera2D>().EntityAt(0)`; для sample
этого достаточно. Перед `EntityAt(0)` проверяется `Count() > 0`.

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

`ToMatrix4(Matrix3x2 m)` сохраняет row-vector соглашение:

```text
M11 = m.M11   M12 = m.M12   M13 = 0   M14 = 0
M21 = m.M21   M22 = m.M22   M23 = 0   M24 = 0
M31 = 0       M32 = 0       M33 = 1   M34 = 0
M41 = m.M31   M42 = m.M32   M43 = 0   M44 = 1
```

При загрузке в GL используется `transpose = false`; GLSL-код умножает
`uViewProjection * vec4(...)`, поэтому неявный transpose на стороне GL даёт
корректное преобразование row-vector матрицы.

В GL NDC Y направлен вверх, поэтому в матрице Y не переворачивается.
Переворот Y делается только в HiDPI-преобразовании, где экранные координаты
идут сверху вниз.

### 8.3. Центрированный зум

Ключевой момент: `world * Cinv` переводит позицию камеры в ноль **до** масштабирования.
Поэтому `Zoom` масштабирует вокруг позиции камеры, а не вокруг левого верхнего
угла. Если сначала делить на `ViewPort`, а потом вычитать позицию, зум уедет в
угол.

В sample `DemoControlSystem` меняет `Zoom` колесом мыши:
`Zoom *= 1.1^ScrollDelta.Y`, диапазон `0.25…8`.

### 8.4. Aspect

`ViewPort` — полный размер, а не half-size. Его aspect должен совпадать с
framebuffer aspect, иначе круги растянутся.

Политика sample:

- `ViewPort.Y` считается опорным;
- при изменении размера окна `ViewPort.X = ViewPort.Y * F.X / F.Y`;
- helper `Camera2D.FitTo(framebufferSize)` обновляет компонент;
- `FitTo` вызывают `DemoControlSystem` в начале `Update` и `CircleRenderSystem`
  перед построением `CameraView` (идемпотентно);
- если `FramebufferSize` или `ViewPort` нулевые, `FitTo`/`CameraView` не
  строятся, кадр пропускается.

`DemoControlSystem` получает `FramebufferSize` через `SilkGraphicsService`,
переданный из `DemoScene` (см. §11).

### 8.5. `CameraView`

`CameraView` — readonly struct, не ECS-система и не сервис. Собирается из
`in Transform2D`, `in Camera2D` и `Vector2 framebufferSize`. Конструктор требует
ненулевые `framebufferSize` и `ViewPort`; при вырожденной камере
`Matrix3x2.Invert` даёт fallback Identity.

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
Vector2 ScrollDelta       // wheel ticks since previous Poll

bool IsActionInactive(string action)
bool IsActionActive(string action)
bool IsActionJustPressed(string action)
bool IsActionJustReleased(string action)

float GetAxis(string axis)
```

`InputService` дополнительно:

```text
void Attach(IInputContext input)
void Detach()
void BindAction(string action, Key key)
void AddAxis(string name, AxisSettings settings)
```

`Poll` до `Attach` — no-op. `Attach`/`Detach` вызываются модулем в
`OnEngineCreated`/`OnEngineDisposing`. `InputService` не `IDisposable`.

### 9.2. HiDPI

`MousePosition` и `MouseDelta` остаются в HiDPI-пикселях framebuffer:
в сервисе нет преобразования в мир и нет знания о камере.

`ScrollDelta` — не координата, а накопленное колесо в тиках с прошлого `Poll`.
Оно не масштабируется через HiDPI. `InputService` подписывается на
`IMouse.Scroll` и складывает `ScrollWheel.X/Y` в `ScrollAccumulator`; `Poll`
забирает накопленное значение и обнуляет его.

Silk отдаёт позицию мыши через `IInputContext.Mice[0].Position` в координатах
окна. `InputService` получает `FramebufferSize` и `WindowSize` из
`SilkGraphicsService` и переводит позицию в framebuffer-пиксели:

```text
scale = framebufferSize / windowSize
MousePosition = rawMousePosition * scale
MouseDelta = MousePosition - previousMousePosition
```

Клавиши читаются через `IInputContext.Keyboards[0].IsKeyPressed(Key)`, кнопки
мыши — через `Mice[0].IsButtonPressed(MouseButton)`. Если список устройств пуст,
состояния остаются нулевыми.

На первом `Poll` delta равна нулю, а previous-состояния клавиш и кнопок
инициализируются текущими, чтобы зажатая при старте клавиша не дала
`JustPressed`. Если окно свёрнуто и размеры нулевые, scale не применяется.

### 9.3. Семантика действий

Сохранить legacy-поведение:

- `IsActionActive` — действие было активно и в прошлом, и в текущем кадре;
- `IsActionJustPressed` — активно сейчас, не было активно в прошлом;
- `IsActionJustReleased` — не активно сейчас, было активно в прошлом;
- `IsActionInactive` — ни сейчас, ни в прошлом нет активных клавиш.

Если действие не привязано, `IsActionActive`/`JustPressed`/`JustReleased`
возвращают `false`, `IsActionInactive` — `false` (как в legacy).

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

`Axis.Update` использует `IsActionActive`, поэтому первый кадр нажатия ещё не
даёт вклад; это legacy-поведение sample.

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

## 10. Модуль

`SilkTestModule : IEngineModule`.

`ConfigureComponents`:

- регистрирует `Circle` (`"sample.Circle"`);
- регистрирует `Camera2D` (`"sample.Camera2D"`).

Регистрация обязательна: `DemoScene` добавляет оба компонента, а реестр в
production-пути заморожен.

`ConfigureServices`:

- создаёт `SilkGraphicsService` и `InputService` (input получает ссылку на
  graphics для `FramebufferSize`/`WindowSize`);
- регистрирует оба в `EngineServiceContainer` (только они — сервисы);
- создаёт `CircleRenderer`, но **не** регистрирует его в контейнере: это поле
  модуля, потому что рендерер требует живой GL-контекст;
- настраивает действия и оси.

`OnEngineCreated`:

- инициализирует `SilkGraphicsService` (окно, GL, input context);
- инициализирует `CircleRenderer` (шейдеры, quad, instance buffer);
- `_input.Attach(_graphics.Input)`.

`ConfigureWorld`:

- `world.SetResource<IInputService>(_input)`;
- `world.AddSystem(SystemPhase.PreUpdate, new InputSystem(_input))`;
- `world.AddSystem(SystemPhase.Transform, new CircleRenderSystem(_graphics, _renderer))`.

`OnEngineDisposing`:

- `_renderer.Dispose()` — пока GL-контекст ещё жив;
- `_input.Detach()`;
- `_graphics` здесь не диспозится: это сделает `EngineServiceContainer`.

Порядок модулей в `Program.cs`:

```text
TransformModule -> SilkTestModule
```

Иначе `CircleRenderSystem` окажется раньше `TransformSystem` в `Transform`-фазе.

### Владение ресурсами

`World.Dispose()` освобождает ресурсы, реализующие `IDisposable`. Поэтому:

- `IInputService` не `IDisposable` и может лежать в ресурсах;
- `CircleRenderer` — `IDisposable`, но **не** кладётся ни в ресурсы мира, ни в
  `EngineServiceContainer`; это поле `SilkTestModule`, и модуль освобождает его
  в `OnEngineDisposing`;
- `CircleRenderSystem` получает `CircleRenderer` через конструктор;
- `SilkGraphicsService` живёт в `Engine.Services`; его освобождает
  `EngineServiceContainer` после сцен и модулей;
- `InputService` не `IDisposable`, input context принадлежит
  `SilkGraphicsService`.

Так переход между сценами не уничтожит общий рендерер и input context, а
двойного `Dispose` не будет.

## 11. Демо-сцена

`DemoScene` создаёт:

- камеру: `Transform2D.At(Vector2.Zero)` + `Camera2D { ViewPort = (16, 9), Zoom = 1 }`;
- игрока: `Transform2D.At(Vector2.Zero)` + `Circle { Radius = 0.5 }`;
- курсор: `Transform2D.At(Vector2.Zero)` + `Circle { Radius = 0.2 }`;
- сетку 10×10 из 100 статических кругов для визуальной проверки instancing;
- `DemoControlSystem` в `SystemPhase.Update`.

`DemoScene` получает `SilkGraphicsService` через `context.Services.Get<SilkGraphicsService>()`
в `SceneBuildContext` и передаёт его вместе с `EntityId player, cursor` в
конструктор `DemoControlSystem`. Иначе система не отличит игрока и курсор от
кругов сетки и не узнает `FramebufferSize`.

`DemoControlSystem`:

- получает `IInputService` из ресурсов мира;
- берёт `FramebufferSize` из `SilkGraphicsService`;
- находит камеру через `Query<Transform2D, Camera2D>`, проверяет `Count() > 0`;
- вызывает `Camera2D.FitTo(framebufferSize)`, применяет `ScrollDelta.Y` к
  `Camera2D.Zoom` (0.25…8) и строит `CameraView`;
- двигает игрока осями `move.horizontal` / `move.vertical` со скоростью,
  например, 6 единиц в секунду (`position += direction * speed * dt`);
- ставит курсор в `HiDpiToWorld(input.MousePosition)`;
- по `IsActionJustPressed("reset")` возвращает игрока в `Vector2.Zero`.

Это проверяет одновременно: polling, edge detection, оси, HiDPI ↔ мир, камеру,
ECS-компонент круга и instanced draw.

## 12. Порядок работ

1. Создать `samples/XEngine.Interactive.csproj` с пакетами Silk.NET, `AllowUnsafeBlocks` и добавить в `XEngine.slnx`.
2. Реализовать `Clock`, `SilkTestOptions`, `SilkGraphicsService`, `Program.cs`.
3. Перенести/адаптировать `Shader` и `UnitQuad` с учётом Silk.NET API.
4. Реализовать `CircleRenderer` с dynamic instance VBO и `DrawElementsInstanced`.
5. Добавить `Camera2D`, `CameraView`, формулы и `FitTo`.
6. Перенести ввод из `legacy/Input`, добавить `Attach`/`Detach` и `InputSystem`.
7. Реализовать `Circle`, `CircleRenderSystem`, guard `FrameCount`.
8. Собрать `DemoScene` и `DemoControlSystem`.
9. Добавить `tests/XEngine.Interactive.Tests` с тестами `CameraView`, `Axis` и `ScrollAccumulator`.
10. Добавить README sample и обновить корневой README, `AGENTS.md`, `docs/TODO.md`.
11. Проверить build/test/run.

## 13. Проверка

Автоматически:

- `dotnet build XEngine.slnx`;
- `dotnet test XEngine.slnx` — Core-тесты и тесты чистой логики sample, без GPU.

Вручную, `dotnet run --project samples/XEngine.Interactive`:

- окно открывается и закрывается без зависаний;
- фон очищается цветом камеры;
- сетка кругов рисуется одним instanced draw;
- курсорный круг следует за мышью;
- WASD двигает игрока;
- `Space` возвращает игрока в центр;
- колесо мыши меняет зум камеры в диапазоне 0.25…8;
- при resize круги не растягиваются, aspect сохраняется;
- при сворачивании окна нет падения и деления на ноль;
- если камеру удалить из сцены, кадр пропускается без исключения;
- после закрытия окна процесс завершается, ресурсы освобождаются.

## 14. Ограничения и риски

- **Module-only невозможен.** Главный цикл остаётся в `Program.cs`; `OnFrame` не
  добавляется, потому что кадровая работа — это `ISystem`.
- **Рендер в `Transform`-фазе — семантический компромисс.** После неё трансформы
  чистые, но фаза называется не по рендеру. Позже можно добавить отдельную
  `Render`-фазу или переименовать `Transform`.
- **Порядок систем.** `CircleRenderSystem` добавляется в `ConfigureWorld`, то есть
  раньше систем, добавленных из `ISceneBuilder`. Если сцена добавит свою
  `Transform`-систему, она выполнится после рендера. `SilkTestModule` должен идти
  последним; при необходимости позже вводятся приоритеты систем.
- **`UpdateTransforms()` при сборке сцены** запускает `Transform`-системы; guard
  `FrameCount > 0` обязателен. Внешний вызов `UpdateTransforms()` в обычном кадре
  приведёт к повторному рендеру; тогда нужен явный флаг сборки сцены.
- **`DrawElementsInstanced` требует unsafe.** Перегрузок с `int offset` нет;
  используется `void*`-вариант, поэтому в sample включён `AllowUnsafeBlocks`.
- **`Camera2D` должен быть зарегистрирован.** Иначе замороженный реестр бросит
  исключение при построении сцены, а не при первом рендере.
- **Камера в `Update` видит прошлый кадр.** Для статичной камеры это неважно;
  для движущейся нужна отдельная `CameraSystem` или render-фаза.
- **`ViewPort` и aspect.** Если не обновлять `ViewPort.X` при resize, круги
  растянутся. Политика — опорный `ViewPort.Y`; `FitTo` вызывают и контроллер,
  и рендер.
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

- Нужен ли `Camera2D` как компонент в релизной рендер-подсистеме или позже
  станет сервисом/ресурсом.
- Нужна ли отдельная `CameraSystem` для обновления камеры до игровых систем.
- Нужна ли отдельная `Render`-фаза вместо использования `Transform`.
- Стоит ли вынести GLSL в файлы после стабилизации sample.
- Удалить `samples/plan.md` после реализации или оставить как историю.
