# XEngine

Headless-ядро 2D-движка: archetype ECS, иерархия, системы, сцены и отдельный модуль
2D-трансформов. Рендер, физика, ввод и игровая логика в этот репозиторий не входят.

## Требования

- .NET SDK 10.0.113 или новее в рамках `10.0.x` (см. `global.json`).
- Репозиторий собирается и тестируется без GPU и без внешних сервисов.

## Сборка и проверки

```bash
dotnet build XEngine.slnx
dotnet test XEngine.slnx
```

`XEngine.Core` не имеет NuGet-зависимостей. xUnit подключён только в тестовом проекте,
BenchmarkDotNet — только в проекте бенчмарков.

## Структура

```
src/XEngine.Core/
  Ecs/            EntityId, World, Archetype, ComponentRegistry, ComponentHandle, Query, CommandBuffer
  Hierarchy/      Node, BeforeReparent-событие (иерархией владеет World)
  Systems/        ISystem, SystemPhase, SystemScheduler, WorldConfigurator
  Scenes/         ISceneBuilder, SceneBuildContext, Scene, SceneManager
  Services/       EngineServiceContainer
  Modules/        IEngineModule, CoreModule
  Hosting/        Engine, EngineBuilder
  Time/           GameTime
src/XEngine.Common.Transform/
  Transform2D, TransformFlags, TransformSystem, TransformModule, TransformWorldConfigurator
tests/XEngine.Core.Tests/
benchmarks/XEngine.Core.Benchmarks/
docs/plans.md      оставшиеся задачи и будущие подсистемы
docs/code-style.md конвенция оформления кода
```

## Быстрый старт

```csharp
using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Hosting;
using XEngine.Core.Modules;
using XEngine.Core.Scenes;
using XEngine.Core.Systems;
using XEngine.Common.Transform;

struct Velocity : IComponent
{
    public Vector2 Value;
}

sealed class GameModule : IEngineModule
{
    public void ConfigureComponents(ComponentRegisterContext context) =>
        context.Register<Velocity>("game.Velocity");
}

sealed class MovementSystem : ISystem
{
    private static void SingleUpdate(in float dt, ref Transform2D t, ref Velocity v) => t.Position += v.Value * dt;
    public void Update(World world, float dt) => world.Query<Transform2D, Velocity>().ForEach(dt, SingleUpdate);
}

sealed class Level : ISceneBuilder
{
    public void Build(SceneBuildContext context)
    {
        World world = context.World;

        EntityId player = world.Create();
        world.Add(player, Transform2D.At(Vector2.Zero));
        world.Add(player, new Velocity { Value = new Vector2(5, 0) });

        world.AddSystem(SystemPhase.Update, new MovementSystem());
    }
}

using Engine engine = new EngineBuilder()
    .AddModule(new GameModule())
    .AddModule(new TransformModule())
    .Build();

engine.Scenes.Replace(new Level(), "level");

while (running) engine.Update(dt);
```

Компоненты регистрируются один раз в модуле и замораживаются до создания миров. Для тестов и
standalone-сценариев `new World()`/`new SceneManager()` создают локальный незамороженный
реестр: недостающие типы регистрируются при первом обращении. Трансформы в него не входят:
для standalone-мира передайте `TransformWorldConfigurator.Default`, а в движке подключите
`TransformModule`.

`Engine.Update` вызывает `SceneManager.Update`, который сам применяет отложенные переходы,
обновляет только верхнюю сцену и в конце кадра запускает фазу `Transform`. Если модуль
трансформов не подключён, фаза остаётся пустой. Цикл приложения и расчёт `dt` — задача
будущего runtime-слоя.

## ECS

- Каждая сущность создаётся через `World.Create()` и сразу получает `Node`. Добавить или
  удалить `Node` вручную нельзя.
- Компоненты — только `struct`, реализующие `IComponent`.
- `World.Add/Remove` двигают сущность между архетипами; значения общих компонентов
  сохраняются.
- Обход запросов — только `world.Query<...>().ForEach(...)`. `foreach` по результатам
  запроса не предусмотрен: `ForEach` даёт `ref`-доступ к колонкам без аллокаций.
- Фильтры: `With<T>()` и `Without<T>()`.
- Структурные изменения во время `ForEach` запрещены. В Debug они бросают
  `InvalidOperationException`, в Release проверка не компилируется. Для изменений есть
  `CommandBuffer`:

```csharp
CommandBuffer commands = world.BeginCommands();

world.Query<Lifetime>().ForEach((EntityId entity, ref Lifetime lifetime) => commands.DestroyIf(lifetime.Seconds <= 0, entity));

world.Submit(commands);
```

`CommandBuffer` схлопывает операции по сущности, умеет создавать и уничтожать сущности и
переподвешивать их. Сохранение мировой трансформации задаётся флагом
`TransformFlags.KeepWorldOnDetach`, а не параметром команды.

## Реестр компонентов и модули

- `ComponentRegistry` один на движок и общий для всех миров и сцен. Он выдаёт
  `ComponentType.Id`, хранит имя и CLR-тип и создаёт колонки без рефлексии.
- Регистрация выполняется в `IEngineModule.ConfigureComponents`. `EngineBuilder` вызывает
  все модули, замораживает реестр, затем настраивает сервисы и создаёт `SceneManager`.
- После заморозки новый тип зарегистрировать нельзя, а `World.Add<T>/Get<T>/Has<T>` для
  незарегистрированного `T` бросает `InvalidOperationException`.
- `World.Add<T>/Get<T>/Has<T>` резолвят дескриптор через `World.Components`; статической
  авторегистрации в `ComponentType<T>` больше нет.
- `World.Handle<T>()` возвращает `ComponentHandle<T>`, который фиксирует `ComponentType` и
  реестр. Перегрузки `Get/TryGet/Has(entity, handle)` проверяют принадлежность реестру и
  используются в горячих путях, чтобы не разрешать тип на каждой сущности. Невалидный или
  чужой handle бросает `InvalidOperationException`.
- `ComponentRegistry.Get<T>()` держит горячий `[ThreadStatic]`-кэш в маленьком
  inline-методе; холодный поиск/регистрация вынесены в `NoInlining`, чтобы не раздувать
  вызывающие query-методы и сохранять инлайнинг `ForEach`.
- Имена компонентов стабильны и сохраняются в сценах; числовые `Id` не сохраняются, потому
  что порядок регистрации модулей может измениться.
- Production-путь — `EngineBuilder`/`Engine` с общим замороженным реестром. `new World()` и
  `new SceneManager()` создают локальный незамороженный реестр для тестов и standalone.

## Иерархия и трансформы

- `World.SetParent`, `SetActive`, `Destroy` обслуживают иерархию сразу: связи, `Depth`,
  `ActiveInHierarchy`, запрет циклов.
- `Destroy` по умолчанию переподвешивает детей к родителю удалённой сущности;
  `DestroyMode.Recursive` удаляет поддерево.
- `Transform2D` живёт в `XEngine.Common.Transform` и опционален. Если у родителя его нет, мировая
  матрица ребёнка наследует мировую матрицу ближайшего предка с `Transform2D`.
- Конвенция `System.Numerics` — row-vector: `point' = point * matrix`,
  `World = Local * Parent.World`, `Local = Scale * Rotation * Translation`.
- `TransformFlags.KeepWorldOnDetach` — постоянная политика сущности: при каждом
  `SetParent`/detach, включая переподвешивание детей при `Destroy`, мировая матрица
  сохраняется. Флаг применяется к каждому ребёнку отдельно; `DestroyMode.Recursive`
  его игнорирует, потому что поддерево уничтожается.
- `World.BeforeReparent` вызывается после валидаций и early-return, но до изменения связей.
  Аргументы передаются по `in`, поэтому подписка не аллоцирует на вызов.
- `TransformSystem` пересчитывает всё дерево в фазе `Transform`. Мировые матрицы готовы
  после `SceneManager`/`World.Update`; `World.UpdateTransforms()` запускает только эту фазу.

## Системы и ресурсы

Фазы выполняются в фиксированном порядке: `PreUpdate`, `Update`, `PostUpdate`, `Transform`.
Внутри фазы системы идут в порядке регистрации. `new World()` не добавляет систем
автоматически; свой набор задаётся `WorldConfigurator`. В движке трансформы подключаются
через `TransformModule`, в standalone — через `TransformWorldConfigurator.Default`.

- Состояние текущей сцены — в `World.SetResource<T>` / `GetResource<T>`.
- Глобальные сервисы (конфигурация, логирование, ассеты, будущие бэкенды) —
  в `EngineServices`, который передаётся через `SceneBuildContext.Services`.
- `World.Dispose` и `SceneManager.Dispose` освобождают системы и ресурсы, реализующие
  `IDisposable`.

## Сцены

- `SceneManager.Replace/Push/Pop/Reload` ставят переход в очередь. Переходы применяются в
  начале кадра (если запрошены до `Update`) и в конце кадра (если запрошены во время
  обновления или построения).
- `Push` ставит текущую сцену на паузу, `Pop` возвращает её и уничтожает верхнюю.
- Если `ISceneBuilder` бросил исключение, частично построенный мир освобождается, а
  текущая сцена остаётся активной.
- Асинхронной загрузки пока нет: `ISceneBuilder` синхронный.

## Бенчмарки

```bash
dotnet run -c Release --project benchmarks/XEngine.Core.Benchmarks -- --filter "*QueryBenchmarks*"
dotnet run -c Release --project benchmarks/XEngine.Core.Benchmarks -- --filter "*QueryCallBenchmarks*"
dotnet run -c Release --project benchmarks/XEngine.Core.Benchmarks -- --filter "*StructuralBenchmarks*"
dotnet run -c Release --project benchmarks/XEngine.Core.Benchmarks -- --filter "*TransformBenchmarks*"
```

> Замеры — ориентир для текущей машины, а не контракт. Последний прогон:
> AMD Ryzen 7 7800X3D (16 logical / 8 physical cores), Linux CachyOS,
> .NET SDK 10.0.114, runtime 10.0.14, x64 RyuJIT.
>
> **TODO:** числа ниже не перемерялись на текущей машине. Нужны повторные прогоны
> `TransformBenchmarks`, `StructuralBenchmarks` и `SetParent` с подписчиком
> `BeforeReparent` и без; актуальный список задач — в `docs/plans.md`.

Обход запросов (0 B на операцию):

| Запрос | 10 000 сущностей | 100 000 сущностей |
| --- | ---: | ---: |
| 1 компонент | 8.93 µs | 85.15 µs |
| 2 компонента | 6.50 µs | 67.38 µs |
| 3 компонента | 12.68 µs | 129.84 µs |
| С фильтром `Without<Health>` | 4.32 µs | 42.31 µs |
| `Count()` | 9.9 ns | 9.9 ns |

Вызов запроса: пустой — 6.0 ns, по одной сущности — 9.7 ns.

Структурные операции (на операцию, 0 B):

| Сценарий | Стоимость |
| --- | ---: |
| Create + Destroy | 22.96 ns |
| Add + Remove компонента | 55.49 ns |
| Reparent + detach | 48.78 ns |
| CommandBuffer create + destroy | 106.46 ns, 379 B |
| Построить цепь и удалить рекурсивно | 3.35 µs |

Полный пересчёт трансформов (0 B):

| Сущностей | Flat | Chain | BinaryTree |
| ---: | ---: | ---: | ---: |
| 1 000 | 51.75 µs | 77.20 µs | 72.88 µs |
| 10 000 | 511.19 µs | 789.52 µs | 754.49 µs |

Выводы:

- Обход запросов и структурные операции не аллоцируют; стоимость линейна по числу
  сущностей.
- `TransformSystem` — самая дорогая часть кадра: на сущность приходится несколько
  обращений к `World` плюс обход иерархии. Он разрешает `ComponentType` один раз за
  кадр; следующий кандидат — dirty-поддеревья или кэш колонок на время обхода.
- `CommandBuffer` аллоцирует группу команд на сущность. Если буфер станет горячим,
  понадобится пул.
- `SetParent` проверяет цикл проходом вверх по родителям, поэтому построение очень
  глубокой цепи квадратично по глубине. Для обычных деревьев глубина мала, но это
  ограничение нужно помнить.

## Вне текущего этапа

- Рендер, окно, ввод, спрайты, камера, батчинг.
- Физика (Box2D или другая).
- Конфигурация, hot-reload, асинхронные ресурсы и префабы.
- Скриптовый язык сцен и игровая логика GunFight.
