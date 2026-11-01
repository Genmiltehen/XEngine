using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Scenes;
using XEngine.Core.Services;
using XEngine.Core.Systems;
using XEngine.Core.Transforms;

namespace XEngine.Core.Tests.Scenes;

public sealed class SceneTests
{
    [Fact]
    public void ReplaceBeforeFirstUpdate_AppliesOnFirstUpdate()
    {
        using var manager = new SceneManager();
        EntityId entity = default;
        var builder = new TestBuilder(context =>
        {
            entity = context.World.Create();
            context.World.Add(entity, Transform2D.At(new Vector2(3, 4)));
        });
        manager.Replace(builder, "first");
        Assert.Null(manager.Current);
        Assert.Equal(0, manager.SceneCount);
        manager.Update(0.1f);
        Scene scene = Assert.IsType<Scene>(manager.Current);
        Assert.Equal("first", scene.Name);
        Assert.Equal(SceneState.Active, scene.State);
        Assert.Equal(1, builder.BuildCount);
        Assert.Equal(1, scene.World.Time.FrameCount);
        AssertNear(
            new Vector2(3, 4),
            Vector2.Transform(Vector2.Zero, scene.World.Get<Transform2D>(entity).World));
    }

    [Fact]
    public void Replace_DisposesPreviousScene_AndKeepsStackDepth()
    {
        using var manager = new SceneManager();
        manager.Replace(new TestBuilder(_ => { }), "first");
        manager.Update(0f);
        Scene previous = manager.Current!;
        manager.Replace(new TestBuilder(_ => { }), "second");
        manager.Update(0f);
        Scene current = manager.Current!;
        Assert.NotSame(previous, current);
        Assert.Equal("second", current.Name);
        Assert.Equal(SceneState.Disposed, previous.State);
        Assert.Equal(SceneState.Active, current.State);
        Assert.Equal(1, manager.SceneCount);
        Assert.Throws<ObjectDisposedException>(() => previous.World.Update(0f));
    }

    [Fact]
    public void Push_PausesLowerScene_AndPopResumesIt()
    {
        using var manager = new SceneManager();
        var first = new SceneWithCounter();
        var second = new SceneWithCounter();
        manager.Replace(first.Builder, "first");
        manager.Update(0.1f);
        Scene firstScene = manager.Current!;
        Assert.Equal(1, first.System.Calls);
        Assert.Equal(SceneState.Active, firstScene.State);
        manager.Push(second.Builder, "second");
        manager.Update(0.1f);
        Scene secondScene = manager.Current!;
        Assert.Equal(2, manager.SceneCount);
        Assert.Equal(SceneState.Paused, firstScene.State);
        Assert.Equal(SceneState.Active, secondScene.State);
        Assert.Equal(1, first.System.Calls);
        Assert.Equal(1, second.System.Calls);
        manager.Pop();
        manager.Update(0.1f);
        Assert.Equal(1, manager.SceneCount);
        Assert.Same(firstScene, manager.Current);
        Assert.Equal(SceneState.Active, firstScene.State);
        Assert.Equal(SceneState.Disposed, secondScene.State);
        Assert.Equal(2, first.System.Calls);
        Assert.Equal(1, second.System.Calls);
    }

    [Fact]
    public void Reload_RebuildsCurrentSceneFromItsBuilder()
    {
        using var manager = new SceneManager();
        var builder = new TestBuilder(context =>
        {
            context.World.Create();
        });
        manager.Replace(builder, "level");
        manager.Update(0f);
        World previousWorld = manager.Current!.World;
        manager.Reload();
        manager.Update(0f);
        Scene scene = manager.Current!;
        Assert.Equal(2, builder.BuildCount);
        Assert.Equal("level", scene.Name);
        Assert.Equal(1, manager.SceneCount);
        Assert.NotSame(previousWorld, scene.World);
        Assert.Throws<ObjectDisposedException>(() => previousWorld.Update(0f));
    }

    [Fact]
    public void Replace_OnlyReplacesTopScene_AndLeavesLowerScenesPaused()
    {
        using var manager = new SceneManager();
        manager.Replace(new TestBuilder(_ => { }), "bottom");
        manager.Update(0f);
        Scene bottom = manager.Current!;
        manager.Push(new TestBuilder(_ => { }), "middle");
        manager.Update(0f);
        Scene middle = manager.Current!;
        manager.Replace(new TestBuilder(_ => { }), "top");
        manager.Update(0f);
        Assert.Equal(2, manager.SceneCount);
        Assert.Equal(SceneState.Paused, bottom.State);
        Assert.Equal(SceneState.Disposed, middle.State);
        Assert.Equal("top", manager.Current!.Name);
        Assert.Equal(SceneState.Active, manager.Current.State);
    }

    [Fact]
    public void FailedBuild_KeepsCurrentSceneActive_AndDisposesFailedWorld()
    {
        using var manager = new SceneManager();
        manager.Replace(new TestBuilder(_ => { }), "good");
        manager.Update(0f);
        Scene current = manager.Current!;
        World? failedWorld = null;
        var failing = new TestBuilder(context =>
        {
            failedWorld = context.World;
            throw new InvalidOperationException("build failed");
        });
        manager.Replace(failing, "bad");
        Assert.Throws<InvalidOperationException>(() => manager.Update(0f));
        Assert.Same(current, manager.Current);
        Assert.Equal(SceneState.Active, current.State);
        Assert.Equal(1, manager.SceneCount);
        Assert.NotNull(failedWorld);
        Assert.Throws<ObjectDisposedException>(() => failedWorld.Update(0f));
        manager.Update(0.5f);
        Assert.Equal(2, current.World.Time.FrameCount);
    }

    [Fact]
    public void TransitionRequestedDuringUpdate_AppliesAtEndOfFrame()
    {
        using var manager = new SceneManager();
        var replacement = new TestBuilder(_ => { });
        var first = new TestBuilder(context =>
            context.World.AddSystem(
                SystemPhase.Update,
                new CountingSystem(() => manager.Replace(replacement, "replacement"))));
        manager.Replace(first, "first");
        manager.Update(0f);
        Scene current = manager.Current!;
        Assert.Equal("replacement", current.Name);
        Assert.Equal(SceneState.Active, current.State);
        Assert.Equal(1, manager.SceneCount);
        Assert.Equal(0, current.World.Time.FrameCount);
        manager.Update(0.2f);
        Assert.Equal(1, manager.Current!.World.Time.FrameCount);
    }

    [Fact]
    public void TransitionRequestedDuringBuild_IsAppliedAfterBuild()
    {
        using var manager = new SceneManager();
        var overlay = new TestBuilder(_ => { });
        World? firstWorld = null;
        var first = new TestBuilder(context =>
        {
            firstWorld = context.World;
            context.Scenes.Push(overlay, "overlay");
        });
        manager.Replace(first, "first");
        manager.Update(0f);
        Assert.Equal(2, manager.SceneCount);
        Assert.NotNull(firstWorld);
        Assert.NotSame(firstWorld, manager.Current!.World);
        Assert.Equal("overlay", manager.Current.Name);
        Assert.Equal(SceneState.Active, manager.Current.State);
        Assert.Equal(0, manager.Current.World.Time.FrameCount);
        Assert.Equal(1, firstWorld.Time.FrameCount);
        manager.Update(0.1f);
        Assert.Equal(1, manager.Current!.World.Time.FrameCount);
        Assert.Equal(1, firstWorld.Time.FrameCount);
    }

    [Fact]
    public void PopWithoutScenes_IsNoOp()
    {
        using var manager = new SceneManager();
        manager.Pop();
        manager.Update(0f);
        Assert.Null(manager.Current);
        Assert.Equal(0, manager.SceneCount);
    }

    [Fact]
    public void ReloadWithoutScene_ThrowsWhenTransitionIsApplied()
    {
        using var manager = new SceneManager();
        manager.Reload();
        Assert.Throws<InvalidOperationException>(() => manager.Update(0f));
        Assert.Null(manager.Current);
    }

    [Fact]
    public void BuildContext_ExposesWorldManagerServicesAndCancellation()
    {
        using var services = new EngineServiceContainer();
        var token = new TestService();
        services.Set(token);
        using var manager = new SceneManager(services);
        SceneBuildContext? captured = null;
        var builder = new TestBuilder(context => captured = context);
        manager.Replace(builder);
        manager.Update(0f);
        Assert.NotNull(captured);
        Assert.Same(manager.Current!.World, captured.World);
        Assert.Same(manager, captured.Scenes);
        Assert.Same(services, captured.Services);
        Assert.Same(token, captured.Services.Get<TestService>());
        Assert.False(captured.Cancellation.CanBeCanceled);
    }

    [Fact]
    public void Dispose_DisposesScenesTopDown_AndOwnedServices()
    {
        List<string> disposed = [];
        var manager = new SceneManager();
        var resource = new DisposableService();
        manager.Services.Set(resource);
        manager.Replace(new TestBuilder(context =>
            context.World.SetResource(new RecordingResource("bottom", disposed))), "bottom");
        manager.Update(0f);
        Scene bottom = manager.Current!;
        manager.Push(new TestBuilder(context =>
            context.World.SetResource(new RecordingResource("top", disposed))), "top");
        manager.Update(0f);
        Scene top = manager.Current!;
        manager.Dispose();
        manager.Dispose();
        Assert.Equal(["top", "bottom"], disposed);
        Assert.Equal(SceneState.Disposed, top.State);
        Assert.Equal(SceneState.Disposed, bottom.State);
        Assert.True(resource.Disposed);
        Assert.Equal(0, manager.SceneCount);
        Assert.Null(manager.Current);
        Assert.Throws<ObjectDisposedException>(() => manager.Update(0f));
        Assert.Throws<ObjectDisposedException>(() => manager.Replace(new TestBuilder(_ => { })));
    }

    [Fact]
    public void ExternallyProvidedServices_AreNotDisposedWithManager()
    {
        using var services = new EngineServiceContainer();
        var service = new TestService();
        services.Set(service);
        var manager = new SceneManager(services);
        manager.Dispose();
        Assert.False(service.Disposed);
        Assert.Same(service, services.Get<TestService>());
    }

    [Fact]
    public void CustomConfigurator_IsAppliedToEverySceneWorld()
    {
        var counter = new CountingSystem();
        using var manager = new SceneManager(
            configurator: world => world.AddSystem(SystemPhase.Update, counter));
        var builder = new TestBuilder(_ => { });
        manager.Replace(builder, "configured");
        manager.Update(0.1f);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public void SceneManagerFactoryMethods_QueueTransitionsUntilUpdate()
    {
        using var manager = new SceneManager();
        var builder = new TestBuilder(_ => { });
        manager.Replace(builder, "name");
        Assert.Equal(0, manager.SceneCount);
        manager.Update(0f);
        Assert.Equal(1, manager.SceneCount);
    }

    private static void AssertNear(Vector2 expected, Vector2 actual, float epsilon = 1e-4f)
    {
        Assert.InRange(actual.X, expected.X - epsilon, expected.X + epsilon);
        Assert.InRange(actual.Y, expected.Y - epsilon, expected.Y + epsilon);
    }

    private sealed class TestBuilder(Action<SceneBuildContext>? build = null) : ISceneBuilder
    {
        public int BuildCount { get; private set; }

        public void Build(SceneBuildContext context)
        {
            BuildCount++;
            build?.Invoke(context);
        }
    }

    private sealed class SceneWithCounter
    {
        public CountingSystem System { get; } = new();

        public ISceneBuilder Builder { get; }

        public SceneWithCounter()
        {
            Builder = new TestBuilder(context => context.World.AddSystem(SystemPhase.Update, System));
        }
    }

    private sealed class CountingSystem(Action? onUpdate = null) : ISystem
    {
        public int Calls { get; private set; }

        public void Update(World world, float dt)
        {
            Calls++;
            onUpdate?.Invoke();
        }
    }

    private sealed class TestService : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class DisposableService : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class RecordingResource(string name, List<string> disposed) : IDisposable
    {
        public void Dispose() => disposed.Add(name);
    }
}
