# Ashenfall project rules

- Work incrementally within the requested milestone. Read the current project and docs/MILESTONE_01.md before changes.
- Keep Godot 4.7.2 .NET / Godot.NET.Sdk 4.7.2 and .NET 8 compatibility. Preserve Forward+ unless a change is explicitly required. Do not install dependencies or modify global system configuration.
- Keep main world composition in scenes/world, reusable environment scenes in scenes/environment, and their C# logic in scripts/world and scripts/environment. Use resources for native reusable configuration and assets for future source assets.
- Keep generation deterministic with a local configurable seed and exported counts/settings. Separate camera, environment, and future actors; avoid singleton coupling and unnecessary frameworks.
- Use C# for game logic, native .tscn/.tres for scenes/resources. The GDScript screenshot utility is validation tooling only.
- Keep the camera fixed, orthographic, and isometric with configurable inclination, azimuth, distance, and framing.
- Do not add characters, combat, inventory, networking, or backend features without a subsequent scoped request. Two-player support is future scope, not currently implemented.
- Inspect git status before editing and preserve unrelated changes. If the directory is not a repository, report it; do not initialize one without instruction.
- Never delete existing files, overwrite unrelated work, commit automatically, push, merge, rebase, or perform destructive Git operations. Keep task changes inside the project directory.
- Run dotnet build after code changes. Use available Godot executables to import headlessly and validate startup, with bounded execution. Document failures and unperformed checks honestly.
- Claim visual verification only after rendering and inspecting an image. On this machine, Vulkan Forward+ works; the existing Direct3D 12 setting triggers automatic fallback on the GTX 650.
- Update milestone documentation with architecture, changed files, exact launch steps, validation, and limitations.
