using XEngine.Core.Base;
using XEngine.Core.Common;
using XEngine.Core.Graphics;
using XEngine.Core.Input;

namespace XEngine.Core.Scenery
{
    public class GScene
    {
        public event Action? OnEnd;

        protected int _id = 0;

        private readonly Dictionary<int, Entity> _entities = [];
        private readonly List<IGameSystem> _systems = [];
        private readonly List<Action> schedules = [];
        private readonly List<GameTimer> _timers = [];

        public bool IsDeleted { get; set; } = false;
        public readonly IInputService Input;
        public readonly IAssetLoader Assets;
        public readonly Random Random;
        public float GetR01() => (float)Random.NextDouble();
        public float GetR_11() => (float)Random.NextDouble() * 2 - 1;
        public GCamera Camera { get; }

        public GScene(GameEngine _engine)
        {
            Assets = _engine.Assets;
            Input = _engine.Input;
            Random = new Random();

            var _cam = SpawnEntity();
            Camera = _cam.AddComponent<GCamera>();
            _cam.Transform.Init(new(0, 0, 30), 0);
        }

        public Entity SpawnEntity()
        {
            int id = _id++;
            var _e = new Entity(id, this);
            _entities[id] = _e;
            return _e;
        }

        public void Schedule(Action action)
        {
            schedules.Add(action);
        }

        protected void ClearScene()
        {
            foreach (var e in _entities.Values) e.MarkDelete();
        }

        public void End() => OnEnd?.Invoke();

        #region System
        public void AddSystem(IGameSystem system)
        {
            _systems.Add(system);
            _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public void RemoveSystem(IGameSystem system)
        {
            _systems.Remove(system);
        }
        #endregion

        #region Context
        public virtual void Load() { }

        public virtual void Unload() { }
        #endregion

        #region Timer
        public void RegisterTimer(GameTimer timer)
        {
            if (!_timers.Contains(timer)) _timers.Add(timer);
        }
        public void UnregisterTimer(GameTimer timer)
        {
            _timers.Remove(timer);
        }
        #endregion

        #region Update
        public void Update(float _dt)
        {
            if (IsDeleted) return;
            UpdateTimers(_dt);
            InvokeSystems(_dt);
            InvokeEvents();
            RemoveDeleted();
        }
        private void UpdateTimers(float _dt)
        {
            foreach (var timer in _timers) timer.Tick(_dt);
        }
        private void InvokeSystems(float _dt)
        {
            foreach (var _s in _systems) if (_s.IsEnabled) _s.Update(this, _dt);
        }
        private void InvokeEvents()
        {
            foreach (var action in schedules) action.Invoke();
            schedules.Clear();
        }
        private void RemoveDeleted()
        {
            var _removedEntityId = _entities
                .Where(kvp => kvp.Value._isDeleted)
                .Select(kvp => kvp.Key);
            foreach (var key in _removedEntityId)
            {
                if (_entities.TryGetValue(key, out var value))
                {
                    value.Transform.SetParent(null);
                    value.Dispose();
                    _entities.Remove(key);
                }
            }
        }
        #endregion

        #region Query
        public Entity? GetById(int id) => _entities.TryGetValue(id, out var entity) ? entity : null;

        public IEnumerable<Entity> IterateByIds(IEnumerable<int> ids)
        {
            foreach (var id in ids) if (_entities.TryGetValue(id, out var entity)) yield return entity;
        }

        public IEnumerable<Entity> Query(Predicate<Entity> _predicate)
        {
            foreach (var _e in _entities.Values) if (_predicate(_e)) yield return _e;
        }

        public IEnumerable<(Entity, T1)> Query<T1>(Func<Entity, T1, bool>? _predicate = null)
where T1 : GameComponent
        {
            foreach (var _e in _entities.Values)
            {
                if (_e.TryGet<T1>(out T1 comp1))
                {
                    if (_predicate != null && !_predicate(_e, comp1)) continue;
                    yield return (_e, comp1);
                }
            }
        }

        public IEnumerable<(Entity, T1, T2)> Query<T1, T2>(Func<Entity, T1, T2, bool>? _predicate = null)
where T1 : GameComponent
where T2 : GameComponent
        {
            foreach (var _e in _entities.Values)
            {
                if (_e.TryGet<T1>(out T1 comp1) && _e.TryGet<T2>(out T2 comp2))
                {
                    if (_predicate != null && !_predicate(_e, comp1, comp2)) continue;
                    yield return (_e, comp1, comp2);
                }
            }
        }

        public IEnumerable<(Entity, T1, T2, T3)> Query<T1, T2, T3>(Func<Entity, T1, T2, T3, bool>? _predicate = null)
where T1 : GameComponent
where T2 : GameComponent
where T3 : GameComponent
        {
            foreach (var _e in _entities.Values)
            {
                if (_e.TryGet<T1>(out T1 comp1) && _e.TryGet<T2>(out T2 comp2) && _e.TryGet<T3>(out T3 comp3))
                {
                    if (_predicate != null && !_predicate(_e, comp1, comp2, comp3)) continue;
                    yield return (_e, comp1, comp2, comp3);
                }
            }
        }
        #endregion
    }
}
