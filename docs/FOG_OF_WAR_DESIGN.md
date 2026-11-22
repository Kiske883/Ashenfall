# Future Fog of War design note

This is a design decision only. Milestone 08 adds no visibility, persistence or networking code.

Each player character will have three terrain visibility states: **unexplored** (terrain and enemies hidden), **explored but not currently visible** (terrain shown darkened, enemies hidden), and **currently visible** (terrain and enemies shown normally). Static obstacles such as trees, rocks and ruins should occlude line of sight where their geometry warrants it. An enemy's AI, collision and combat state must continue independently of whether a particular player's client renders that enemy.

The first adventure already separates the world, camera, enemy actors and atmosphere. A future visibility service can sample the world/navigation geometry and actor positions, maintain a per-character exploration mask plus a transient current-visibility mask, and pass those masks to terrain presentation and enemy visibility adapters. Atmospheric distance fog remains an Environment effect; it must not encode exploration state. Chunked bitsets or compact cell arrays can scale the exploration mask to larger maps. Cell size and line-of-sight sampling should be tuned against obstacle size and map scale.

For future multiplayer, the authoritative host should own gameplay-relevant visibility decisions and deliver only each player's permitted visibility updates. The planned ASP.NET Core/PostgreSQL layer can later store versioned, compact exploration masks per character and map; clients reconstruct rendering state from those masks. No database schema or API is introduced here.
