# Бенчмарки

Запуск из корня репозитория:

```bash
dotnet run -c Release --project benchmarks/XEngine.Core.Benchmarks -- --filter "*QueryBenchmarks*"
dotnet run -c Release --project benchmarks/XEngine.Core.Benchmarks -- --filter "*QueryCallBenchmarks*"
dotnet run -c Release --project benchmarks/XEngine.Core.Benchmarks -- --filter "*StructuralBenchmarks*"
dotnet run -c Release --project benchmarks/XEngine.Core.Benchmarks -- --filter "*ReparentBenchmarks*"
dotnet run -c Release --project benchmarks/XEngine.Core.Benchmarks -- --filter "*TransformBenchmarks*"
```

Результаты складываются в `BenchmarkDotNet.Artifacts/` (игнорируется git).

> Замеры — ориентир для текущей машины, а не контракт. Последний прогон:
> AMD Ryzen 7 7800X3D (16 logical / 8 physical cores), Linux CachyOS,
> .NET SDK 10.0.114, runtime 10.0.14, x64 RyuJIT.
>
> `StructuralBenchmarks` и `ReparentBenchmarks` перемерены 5 ноября 2026.
> `TransformBenchmarks` после выноса трансформов создавал `new World()` без
> `TransformWorldConfigurator.Default`, поэтому измерял пустую фазу (≈1 ns/вызов).
> Бенчмарк исправлен, но повторный прогон не запускался: расхождение с числами ниже
> объясняется отсутствием `TransformSystem`, а не ускорением. Для актуальных значений
> нужен один прогон `*TransformBenchmarks*`.

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
| Create + Destroy | 26.54 ns |
| Add + Remove компонента | 54.08 ns |
| Reparent + detach | 60.92 ns |
| CommandBuffer create + destroy | 115.15 ns, 371 B |
| Построить цепь и удалить рекурсивно | 3.48 µs |

Стоимость подписки `BeforeReparent` (1 000 reparent + detach, на операцию, 0 B):

| Подписчик | Стоимость |
| --- | ---: |
| Нет | 60.54 ns |
| Пустой обработчик | 61.18 ns |

Полный пересчёт трансформов (0 B, исторические значения до выноса `TransformSystem`;
требуется повторный прогон после исправления бенчмарка):

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
- `CommandBuffer` аллоцирует группу команд на сущность (~371 B). Если буфер станет
  горячим, понадобится пул.
- Пустой подписчик `BeforeReparent` добавляет ~0.6 ns к операции reparent+detach (<1% от
  стоимости `SetParent`) и не аллоцирует; event-хук не является узким местом.
- `SetParent` проверяет цикл проходом вверх по родителям, поэтому построение очень
  глубокой цепи квадратично по глубине. Для обычных деревьев глубина мала, но это
  ограничение нужно помнить.
