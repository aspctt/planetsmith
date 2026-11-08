# Planetsmith tests

`Run-Tests.ps1` generates worlds inside RimWorld and compares their statistics with a saved baseline. It builds Planetsmith and the test runner, starts the game with a separate save data folder and closes it again once the worlds are measured. A run of the baseline job takes about a minute.

```powershell
./tests/Run-Tests.ps1                     # the baseline job
./tests/Run-Tests.ps1 -Job determinism    # the same world twice, which must match
./tests/Run-Tests.ps1 -DeployPlanetsmith  # copy the fresh build over the installed copy first
./tests/Run-Tests.ps1 -UpdateBaseline     # keep this run as the new baseline
```

Steam has to be running, since Harmony and the helper mods load from the Workshop. The game loads the Planetsmith installed in its Mods folder, so the script stops when that copy differs from the build in `Mod/`, unless `-DeployPlanetsmith` is given. The runner installs itself to `Mods/planetsmith-test-runner` and does nothing unless the game is started with `-planetsmithtest`. The player's own config, mod list and saves are never touched.

## Layout

- `jobs/` holds the jobs. Each names a mod profile and lists the worlds to generate, with the settings the world creation page offers.
- `profiles/` holds the mod lists, one package id per line in load order.
- `baselines/` holds the last accepted result of each job. The first run of a job saves one.
- `Runner/` is the source of the runner mod, and `RunnerMod/` the mod folder it builds into.
- `output/` holds the test save data, the game's log and every run's results. It is not tracked.

## Reading the results

Each world gets two fingerprints. The tile fingerprint hashes every tile's final elevation, hilliness, temperature, rainfall, swampiness, biome and rivers. The climate fingerprint hashes the fields Planetsmith works out during generation for the map modes. Matching fingerprints mean the worlds are identical.

When a world changed, the script lists what moved by more than its tolerance: biome shares over the planet and in each 10 degree band, mean rainfall and temperature, the arid, soaked and waterlogged shares and the land with rivers. Errors logged by any mod during generation fail the run.
