using XEngine.Core.Base;
using XEngine.Core.Utils;

namespace XEngine.Core.Common.Health
{
    public sealed class GHealth : GameComponent, IDisposable
    {
        private readonly GameTimer RegenDelay = new(float.MaxValue);
        public float MaxHealth { get; private set; } = 0;
        public float HealthRegen { get; private set; } = 0;
        public float Health { get; private set; } = 0;
        public bool IsRendered = false;

        private float _dHealth = 0;
        private const float DHFR = 10f; // Display health fill rate

        public float DisplayRatio => _dHealth / MaxHealth;
        public float DisplayLeftRatio => 1 - _dHealth / MaxHealth;
        public bool CanRegenerate => RegenDelay.IsFinished;

        private Action<Entity>? _onDeath = null;

        public GHealth Init(float maxHealth)
        {
            _dHealth = Health = MaxHealth = maxHealth;
            Owner.Scene.RegisterTimer(RegenDelay);
            return this;
        }

        public GHealth SetRegen(float healthRegenRate, float healthRegenDelay)
        {
            HealthRegen = healthRegenRate;
            RegenDelay.Duration = healthRegenDelay;
            return this;
        }

        public GHealth SetDeathCallback(Action<Entity> onDeath)
        {
            _onDeath = onDeath;
            return this;
        }

        public void Update(float dt)
        {
            if (CanRegenerate) Health = MathUtils.MoveToward(Health, MaxHealth, HealthRegen * dt);
            _dHealth = float.Lerp(_dHealth, Health, dt * DHFR);
        }

        public void DealDamage(float amount)
        {
            RegenDelay.Start();
            Health -= amount;
            if (Health <= 0)
            {
                Health = 0;
                _onDeath?.Invoke(Owner);
            }
        }

        public void Dispose()
        {
            Owner.Scene.UnregisterTimer(RegenDelay);
        }
    }
}
