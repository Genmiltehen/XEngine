using XEngine.Core.Ecs;
using XEngine.Core.Hosting;
using XEngine.Core.Modules;

namespace XEngine.Core.Tests.Ecs;

public sealed class ComponentRegistryTests
{
    [Fact]
    public void Registration_AssignsSequentialIds_AndBuildsCatalog()
    {
        var registry = new ComponentRegistry();
        using Engine engine = new EngineBuilder(registry)
            .AddModule(new TestModule(context => context.Register<Health>("game.Health")))
            .Build();

        Assert.True(registry.IsFrozen);
        Assert.Equal(3, registry.Count);

        ComponentType node = registry.GetById(0);
        Assert.Equal("core.Node", node.Name);
        Assert.Equal(typeof(XEngine.Core.Hierarchy.Node), node.RuntimeType);

        ComponentType transform = registry.GetById(1);
        Assert.Equal("core.Transform2D", transform.Name);
        Assert.Equal(typeof(XEngine.Core.Transforms.Transform2D), transform.RuntimeType);

        ComponentType health = registry.GetById(2);
        Assert.Equal("game.Health", health.Name);
        Assert.Equal(typeof(Health), health.RuntimeType);
        Assert.Equal([node, transform, health], registry.Types);

        Assert.True(registry.TryGet("game.Health", out ComponentType byName));
        Assert.Same(health, byName);

        Assert.True(registry.TryGet<Health>(out ComponentType byType));
        Assert.Same(health, byType);
    }

    [Fact]
    public void GetById_UnknownId_Throws()
    {
        var registry = new ComponentRegistry();
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.GetById(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.GetById(-1));
    }

    [Fact]
    public void TryGet_MissingEntries_ReturnFalse()
    {
        var registry = new ComponentRegistry();
        Assert.False(registry.TryGet("missing", out _));
        Assert.False(registry.TryGet<Health>(out _));
        Assert.Throws<ArgumentException>(() => registry.TryGet(" ", out _));
    }

    [Fact]
    public void Register_SameTypeWithDifferentName_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new EngineBuilder()
            .AddModule(new TestModule(context =>
            {
                context.Register<Health>("game.Health");
                context.Register<Health>("game.HealthAlias");
            }))
            .Build());

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("already registered as 'game.Health'", exception.InnerException!.Message);
    }

    [Fact]
    public void Register_SameTypeWithSameName_ReturnsExistingDescriptor()
    {
        ComponentType? first = null;
        ComponentType? second = null;
        using Engine engine = new EngineBuilder()
            .AddModule(new TestModule(context =>
            {
                first = context.Register<Health>("game.Health");
                second = context.Register<Health>("game.Health");
            }))
            .Build();

        Assert.Same(first, second);
        Assert.Equal(3, engine.Components.Count);
    }

    [Fact]
    public void Register_DuplicateNameForDifferentType_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new EngineBuilder()
            .AddModule(new TestModule(context =>
            {
                context.Register<Health>("game.Shared");
                context.Register<Velocity>("game.Shared");
            }))
            .Build());

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("already registered for", exception.InnerException!.Message);
    }

    [Fact]
    public void Register_AfterFreeze_Throws()
    {
        ComponentRegisterContext? captured = null;
        using Engine engine = new EngineBuilder()
            .AddModule(new TestModule(context => captured = context))
            .Build();

        Assert.True(engine.Components.IsFrozen);
        var exception = Assert.Throws<InvalidOperationException>(() => captured!.Register<Health>("game.Late"));
        Assert.Contains("frozen", exception.Message);
        Assert.Equal(2, engine.Components.Count);
    }

    private sealed class TestModule(Action<ComponentRegisterContext> configureComponents) : IEngineModule
    {
        public void ConfigureComponents(ComponentRegisterContext context) => configureComponents(context);
    }
}
