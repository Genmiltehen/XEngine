# Планы

Рабочий документ. Выполненные пункты удаляются. Статусы здесь не ведутся: если нужна отдельная отметка о ходе работ, она будет вынесена в другой файл.

## 1. Контекст

- Репозиторий — только движок. GunFight, его история и игровая логика не учитываются.
- Текущий этап — headless-ядро: ECS, иерархия, трансформы, системы, сцены, тесты.
- Рендер, физика, ввод, конфигурация и игровые подсистемы в текущий этап не входят.
- Git-историю можно реинициализировать: старый проект сохранён в другом репозитории.

## 2. Зафиксированные решения

- ECS — archetype-based, компоненты только struct.
- Публичный стиль обхода запросов — `world.Query<...>().ForEach(callback)`, без `foreach` по результатам запроса.
- Свою математику не пишем. Используем `System.Numerics`.
- `MathNet.Numerics` на текущем этапе не подключаем: это библиотека линейной алгебры общего назначения, для `Vector2`/`Matrix3x2` она не даёт выигрыша и добавляет тяжёлую зависимость. Вернуться к вопросу можно, если появятся задачи, где `System.Numerics` недостаточно.
- Принята row-vector-конвенция `System.Numerics`: `point' = point * matrix`, `World = Local * Parent.World`, `Local = Scale * Rotation * Translation`. Конвенция фиксируется тестами.
- `Node` — отдельный обязательный компонент, владеющий иерархией. `Transform2D` — опциональный компонент с локальными данными и вычисляемой мировой матрицей.
- Сцены входят в `Core`: переходы, стек сцен, инициализация через `ISceneBuilder`.
- Ядро не знает о рендере и физике. Для них позже будут отдельные сборки, компоненты и системы.
- Рендер в будущем забирает из мира снимок данных и строит список команд. Ядро не формирует GPU-команды и не ссылается на графические типы.

## 3. Структура решения

```
src/XEngine.Core/
  Ecs/            EntityId, World, Archetype, ComponentType, Column<T>, Query, CommandBuffer
  Hierarchy/      Node (обслуживается World)
  Transforms/     Transform2D, TransformSystem
  Scenes/         ISceneBuilder, Scene, SceneManager, SceneBuildContext
  Systems/        ISystem, SystemPhase, SystemScheduler, WorldConfigurator
  Services/       EngineServices
  Modules/        IEngineModule, CoreModule
  Hosting/        Engine, EngineBuilder
  Time/           GameTime
tests/XEngine.Core.Tests/
benchmarks/XEngine.Core.Benchmarks/
docs/plans.md
README.md
```

- `XEngine.Core` не имеет внешних NuGet-зависимостей.
- Тесты — xUnit.
- Бенчмарки — BenchmarkDotNet, отдельный проект.
- Рендер и физика появятся отдельными проектами позже; в `Core` для них только точки расширения (`World.Resources`, системы, компоненты).

## 4. ECS

### 4.1. EntityId

```csharp
public readonly record struct EntityId(int Index, int Version)
{
    public static readonly EntityId None = new(-1, 0);
    public bool IsNone => Index < 0;
}
```

- Индекс + версия защищают от использования старых хэндлов после уничтожения.
- Индексы переиспользуются, версия инкрементируется при уничтожении.
- `Node` хранит `EntityId`, поэтому перемещение сущности между архетипами не ломает иерархию.

### 4.2. Архетипы и колонки

- Архетип определяется уникальным набором типов компонентов.
- Внутри архетипа: `EntityId[] Entities` и по одной плотной колонке `Column<T>` на компонент.
- `Column<T>` — `T[]` + `Count`, рост по doubling.
- `ComponentMask` — битсет на 256 типов (4 × `ulong`), хэш кэшируется.
- `ComponentType` — негенеричный дескриптор: `Id`, имя, CLR-тип и фабрика колонки. `Id` выдаёт `ComponentRegistry` при регистрации, а не generic-static.
- `Add<T>`/`Remove<T>` переводят сущность в целевой архетип: общие компоненты копируются, строка удаляется swap-back-ом, `Row` переехавшей сущности обновляется.
- На первом этапе одна колонка на архетип. API запросов строится через чанки, чтобы позже можно было нарезать колонки на чанки без изменения пользовательского кода.
- `Node` обязателен: `World.Create()` создаёт сущность сразу в архетипе с `Node` и инициализирует связи как `EntityId.None`, а флаги — как `NodeFlags.ActiveSelf | NodeFlags.ActiveInHierarchy`. `default(Node)` как рабочее значение не используется.

### 4.3. Компоненты

```csharp
public interface IComponent { }

public struct Velocity : IComponent
{
    public Vector2 Value;
}
```

- Компоненты не знают о владельце, сцене и мире.
- Логика размещается в системах, а не в компонентах.
- Ссылочные ресурсы и подсистемы хранятся в `World.Resources`/`EngineServices`, а не в компонентах.

### 4.4. Запросы и ForEach

Публичный API — только `ForEach`:

```csharp
world.Query<Transform2D, Velocity>()
    .ForEach((EntityId entity, ref Transform2D transform, ref Velocity velocity) =>
    {
        transform.Position += velocity.Value * dt;
    });
```

Поддерживаются варианты:

```csharp
// без EntityId
world.Query<Velocity>().ForEach((ref Velocity velocity) => { ... });

// с сущностью
world.Query<Transform2D, Velocity>()
    .ForEach((EntityId entity, ref Transform2D t, ref Velocity v) => { ... });

// фильтры
world.Query<Transform2D, Velocity>()
    .With<Active>()
    .Without<Frozen>()
    .ForEach(...);
```

- Делегаты с `ref`-параметрами объявляются отдельно (`QueryAction`, `EntityQueryAction`).
- Внутри `ForEach` идёт обход чанков и вызов колбэка на каждую сущность.
- Порядок обхода детерминирован: порядок архетипов, затем порядок строк в чанке.
- Аллокаций на сущность нет. Делегат может аллоцироваться один раз на вызов при захвате замыкания; для горячих систем добавляется перегрузка с состоянием:
  `ForEach(in state, static (in State s, EntityId e, ref A a) => ...)`.
- Изменять структуру мира внутри `ForEach` запрещено. В Debug бросается понятная ошибка; в Release проверка отключается.
- Чанки и `Span<T>` остаются внутренней деталью. Низкоуровневый API для систем, которым нужны спаны, добавляется только при доказанной необходимости.
- Для подсчёта и тестов query предоставляет `Count()`.

### 4.5. Структурные изменения

Два уровня:

1. Прямой API для загрузки, тулинга и тестов: `Create`, `Add`, `Remove`, `Destroy`, `SetParent`.
2. `CommandBuffer` для систем: операции копятся и применяются на границе фазы через `world.Submit(cmd)`.

- Сущность, созданная в буфере, получает зарезервированный `EntityId` сразу; архетип создаётся при flush.
- `Add`/`SetParent` для такой сущности накапливаются и применяются одним переходом.
- Создание и уничтожение одной сущности в одном буфере отменяют друг друга.
- Прямые структурные изменения во время `ForEach` запрещены и проверяются в Debug.
- Внутри буфера допускается агрегировать изменения по сущности, чтобы не делать цепочку лишних переходов между архетипами.

### 4.6. Уничтожение

```csharp
world.Destroy(entity);
world.Destroy(entity, DestroyMode.Recursive);
```

- По умолчанию дети переподвешиваются к родителю.
- `DestroyMode.Recursive` уничтожает всё поддерево.
- При переподвешивании можно сохранять мировую трансформацию.
- Ссылки `Parent`/`FirstChild`/`LastChild`/`NextSibling`/`PrevSibling` очищаются.
- Повторное уничтожение — no-op в Release, assert в Debug.

### 4.7. World и ресурсы

```csharp
public sealed class World : IDisposable
{
    public World(WorldConfigurator? configurator = null);
    public World(ComponentRegistry components, WorldConfigurator? configurator = null);

    public ComponentRegistry Components { get; }
    public GameTime Time { get; }

    public EntityId Create();
    public void Destroy(EntityId entity, DestroyMode mode = DestroyMode.ReparentChildren, bool keepWorld = false);
    public void Add<T>(EntityId entity, T component) where T : struct, IComponent;
    public void Remove<T>(EntityId entity) where T : struct, IComponent;
    public ref T Get<T>(EntityId entity) where T : struct, IComponent;
    public bool TryGet<T>(EntityId entity, out T component) where T : struct, IComponent;
    public bool Has<T>(EntityId entity) where T : struct, IComponent;
    public void SetParent(EntityId child, EntityId parent, bool keepWorld = false);
    public void SetActive(EntityId entity, bool active);
    public Query<T> Query<T>() where T : struct, IComponent;
    public CommandBuffer BeginCommands();
    public void Submit(CommandBuffer commands);

    public T GetResource<T>() where T : class;
    public bool TryGetResource<T>(out T? resource) where T : class;
    public void SetResource<T>(T resource) where T : class;
    public bool RemoveResource<T>() where T : class;

    public void AddSystem(SystemPhase phase, ISystem system);
    public void Update(float dt);
    public void UpdateTransforms(float dt = 0f);
    public void Dispose();
}
```

- `World` — контейнер ECS, систем и ресурсов. Конструктор без аргумента применяет `WorldConfigurators.Default`.
- `World.Dispose` идемпотентен, освобождает системы и ресурсы, реализующие `IDisposable`.
- `SetResource` освобождает предыдущий ресурс того же типа, если он реализует `IDisposable`; `RemoveResource` делает то же при удалении.
- `World.Update` обновляет `GameTime` и выполняет фазы систем. `UpdateTransforms` выполняет только трансформ-фазу и не двигает время — им пользуются сцены при построении.
- Глобальные сервисы (конфиг, логгер, ассеты, будущий рендер) живут в `EngineServices` и передаются через контекст сцены.

### 4.8. Реестр компонентов и модули

- `ComponentRegistry` — один на движок, общий для всех миров и сцен. Владеет `Id`, именем, CLR-типом и фабрикой колонки.
- `ComponentType<T>` удалён: generic-синглтон больше не выдаёт `Id`. `World.Add<T>/Get<T>/Has<T>` сохраняют сигнатуры и резолвят тип через `World.Components`.
- Регистрация выполняется без рефлексии: `context.Register<T>("game.Health")` создаёт замыкание `capacity => new Column<T>(capacity)`. Никаких `Activator`, `MakeGenericType` и сканирования сборок.
- Модуль реализует `IEngineModule` с тремя фазами:
  - `ConfigureComponents(ComponentRegisterContext)` — глобальные компоненты, один раз до создания миров;
  - `ConfigureServices(EngineServiceContainer)` — глобальные сервисы, один раз;
  - `ConfigureWorld(World)` — системы и ресурсы, для каждого мира.
- Порядок сборки: все `ConfigureComponents` → `ComponentRegistry.Freeze()` → все `ConfigureServices` → создание `SceneManager`.
- После заморозки регистрация запрещена, а `Get<T>` для незарегистрированного типа бросает понятную ошибку. Это защищает скриптовый слой от случайного изменения каталога в рантайме.
- Имена компонентов стабильны и сохраняются в сценах; числовые `Id` не сохраняются, потому что порядок регистрации модулей может измениться.
- Скриптовый слой получает read-only каталог: `TryGet(name)`, `GetById(id)`, `Types`. Преобразование значений скрипта в C#-структуры — отдельный слой биндинга поверх реестра.
- `EngineBuilder` собирает реестр, сервисы и `SceneManager`; `Engine` владеет ими и освобождает сцены, модули и сервисы.
- `World()` и `SceneManager()` без аргументов создают локальный незамороженный реестр для тестов и standalone-использования. Production-путь — `EngineBuilder`/`Engine` с общим замороженным реестром.

```csharp
public interface IEngineModule
{
    void ConfigureComponents(ComponentRegisterContext context) { }
    void ConfigureServices(EngineServiceContainer services) { }
    void ConfigureWorld(World world) { }
}

public sealed class CoreModule : IEngineModule
{
    public void ConfigureComponents(ComponentRegisterContext context)
    {
        context.Register<Node>("core.Node");
        context.Register<Transform2D>("core.Transform2D");
    }

    public void ConfigureWorld(World world) => WorldConfigurators.Default(world);
}
```

```csharp
using Engine engine = new EngineBuilder()
    .AddModule(new GameplayModule())
    .AddModule(new RenderingModule())
    .Build();

engine.Scenes.Replace(new MainSceneBuilder());
```

## 5. Иерархия и Transform

```csharp
[Flags]
public enum NodeFlags
{
    None = 0,
    ActiveSelf = 1 << 0,
    ActiveInHierarchy = 1 << 1,
    DestroyWithParent = 1 << 2,
}

public struct Node : IComponent
{
    public EntityId Parent;
    public EntityId FirstChild;
    public EntityId LastChild;
    public EntityId NextSibling;
    public EntityId PrevSibling;
    public NodeFlags Flags;
    public int Depth;
}

public struct Transform2D : IComponent
{
    public Vector2 Position;
    public float Rotation;      // радианы
    public Vector2 Scale;
    public Matrix3x2 World;     // вычисляется TransformSystem
}
```

- `Transform2D.Identity` и `Transform2D.At(position)` обязательны: `default(Transform2D)` имеет нулевой масштаб и не используется как рабочее значение.
- Дети хранятся в стабильном порядке через `FirstChild`/`LastChild` и двусвязный список.
- `SetParent` проверяет циклы, обновляет `Depth` поддерева и при необходимости сохраняет мировую трансформацию.
- Родитель без `Transform2D` считается identity.
- `ActiveInHierarchy` пересчитывается `World` при изменении иерархии или `ActiveSelf`.
- На первом этапе `TransformSystem` каждый кадр полностью обходит дерево и пересчитывает `World`. Оптимизация dirty-поддеревьями — после бенчмарков и без изменения API.
- `Local = Scale * Rotation * Translation`.
- `World = Local * Parent.World`.
- `Vector2.Transform(point, matrix)` даёт `point * matrix` (row-vector).
- При будущей загрузке в OpenGL/Silk.NET column-major-матрица получается транспонированием на границе рендера; ядро конвенцию не меняет.

## 6. World loop и системы

```csharp
public enum SystemPhase
{
    PreUpdate,
    Update,
    PostUpdate,
    Transform,
}

public interface ISystem
{
    void Update(World world, float dt);
}
```

Порядок кадра:

1. `World.Update` увеличивает `GameTime` (`DeltaTime`, `TotalTime`, `FrameCount`).
2. Выполняется `PreUpdate`.
3. Выполняется `Update`.
4. Выполняется `PostUpdate`.
5. Выполняется `Transform`: `TransformSystem` пересчитывает мировые матрицы.

- Внутри фазы системы выполняются в порядке регистрации.
- Система, добавившая новую систему в уже выполняющуюся фазу, увидит её со следующего кадра.
- Иерархия обслуживается сразу при структурных вызовах `World` (`SetParent`, `SetActive`, `Destroy`); отдельной системы иерархии нет.
- `CommandBuffer` применяется, когда система сама вызывает `world.Submit(cmd)` после обхода запроса. Автоматического глобального буфера нет.
- Магические числовые приоритеты не используются.
- Порядок фаз фиксирован; расширение — через новые фазы или явную регистрацию.
- `GameTime` хранит `DeltaTime`, `TotalTime`, `FrameCount`; фиксированный шаг и интерполяция — позже на уровне runtime.
- `WorldConfigurator` (делегат) добавляет в каждый новый мир базовые системы (например, `TransformSystem`) и общие ресурсы. Обслуживание иерархии — встроенная часть `World`, а не пользовательская система.

## 7. Сцены

### 7.1. ISceneBuilder

```csharp
public interface ISceneBuilder
{
    void Build(SceneBuildContext context);
}

public sealed class SceneBuildContext
{
    public World World { get; }
    public SceneManager Scenes { get; }
    public EngineServices Services { get; }
    public CancellationToken Cancellation { get; }
}
```

- Сцена не обязана наследоваться от базового класса сцены.
- `ISceneBuilder` создаёт начальные сущности, добавляет компоненты, регистрирует системы и ресурсы сцены.
- Один и тот же билдер можно использовать для `Replace`, `Push` и `Reload`.
- `Scene` хранит только рантайм-состояние (`Name`, `World`, `State`). `SceneManager` хранит рядом со сценой её `ISceneBuilder`, чтобы `Reload` мог пересоздать сцену.
- Позже `ScriptSceneBuilder : ISceneBuilder` сможет интерпретировать скриптовый язык и вызывать те же API мира. Для этого менять ядро не потребуется.
- Префабы позже подключаются как сервис (`PrefabRegistry`) и используются билдерами и скриптами.

Пример:

```csharp
public sealed class GameplaySceneBuilder : ISceneBuilder
{
    public void Build(SceneBuildContext context)
    {
        var world = context.World;

        var player = world.Create();
        world.Add(player, Transform2D.At(new Vector2(0, 0)));
        world.Add(player, new Velocity());
        world.Add(player, new PlayerTag());

        world.AddSystem(SystemPhase.Update, new PlayerSystem());
        world.AddSystem(SystemPhase.PostUpdate, new CameraSystem());
    }
}
```

### 7.2. Scene и SceneManager

```csharp
public enum SceneState
{
    Loading,
    Active,
    Paused,
    Unloading,
    Disposed,
}

public sealed class Scene
{
    public string Name { get; }
    public World World { get; }
    public SceneState State { get; }
}

public sealed class SceneManager
{
    public SceneManager(EngineServiceContainer? services = null, WorldConfigurator? configurator = null);

    public Scene? Current { get; }
    public int SceneCount { get; }
    public EngineServiceContainer Services { get; }

    public void Replace(ISceneBuilder builder, string name = "");
    public void Push(ISceneBuilder builder, string name = "");
    public void Pop();
    public void Reload();
    public void Update(float dt);
    public void Dispose();
}
```

- `SceneManager` создаёт `EngineServices`, если они не переданы, и тогда освобождает их вместе с собой. Внешние сервисы менеджер не освобождает.
- `SceneManager` передаёт `WorldConfigurator` каждому новому миру; без аргумента используется `WorldConfigurators.Default`.
- `Replace` заменяет текущую сцену.
- `Push` кладёт новую сцену поверх стека и ставит предыдущую на паузу.
- `Pop` уничтожает верхнюю сцену и возвращает предыдущую; `Pop` на пустом стеке — no-op, после `Pop` последней сцены `Current` равен `null`.
- `Reload` пересоздаёт текущую сцену через `ISceneBuilder`, сохранённый менеджером рядом со сценой; без текущей сцены бросает `InvalidOperationException`.
- Переходы, запрошенные во время `Update` или `Build`, ставятся в очередь и применяются в конце кадра, вне итерации по ECS. Переходы, запрошенные до первого `Update`, применяются в начале первого кадра.
- Переход, запрошенный внутри `Build`, применяется после текущего кадра, а не рекурсивно внутри построения.
- `Replace` сначала строит новую сцену. Если построение упало, новая сцена освобождается, текущая остаётся активной.
- После успешного построения старая сцена переводится в `Unloading` и освобождается.
- `SceneManager.Dispose` освобождает все сцены стека сверху вниз.
- По умолчанию обновляется только верхняя сцена; нижние находятся в `Paused`.
- Асинхронная загрузка (`BuildAsync`) — отдельный следующий этап; текущий `ISceneBuilder` синхронный.

### 7.3. Порядок построения сцены

1. Создать `World`.
2. Выполнить `WorldConfigurator`: добавить базовые системы и общие ресурсы.
3. Вызвать `ISceneBuilder.Build(context)`.
4. Прогнать иерархию и трансформы один раз, чтобы начальные мировые матрицы были готовы.
5. Перевести сцену в `Active`.
6. Применить отложенные переходы в конце кадра.

### 7.4. Жизненный цикл и ошибки

- `World` — единственный владелец сущностей, систем и ресурсов сцены.
- Сценовые ресурсы освобождаются через `World.Dispose`.
- Исключение в `ISceneBuilder.Build` не оставляет полупостроенную сцену активной.
- Логирование переходов и ошибок — через будущий `ILogger`; до его появления допускается `Debug`/`Trace` только на уровне runtime, не внутри ECS.
- Хуки `OnEnter`/`OnExit` для билдеров в первый этап не входят. Если понадобятся, добавляются отдельным опциональным интерфейсом, не меняя `ISceneBuilder`.

## 8. Границы будущих подсистем

### 8.1. Рендер

- `Core` не ссылается на Silk.NET, OpenGL, текстуры, камеры и шейдеры.
- Визуальные компоненты (`Sprite`, `SpriteRenderer`, `Camera`) будут жить в отдельной сборке рендера.
- Рендер читает `World`, строит снимок кадра и список команд; ядро об этом не знает.
- Соглашение по матрицам фиксировано; конвертация в column-major для GPU выполняется на границе рендера.

### 8.2. Физика

- В `Core` нет Box2D и других физических типов.
- Физика подключается отдельным проектом: свои компоненты, системы и ресурс физического мира.
- `World.Resources` и `AddSystem` — достаточные точки интеграции; при необходимости позже добавляется интерфейс `IPhysicsBackend`.

## 9. Этапы

### Упрочнение ядра

- Пересчёт трансформов — доминирующая стоимость кадра. Рассмотреть dirty-поддеревья или кэш колонок на время обхода; решение принимать по повторным замерам.
- Кэш списка архетипов под запрос — только если профиль покажет, что перебор архетипов заметен на фоне обхода сущностей.
- Пул для `CommandBuffer` — если буфер окажется горячим путём (сейчас ~370 B на операцию).
- Критерий: известные узкие места закрыты или осознанно оставлены, API стабилен для следующего этапа.

### Отложенные замеры

- **TODO(benchmarks):** после перехода на `ComponentRegistry` бенчмарки не запускались: текущая машина не тянет BenchmarkDotNet, числа будут несопоставимы с таблицей в `README.md`. Другая сессия должна повторить замеры на машине из README и проверить регрессию резолва `T` в `World.Add<T>/Get<T>`.
- Если регрессия заметна, добавить `FrozenDictionary` (уже используется после `Freeze`) или внутренний `[ThreadStatic]`-кэш `ComponentLookup<T>` без изменения публичного API.

## 10. Открытые вопросы

- 2D-трансформ — текущая база. Если движку нужен 3D-ready фундамент, `Transform2D` заменяется на `Transform3D` до появления 3D-специфичных подсистем.
- Асинхронная загрузка сцен и `BuildAsync` — после стабилизации синхронного API.
- Хуки `OnEnter`/`OnExit` — только если понадобятся после стабилизации сцен.
- Формат префабов и скриптового языка — поверх `ISceneBuilder`.
- Политика логирования — после появления runtime-слоя.

## 11. Вне текущего этапа

- Silk.NET, окно, ввод, OpenGL.
- Спрайты, NineSlice, камера, батчинг.
- Box2D или другая физика.
- Конфигурация движка и hot-reload.
- Игровая логика GunFight.
- Асинхронные ресурсы и префабы.
- Скриптовый язык сцен.
