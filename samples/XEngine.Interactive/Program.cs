using XEngine.Common.Transform;
using XEngine.Core.Hosting;
using XEngine.Interactive;
using XEngine.Interactive.Demo;

var options = new SilkTestOptions();

using Engine engine = new EngineBuilder()
    .AddModule(new TransformModule())
    .AddModule(new SilkTestModule(options))
    .Build();

SilkGraphicsService graphics = engine.Services.Get<SilkGraphicsService>();
var clock = new Clock();

engine.Scenes.Replace(new DemoScene(), "demo");

while (!graphics.IsClosing)
{
    graphics.PumpEvents();
    if (graphics.IsClosing) break;

    float dt = MathF.Min(clock.Delta, 0.1f);
    engine.Update(dt);
}
