using XEngine.Core.Ecs;
using XEngine.Core.Hosting;
using XEngine.Core.Modules;
using XEngine.Core.Scenes;
using XEngine.Core.Services;
using XEngine.Core.Systems;
using XEngine.Core.Tests.Ecs;
using XEngine.Common.Transform;

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

        Assert.Equal(["components", "services", "created"], events);
        Assert.True(module.RegistryFrozenBeforeServices);
        Assert.True(module.ServicesAvailableInOnEngineCreated);
        Assert.True(module.ScenesEmptyInOnEngineCreated);
        Assert.Equal(1, module.CreatedHookCalls);
        Assert.True(registry.IsFrozen);
        Assert.Same(registry, engine.Components);

        engine.Scenes.Replace(new TestBuilder(_ => { }), "test");
        engine.Update(0f);

        Assert.Equal(["components", "services", "created", "world"], events);
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
    public void Build_DefaultBuilder_RegistersOnlyCoreComponents()
    {
        using Engine engine = new EngineBuilder().Build();

        Assert.True(engine.Components.IsFrozen);
        Assert.Equal(1, engine.Components.Count);
        Assert.Equal("core.Node", engine.Components.GetById(0).Name);
    }

    [Fact]
    public void Build_TransformModule_RegistersAndUpdatesTransforms()
    {
        using Engine engine = new EngineBuilder().AddModule(new TransformModule()).Build();
        Assert.Equal(2, engine.Components.Count);
        Assert.True(engine.Components.TryGet("transform.Transform2D", out ComponentType transform));
        Assert.Equal(typeof(Transform2D), transform.RuntimeType);

        EntityId entity = default;
        engine.Scenes.Replace(new TestBuilder(context =>
        {
            entity = context.World.Create();
            context.World.Add(entity, Transform2D.At(new System.Numerics.Vector2(3, 4)));
        }), "test");
        engine.Update(0f);

        Assert.Equal(
            new System.Numerics.Vector2(3, 4),
            System.Numerics.Vector2.Transform(System.Numerics.Vector2.Zero, engine.Scenes.Current!.World.Get<Transform2D>(entity).World));
    }

    [Fact]
    public void Dispose_RunsOnEngineDisposingThenScenesModulesAndServices_OnlyOnce()
    {
        List<string> events = [];
        var module = new DisposableModule(events);
        Engine engine = new EngineBuilder().AddModule(module).Build();
        var sceneResource = new DisposableResource(events);
        engine.Scenes.Replace(new TestBuilder(context => context.World.SetResource(sceneResource)), "test");
        engine.Update(0f);

        engine.Dispose();
        engine.Dispose();

        Assert.True(module.Disposed);
        Assert.True(module.Service.Disposed);
        Assert.True(module.ServicesAvailableInOnEngineDisposing);
        Assert.True(sceneResource.Disposed);
        Assert.Equal(["disposing", "scene", "module", "service"], events);
        Assert.Equal(1, module.DisposeHookCalls);
    }

    [Fact]
    public void Dispose_OnEngineDisposingFailure_StillReleasesScenesModulesAndServices()
    {
        List<string> events = [];
        var module = new FailingDisposingModule(events);
        Engine engine = new EngineBuilder().AddModule(module).Build();
        var sceneResource = new DisposableResource(events);
        engine.Scenes.Replace(new TestBuilder(context => context.World.SetResource(sceneResource)), "test");
        engine.Update(0f);

        var exception = Assert.Throws<InvalidOperationException>(() => engine.Dispose());

        Assert.Contains("FailingDisposingModule failed in OnEngineDisposing", exception.Message);
        Assert.IsType<NotSupportedException>(exception.InnerException);
        Assert.True(sceneResource.Disposed);
        Assert.True(module.Disposed);
        Assert.True(module.Service.Disposed);
        Assert.Throws<ObjectDisposedException>(() => engine.Services.Get<DisposableService>());
    }

    [Fact]
    public void Build_OnEngineCreatedFailure_DisposesServicesAndWraps()
    {
        var module = new FailingCreatedModule();
        var exception = Assert.Throws<InvalidOperationException>(() => new EngineBuilder()
            .AddModule(module)
            .Build());

        Assert.Contains("FailingCreatedModule failed in OnEngineCreated", exception.Message);
        Assert.IsType<NotSupportedException>(exception.InnerException);
        Assert.True(module.Service.Disposed);
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
        Assert.Equal(1, engine.Components.Count);
    }

    private sealed class RecordingModule(List<string> events, ComponentRegistry registry) : IEngineModule
    {
        public bool RegistryFrozenBeforeServices { get; private set; }
        public bool ServicesAvailableInOnEngineCreated { get; private set; }
        public bool ScenesEmptyInOnEngineCreated { get; private set; }
        public int CreatedHookCalls { get; private set; }
        public ComponentRegistry? WorldComponents { get; private set; }
        public MarkerService Service { get; } = new();

        public void ConfigureComponents(ComponentRegisterContext context)
        {
            events.Add("components");
            context.Register<Health>("game.Health");
        }

        public void ConfigureServices(EngineServiceContainer services)
        {
            events.Add("services");
            services.Set(Service);
            RegistryFrozenBeforeServices = registry.IsFrozen;
        }

        public void OnEngineCreated(Engine engine)
        {
            CreatedHookCalls++;
            events.Add("created");
            ServicesAvailableInOnEngineCreated = ReferenceEquals(engine.Services.Get<MarkerService>(), Service);
            ScenesEmptyInOnEngineCreated = engine.Scenes.Current is null;
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
        public bool ServicesAvailableInOnEngineDisposing { get; private set; }
        public int DisposeHookCalls { get; private set; }
        public DisposableService Service { get; } = new(events);

        public void ConfigureServices(EngineServiceContainer services) => services.Set(Service);

        public void OnEngineDisposing(Engine engine)
        {
            DisposeHookCalls++;
            events.Add("disposing");
            ServicesAvailableInOnEngineDisposing = engine.Services.TryGet<DisposableService>(out _);
        }

        public void Dispose()
        {
            Disposed = true;
            events.Add("module");
        }
    }

    private sealed class FailingCreatedModule : IEngineModule
    {
        public DisposableService Service { get; } = new([]);

        public void ConfigureServices(EngineServiceContainer services) => services.Set(Service);

        public void OnEngineCreated(Engine engine) => throw new NotSupportedException("broken");
    }

    private sealed class FailingDisposingModule(List<string> events) : IEngineModule, IDisposable
    {
        public bool Disposed { get; private set; }
        public DisposableService Service { get; } = new(events);

        public void ConfigureServices(EngineServiceContainer services) => services.Set(Service);

        public void OnEngineDisposing(Engine engine) => throw new NotSupportedException("broken");

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

    private sealed class DisposableResource(List<string> events) : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
            events.Add("scene");
        }
    }

    private sealed class MarkerService { }

    private sealed class ThrowingModule : IEngineModule
    {
        public void ConfigureComponents(ComponentRegisterContext context) => throw new NotSupportedException("broken");
    }

    private sealed class TestBuilder(Action<SceneBuildContext>? build = null) : ISceneBuilder
    {
        public void Build(SceneBuildContext context) => build?.Invoke(context);
    }
}
