# Main Menu background: drone-shot flythrough (issue #51)

Replaces the old static single-house diorama (`Exterior`/`Roads`/`Fence`/
`Grass` in `MainMenu.unity`) with a slow camera flythrough over the real
Lobby scene, loaded additively behind the menu.

## Revision history (read this first)

The first version of this built an abstract stand-in neighborhood --
procedurally-ringed houses on a plain green plane, wandering police
stand-ins -- entirely in code, on the reasoning that additively loading
a real gameplay scene was too risky (see the issue's own feasibility
notes about `PoliceAI`). It worked with no errors, but real playtest
feedback was blunt: **it didn't look like the map at all**, which was
the entire point. Rebuilt around the issue's own "option 1" instead --
additively load a real scene -- using **Lobby**, not SampleScene, per
direct instruction once SampleScene was confirmed too heavy to chase.

Lobby turns out to sidestep every risk factor that ruled out SampleScene
in the first place, confirmed by reading `Lobby.unity` directly:

- Zero `PoliceAI`/`HomeownerAI` -- both `NetworkBehaviour`s that lean on
  a live `RoundManager`/server context Main Menu doesn't have. Lobby
  just doesn't have either.
- Zero `NetworkManager` and zero `AudioListener` in the scene file.
- Its ~19 scene-placed `NetworkIdentity` objects (shop items, pickups)
  already carry valid baked sceneIds from Lobby's own normal use as a
  real, already-shipping gameplay scene -- nothing new to bake.

## What changed, and why

`Assets/Scripts/UI/MenuBackgroundBuilder.cs` now just:

1. Disables the old diorama's roots (`Roads`, and the
   `Exterior`/`Fence`/`Grass` bundle) -- **disabled, not deleted**, still
   reversible, safe to delete for real once confirmed working.
2. Additively loads `Lobby` (`SceneManager.LoadSceneAsync(..., 
   LoadSceneMode.Additive)`) behind the Main Menu.
3. Watches for `NetworkServer.active`/`NetworkClient.active` going true
   (a player actually clicking Host/Join via `SteamLobby`) and
   immediately unloads *this specific* decorative Scene handle before
   Mirror's own real scene transition can load a second "Lobby"
   alongside it. Main Menu's own `NetworkManager` never auto-starts a
   server/client by itself -- only those two buttons do -- so this
   decorative copy sits completely inert (no `NetworkServer`/
   `NetworkClient` ever touches it) right up until that moment.

`MenuBackgroundCamera` (unchanged logic, new defaults) orbits around
`orbitCenter (-5, 0, -8)` at `orbitRadius 40` / `orbitHeight 28` --
worked out from Lobby's own `PlayerSpawnPoint`/`PawnShop`/`SellStation`
transforms (a roughly 40x45-unit footprint), not invented numbers. Much
closer and lower than the old ring's 110/65, appropriate for orbiting
one building instead of a sprawling neighborhood.

`MenuPoliceWander.cs` was deleted -- it was built specifically around
`Police.prefab`'s styling, which doesn't fit Lobby thematically (it's
the shop/hangout area, not a heist target). If the background ends up
looking dead/static once you've seen it, ambient movement (a player
skin or two wandering, reusing the same wander-loop technique) is an
easy fast-follow -- just not assumed here.

## Editor steps still needed

This is a genuinely new approach with real unknowns that need actual
eyes, not just tuning:

1. **Press Play and look at it.** Does Lobby's geometry actually show up
   behind the menu? Does it look right -- lighting in particular is
   worth a close look, since Lobby's own baked lighting/reflection
   probes (if any) are tied to it being the *active* scene, and it never
   is here (Main Menu stays active; Lobby only ever loads additively).
   If it looks flat or wrong, that's the first thing to check.
2. **Tune the orbit** (`MenuBackgroundCamera` on `Main Camera`) --
   `orbitCenter`/`orbitRadius`/`orbitHeight` are worked out from
   transform positions, not a real look at the space. Adjust until the
   camera reads as actually flying around the building instead of
   floating too far out or clipping through walls.
3. **Confirm the Host/Join unload actually works** -- click Host (or
   Join) from the Main Menu after the background has loaded, and check
   that the real Lobby loads cleanly with no duplicate-scene weirdness
   (missing shop items, doubled geometry, Mirror spawn warnings). This
   is the one piece of `MenuBackgroundBuilder` that's genuinely new
   runtime logic, not just "point it at different content."
4. **Confirm menu legibility** over the moving background -- buttons,
   title, and the character preview panel should all still read clearly
   with the dim overlay in place (`MenuBackgroundDimOverlay.dimAlpha`,
   starts at 0.45) per `main-menu-visual-design.md`'s composition
   regions.
5. **Cleanup**: once confirmed working, the old diorama objects
   (`Roads`, and the `Exterior`/`Fence`/`Grass` bundle, both referenced
   in `MenuBackgroundBuilder`'s `Legacy Diorama Roots` list) are safe to
   delete from the scene for real.

## Where to look

- `Assets/Scripts/UI/MenuBackgroundBuilder.cs` — loads/unloads Lobby.
- `Assets/Scripts/UI/MenuBackgroundCamera.cs` — the orbit flythrough.
- `Assets/Scripts/UI/MenuBackgroundDimOverlay.cs` — legibility overlay.
- `Assets/Scenes/Lobby.unity` — the real scene now providing the
  background (its `PawnShop`/`Interior`/`Outside`/`PlayerSpawnPoints`
  are the landmarks the camera orbit was sized against).
- `Assets/Scripts/Core/SteamLobby.cs` — `HostLobby`/`JoinLobby`, the
  moment real networking starts and the decorative copy needs to go.
