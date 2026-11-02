using XEngine.Core.Ecs;
using XEngine.Core.Hosting;
using XEngine.Core.Modules;
using XEngine.Core.Scenes;
using XEngine.Core.Services;
using XEngine.Core.Systems;
using XEngine.Core.Tests.Ecs;

namespace XEngine.Core.Tests.Hosting;

public sealed class EngineBuilderTests
{
    [Fact]
    public void Build_RunsPhasesInOrder_AndFreezesBeforeServices()
    {
        List<string> events = [];
        var registry = new ComponentRegistry();
        var module = new RecordingModule(events, registry);

        using Engine engine = new EngineBuilder(registry).AddModule(module).Build();

        Assert.Equal(["components", "services"], events);
        Assert.True(module.RegistryFrozenBeforeServices);
        Assert.True(registry.IsFrozen);
        Assert.Same(registry, engine.Components);

        engine.Scenes.Replace(new TestBuilder(_ => { }), "test");
        engine.Update(0f);

        Assert.Equal(["components", "services", "world"], events);
        Assert.Same(registry, module.WorldComponents);
    }

    [Fact]
    public void Build_SecondCall_Throws()
    {
        var builder = new EngineBuilder();
        using Engine engine = builder.Build();
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_DefaultBuilder_RegistersCoreComponents()
    {
        using Engine engine = new EngineBuilder().Build();

        Assert.True(engine.Components.IsFrozen);
        Assert.Equal(2, engine.Components.Count);
        Assert.Equal("core.Node", engine.Components.GetById(0).Name);
        Assert.Equal("core.Transform2D", engine.Components.GetById(1).Name);
    }

    [Fact]
    public void Dispose_DisposesScenesThenModulesThenServices()
    {
        List<string> events = [];
        var module = new DisposableModule(events);
        Engine engine = new EngineBuilder().AddModule(module).Build();
        engine.Scenes.Replace(new TestBuilder(_ => { }), "test");
        engine.Update(0f);

        engine.Dispose();

        Assert.True(module.Disposed);
        Assert.True(module.Service.Disposed);
        Assert.Equal(["module", "service"], events);
    }

    [Fact]
    public void Build_ModuleFailure_IsWrappedWithPhaseName()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new EngineBuilder()
            .AddModule(new ThrowingModule())
            .Build());

        Assert.Contains("ThrowingModule failed in ConfigureComponents", exception.Message);
        Assert.IsType<NotSupportedException>(exception.InnerException);
    }

    [Fact]
    public void SceneBuild_UnregisteredComponent_ThrowsInsteadOfAutoRegistering()
    {
        using Engine engine = new EngineBuilder().Build();
        engine.Scenes.Replace(new TestBuilder(context =>
        {
            EntityId entity = context.World.Create();
            context.World.Add(entity, new Health { Value = 1 });
        }));

        var exception = Assert.Throws<InvalidOperationException>(() => engine.Update(0f));
        Assert.Contains("not registered", exception.Message);
        Assert.Equal(2, engine.Components.Count);
    }

    private sealed class RecordingModule(List<string> events, ComponentRegistry registry) : IEngineModule
    {
        public bool RegistryFrozenBeforeServices { get; private set; }
        public ComponentRegistry? WorldComponents { get; private set; }

        public void ConfigureComponents(ComponentRegisterContext context)
        {
            events.Add("components");
            context.Register<Health>("game.Health");
        }

        public void ConfigureServices(EngineServiceContainer services)
        {
            events.Add("services");
            RegistryFrozenBeforeServices = registry.IsFrozen;
        }

        public void ConfigureWorld(World world)
        {
            events.Add("world");
            WorldComponents = world.Components;
        }
    }

    private sealed class DisposableModule(List<string> events) : IEngineModule, IDisposable
    {
        public bool Disposed { get; private set; }
        public DisposableService Service { get; } = new(events);

        public void ConfigureServices(EngineServiceContainer services) => services.Set(Service);

        public void Dispose()
        {
            Disposed = true;
            events.Add("module");
        }
    }

    private sealed class DisposableService(List<string> events) : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
            events.Add("service");
        }
    }

    private sealed class ThrowingModule : IEngineModule
    {
        public void ConfigureComponents(ComponentRegisterContext context) => throw new NotSupportedException("broken");
    }

    private sealed class TestBuilder(Action<SceneBuildContext>? build = null) : ISceneBuilder
    {
        public void Build(SceneBuildContext context) => build?.Invoke(context);
    }
}
