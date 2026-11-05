# Legacy reference archive

Local-only archive of the pre-ECS `XEngine` prototype. It is **not** part of
`XEngine.slnx`, is not compiled, and is ignored by git.

Full original sources and assets are available in git history at commit
`3ac02cd` (`init`), e.g.:

```bash
git show 3ac02cd:XEngine/Input/InputManager.cs
git show 3ac02cd:Assets/config.json
```

## Kept

- `Input/` — action bindings, `JustPressed/JustReleased` edge detection,
  analog axes with sensitivity/gravity. `InputSystem.cs` was only a thin
  abstract wrapper and was not kept.
- `Config/` — `GameConfig`, `ConfigManager`, and `config.example.json`.
- `Gameplay/` — `GameTimer`, health/regen examples, lifetime/blink examples.
- `Shaders/` — GLSL 330 sources for the old OpenGL renderer. They are not
  OpenTK-specific and can be reused with Silk.NET's OpenGL bindings.

## Not kept

- The old OOP core (`Entity`, `GameComponent`, `GTransform`, `GScene`,
  `GameEngine`, old `SceneManager`) — replaced by the current ECS.
- The OpenTK rendering layer (`Shader`, `Texture2D`, `UnitQuad`, `GLProvider`,
  `AssetManager`, `RenderPipeline`, sprite/nine-slice renderers) — the renderer
  will be rewritten on Silk.NET.
- `MathUtils` and the OpenTK vector JSON converters.
- GunFight textures, the old `XEngine/XEngine.Core.csproj`, and `GunFight.sln`.

## Caveats

- This code is reference-only and does not compile as a standalone project.
- `Input` still uses OpenTK's `Keys` enum; replace it with a backend-neutral key
  enum when porting.
- `Axis` calls `MathUtils.MoveToward`, which was not archived; reimplement it
  with a small local helper.
- `Gameplay` files reference the old `GameComponent`/`GScene` model and
  `MathUtils`, so they are examples, not drop-in code.
