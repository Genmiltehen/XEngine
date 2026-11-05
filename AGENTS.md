# AGENTS.md

Инструкции для агентов и разработчиков. Пользовательская документация — `README.md`.
Дополнительно: `docs/code-style.md`, `docs/TODO.md`, `docs/benchmarks.md`,
`samples/plan.md`, `legacy/README.md`.

## Назначение и границы

`XEngine` — headless-ядро 2D-движка: archetype ECS, иерархия, системы, сцены и
опциональный модуль 2D-трансформов.

- `src/XEngine.Core` не имеет NuGet-зависимостей и не знает о рендере, физике, вводе,
  окне, OpenGL/Silk.NET.
- `src/XEngine.Common.Transform` зависит только от `XEngine.Core`.
- Рендер, физика и ввод подключаются отдельными проектами через модули, системы и
  ресурсы. `samples/XEngine.Interactive` пока существует только как план.
- `legacy/` — справочный архив старого прототипа. Он не входит в `XEngine.slnx`, не
  компилируется и не является рабочим кодом.
- `bin/`, `obj/`, `BenchmarkDotNet.Artifacts/` — сгенерированные каталоги, не редактировать.

## Команды

```bash
dotnet build XEngine.slnx
dotnet test XEngine.slnx
dotnet test XEngine.slnx --filter FullyQualifiedName~WorldTests
```

- SDK: `10.0.113`, допускается `10.0.x` (`global.json`).
- Сборка и тесты не требуют GPU и внешних сервисов.
- Бенчмарки запускаются только в Release, команды и цифры — в `docs/benchmarks.md`.

## Архитектура

```text
src/XEngine.Core/
  Ecs/       EntityId, World, Archetype, ComponentRegistry, ComponentHandle, Query, CommandBuffer
  Hierarchy/ Node, BeforeReparent (иерархией владеет World)
  Systems/   ISystem, SystemPhase, SystemScheduler, WorldConfigurator
  Scenes/    ISceneBuilder, SceneBuildContext, Scene, SceneManager
  Services/  EngineServiceContainer
  Modules/   IEngineModule, CoreModule
  Hosting/   Engine, EngineBuilder
  Time/      GameTime
src/XEngine.Common.Transform/
  Transform2D, TransformFlags, TransformSystem, TransformModule,
  TransformWorldConfigurator, TransformReparentListener
tests/XEngine.Core.Tests/       xUnit, без GPU
benchmarks/XEngine.Core.Benchmarks/ BenchmarkDotNet
```

## Ключевые инварианты

- Компоненты — только `struct : IComponent`. `Node` принадлежит `World`: вручную его
  нельзя добавить, удалить или заменить.
- Регистрация компонентов — только в `IEngineModule.ConfigureComponents`. Реестр
  замораживается до `ConfigureServices` и создания миров. В production-пути
  (`EngineBuilder`/`Engine`) незарегистрированный тип — ошибка. `new World()` и
  `new SceneManager()` создают локальный незамороженный реестр для тестов/standalone.
- Обход — только `world.Query<...>().ForEach(...)`. `foreach` по запросу не
  предусмотрен. Фильтры — `With<T>()`/`Without<T>()`; одиночная сущность —
  `EntityAt()`.
- Структурные изменения во время `ForEach` запрещены. Для них используется
  `CommandBuffer` + `world.Submit(commands)`. Проверка активна только в Debug.
- Фазы идут строго в порядке `PreUpdate`, `Update`, `PostUpdate`, `Transform`; внутри
  фазы — порядок регистрации. `World.UpdateTransforms()` запускает только `Transform`.
- `SetParent`, `SetActive`, `Destroy` сразу обслуживают иерархию: `Node`, глубину,
  `ActiveInHierarchy`, запрет циклов. `Destroy` по умолчанию переподвешивает детей,
  `DestroyMode.Recursive` удаляет поддерево.
- Трансформы опциональны. В движке подключайте `TransformModule`, в standalone-мире —
  `TransformWorldConfigurator.Default`. Без этого `TransformSystem` не работает.
- Матрицы `System.Numerics` — row-vector: `World = Local * Parent.World`,
  `Local = Scale * Rotation * Translation`. `KeepWorldOnDetach` — постоянный флаг
  сущности, а не параметр команды.
- Переходы сцен (`Replace/Push/Pop/Reload`) ставятся в очередь и применяются в начале
  и конце кадра. `ISceneBuilder` синхронный; при исключении частичный мир освобождается.
- `World`/`SceneManager` освобождают `IDisposable`-системы и ресурсы. `Engine` после
  `OnEngineDisposing` освобождает сцены, `IDisposable`-модули и сервисы. Общие
  платформенные ресурсы (окно, GL, ввод) не кладите в ресурсы мира.
- `Core` остаётся dimension-agnostic: 3D-трансформы и физика — отдельные модули, не
  замена `Transform2D` в ядре.

## Правила изменений

- Соблюдайте `docs/code-style.md`: один верхнеуровневый тип на файл, имя файла = имя типа,
  арность в имени (`Query2.cs`), без XML-документации, комментарии только «почему».
- Поведение фиксируется тестами. Для изменений в горячих путях проверяйте отсутствие
  аллокаций и при необходимости обновляйте бенчмарки.
- `README.md` — для пользователей: установка, сборка, быстрый старт. Архитектурные
  детали и внутренние правила держите в `AGENTS.md` и `docs/`.
- Не добавляйте пакеты в `XEngine.Core`. Не подключайте `legacy/` к решению.

## Планы и TODO

`docs/TODO.md` — планы. `docs/plans.md` удалён; не создавайте его
заново и не дублируйте планы в других документах. Выполненные пункты удаляются.

Конвенция `docs/TODO.md`:

- `# [ Название ]` — заголовок раздела плана.
- `## [ Название ]` — подзаголовок, если нужен.
- `- TODO [<lvl>]: <краткое описание>` — начало задачи; `lvl` — критичность: `0` —
  блокер, `1` — высокий, `2` — средний, `3` — низкий, `4` — минимальный.
- TODO может быть многострочным. Всё, что идёт после первой строки до следующего
  `- TODO [<lvl>]:`, относится к текущей задаче: абзацы, списки, снипеты кода и любые
  другие материалы. Продолжение лучше отделять отступом в 2 пробела.
- Уже выполненные архитектурные инварианты и критерии завершения этапов не оформляются
  как TODO. Инварианты живут в `AGENTS.md`, критерии — обычным текстом в разделе.
- Не дублируйте один и тот же план в разных разделах `docs/TODO.md`.

~~~markdown
# [ Инвентарь ]
- TODO [1]: переписывание логики инвентаря
  Первая строка — краткое описание. Ниже можно расписать полный план.

  ```csharp
  var inventory = new Inventory();
  ```

- TODO [.]: следующий пункт
~~~
