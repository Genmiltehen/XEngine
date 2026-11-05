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

```text
src/XEngine.Core/                ядро: ECS, иерархия, системы, сцены, модули, хостинг
src/XEngine.Common.Transform/    2D-трансформы, необязательный модуль
tests/XEngine.Core.Tests/        xUnit-тесты
benchmarks/XEngine.Core.Benchmarks/ бенчмарки
docs/                            планы, TODO, стиль кода, бенчмарки
samples/                         план временного интерактивного harness
legacy/                          справочный архив старого прототипа
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

## Документация

- `AGENTS.md` — архитектура, инварианты и правила для разработчиков и агентов.
- `docs/code-style.md` — стиль кода.
- `docs/TODO.md` — планы и задачи.
- `docs/benchmarks.md` — команды, замеры и выводы по производительности.
- `samples/plan.md` — план временного интерактивного harness на Silk.NET.
- `legacy/README.md` — описание справочного архива старого прототипа.

## Вне текущего этапа

- Рендер, окно, ввод, спрайты, камера, батчинг.
- Физика (Box2D или другая).
- Конфигурация, hot-reload, асинхронные ресурсы и префабы.
- Скриптовый язык сцен и игровая логика GunFight.
