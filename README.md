# XEngine

Headless-ядро 2D-движка: archetype ECS, иерархия, трансформы, системы и сцены.
Рендер, физика, ввод и игровая логика в этот репозиторий не входят.

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
  Ecs/            EntityId, World, Archetype, ComponentMask, Column<T>, Query, CommandBuffer
  Hierarchy/      Node (иерархией владеет World)
  Transforms/     Transform2D, TransformSystem
  Systems/        ISystem, SystemPhase, SystemScheduler, WorldConfigurator
  Scenes/         ISceneBuilder, SceneBuildContext, Scene, SceneManager
  Services/       EngineServices
  Time/           GameTime
tests/XEngine.Core.Tests/
benchmarks/XEngine.Core.Benchmarks/
docs/plans.md      архитектурные решения и оставшиеся задачи
docs/code-style.md конвенция оформления кода
```

## Быстрый старт

```csharp
using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Scenes;
using XEngine.Core.Systems;
using XEngine.Core.Transforms;

struct Velocity : IComponent
{
    public Vector2 Value;
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

using var scenes = new SceneManager();
scenes.Replace(new Level(), "level");

while (running) scenes.Update(dt);
```

`SceneManager.Update` сам применяет отложенные переходы, обновляет только верхнюю сцену и
пересчитывает трансформы в конце кадра. Цикл приложения и расчёт `dt` — задача будущего
runtime-слоя.

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

`CommandBuffer` схлопывает операции по сущности, умеет создавать и уничтожать сущности,
переподвешивать их и сохранять мировую трансформацию.

## Иерархия и трансформы

- `World.SetParent`, `SetActive`, `Destroy` обслуживают иерархию сразу: связи, `Depth`,
  `ActiveInHierarchy`, запрет циклов.
- `Destroy` по умолчанию переподвешивает детей к родителю удалённой сущности;
  `DestroyMode.Recursive` удаляет поддерево.
- `Transform2D` опционален. Если у родителя его нет, мировая матрица ребёнка просто
  наследует мировую матрицу ближайшего предка с `Transform2D`.
- Конвенция `System.Numerics` — row-vector: `point' = point * matrix`,
  `World = Local * Parent.World`, `Local = Scale * Rotation * Translation`.
- `TransformSystem` пересчитывает всё дерево в фазе `Transform`. Мировые матрицы готовы
  после `SceneManager`/`World.Update`; `World.UpdateTransforms()` запускает только эту фазу.

## Системы и ресурсы

Фазы выполняются в фиксированном порядке: `PreUpdate`, `Update`, `PostUpdate`, `Transform`.
Внутри фазы системы идут в порядке регистрации. `World` по умолчанию регистрирует
`TransformSystem` через `WorldConfigurators.Default`; свой набор систем задаётся
`WorldConfigurator`.

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

Замеры — ориентир для текущей машины, а не контракт. Контрольные значения с dev-машины
(.NET 10.0.13, x64):

| Сценарий | Стоимость |
| --- | --- |
| Обход 100 000 сущностей, 1–3 компонента | 60–130 µs, 0 B |
| Обход 10 000 сущностей | 4–13 µs, 0 B |
| Вызов пустого запроса | ~5.7 ns, 0 B |
| Вызов запроса по одной сущности | ~8.7 ns, 0 B |
| Create + Destroy | ~23 ns на операцию, 0 B |
| Add + Remove компонента | ~54 ns на операцию, 0 B |
| Reparent + detach | ~43 ns на операцию, 0 B |
| CommandBuffer create + destroy | ~103 ns и ~370 B на операцию |
| Полный пересчёт трансформов | ~60 ns на сущность |

Выводы:

- Обход запросов и структурные операции не аллоцируют; стоимость линейна по числу
  сущностей.
- `TransformSystem` — самая дорогая часть кадра: на сущность приходится несколько
  обращений к `World` плюс обход иерархии. Следующий кандидат на оптимизацию —
  dirty-поддеревья или кэширование колонок на время обхода.
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
