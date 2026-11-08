#Requires -Version 7
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 aspctt

<#
.SYNOPSIS
	Generates the worlds in a test job inside RimWorld and compares them with a baseline.

.DESCRIPTION
	Builds Planetsmith and the test runner, installs both, writes a separate save data
	folder with the job's mod list, then starts RimWorld with -planetsmithtest. The
	runner generates each world, writes its statistics and closes the game. This script
	then reports what changed against tests/baselines/<job>.json.
	
	Steam has to be running, since Harmony and the other helper mods come from the
	Workshop. The player's own config, mod list, saves and installed copy of Planetsmith
	are never touched: the build under test is installed as a mod of its own.

.EXAMPLE
	./Run-Tests.ps1
	./Run-Tests.ps1 -Job determinism
	./Run-Tests.ps1 -UpdateBaseline
#>

param(
	# A file in tests/jobs, without the extension.
	[string]$Job = "baseline",
	[string]$GameDir = "S:\SteamLibrary\steamapps\common\RimWorld",
	# Save this run as the job's new baseline.
	[switch]$UpdateBaseline,
	[int]$TimeoutMinutes = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

#### Constants

$REPO_ROOT = Split-Path $PSScriptRoot -Parent
$TESTS_ROOT = $PSScriptRoot
$OUTPUT_ROOT = Join-Path $TESTS_ROOT "output"
$SAVE_DATA = Join-Path $OUTPUT_ROOT "savedata"
$TEST_FOLDER = Join-Path $SAVE_DATA "PlanetsmithTest"
$RUNNER_INSTALL = Join-Path $GameDir "Mods\planetsmith-test-runner"

# The build under test is installed as its own mod under its own package id. Under
# Planetsmith's id it would clash with the player's installed copy, and the game turns
# away the second of two local mods sharing an id, which could be the player's own.
# Profiles name Planetsmith by its real id, and runs use this one in its place.
$PLANETSMITH_ID = "aspctt.planetsmith"
$TEST_BUILD_ID = "aspctt.planetsmith.testbuild"
$TEST_BUILD_INSTALL = Join-Path $GameDir "Mods\planetsmith-test-build"
$GAME_EXE = Join-Path $GameDir "RimWorldWin64.exe"

# Differences smaller than these are reported as unchanged. Shares are fractions of
# land, so 0.005 is half a percentage point.
$SHARE_TOLERANCE = 0.005
$RAINFALL_TOLERANCE = 0.03
$TEMPERATURE_TOLERANCE = 0.2

# How much slower Planetsmith's passes may get before a run says so, as a fraction, and
# the least time that counts. Timings vary a lot with whatever else the computer is
# doing, so this only warns, and only when Planetsmith's share of the whole generation
# grew as well: a busy computer slows every step alike and leaves the share unchanged.
$SPEED_TOLERANCE = 0.25
$SPEED_MINIMUM_MS = 50

#### Private Functions

function Write-Step([string]$Text) {
	Write-Host "== $Text" -ForegroundColor Cyan
}

# Mirrors a folder into the game's Mods, so nothing stale is left behind.
function Install-Mod([string]$Source, [string]$Destination) {
	robocopy $Source $Destination /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
	if ($LASTEXITCODE -ge 8) {
		throw "Installing $Destination failed (robocopy exit code $LASTEXITCODE)."
	}
}

# Reads a profile's package ids, leaving out comments and expansions that are not installed.
function Read-Profile([string]$Name) {
	$path = Join-Path $TESTS_ROOT "profiles\$Name.txt"
	if (-not (Test-Path $path)) {
		throw "No profile named '$Name' in tests/profiles."
	}
	
	$installed = Get-ChildItem (Join-Path $GameDir "Data") -Directory | ForEach-Object Name
	$expansions = @{
		"ludeon.rimworld.royalty" = "Royalty"
		"ludeon.rimworld.ideology" = "Ideology"
		"ludeon.rimworld.biotech" = "Biotech"
		"ludeon.rimworld.anomaly" = "Anomaly"
		"ludeon.rimworld.odyssey" = "Odyssey"
	}
	$ids = foreach ($line in Get-Content $path) {
		$id = $line.Trim()
		if ($id -eq "" -or $id.StartsWith("#")) {
			continue
		}
		if ($expansions.ContainsKey($id) -and $installed -notcontains $expansions[$id]) {
			continue
		}
		$id = $id.ToLowerInvariant()
		if ($id -eq $PLANETSMITH_ID) {
			$id = $TEST_BUILD_ID
		}
		$id
	}
	return $ids
}

function Write-ModsConfig([string[]]$Ids) {
	# Same layout as the game's own ModsConfig.xml. knownExpansions stops the game from
	# announcing each expansion as newly installed on the first start.
	$version = (Get-Content (Join-Path $GameDir "Version.txt") -Raw).Trim()
	$lines = @('<?xml version="1.0" encoding="utf-8"?>', "<ModsConfigData>", "`t<version>$version</version>", "`t<activeMods>")
	$lines += $Ids | ForEach-Object { "`t`t<li>$_</li>" }
	$lines += @("`t</activeMods>", "`t<knownExpansions>")
	$lines += $Ids | Where-Object { $_.StartsWith("ludeon.rimworld.") } | ForEach-Object { "`t`t<li>$_</li>" }
	$lines += @("`t</knownExpansions>", "</ModsConfigData>")
	
	$config = Join-Path $SAVE_DATA "Config"
	New-Item -ItemType Directory -Force $config | Out-Null
	Set-Content -Path (Join-Path $config "ModsConfig.xml") -Value $lines -Encoding utf8NoBOM
}

function Format-Share([double]$Value) {
	return "{0:P1}" -f $Value
}

# Lists what moved between a baseline world and a new one, biggest changes first.
function Compare-World($Old, $New) {
	$changes = [System.Collections.Generic.List[object]]::new()
	
	function Add-Change([string]$Where, [string]$What, [string]$Before, [string]$After, [double]$Size) {
		$changes.Add([pscustomobject]@{ Where = $Where; What = $What; Before = $Before; After = $After; Size = $Size })
	}
	
	# Compare two lists of biome shares, including biomes only one side has
	function Compare-Biomes([string]$Where, $OldBiomes, $NewBiomes) {
		$before = @{}
		$after = @{}
		foreach ($entry in @($OldBiomes)) { $before[$entry.biome] = [double]$entry.share }
		foreach ($entry in @($NewBiomes)) { $after[$entry.biome] = [double]$entry.share }
		foreach ($biome in @($before.Keys) + @($after.Keys) | Sort-Object -Unique) {
			$oldShare = $before[$biome] ?? 0.0
			$newShare = $after[$biome] ?? 0.0
			if ([math]::Abs($newShare - $oldShare) -ge $SHARE_TOLERANCE) {
				Add-Change $Where $biome (Format-Share $oldShare) (Format-Share $newShare) ([math]::Abs($newShare - $oldShare))
			}
		}
	}
	
	# The planet as a whole
	if ([math]::Abs($New.meanRainfall - $Old.meanRainfall) -ge $Old.meanRainfall * $RAINFALL_TOLERANCE) {
		Add-Change "planet" "mean rainfall" ("{0:F0}mm" -f $Old.meanRainfall) ("{0:F0}mm" -f $New.meanRainfall) 1
	}
	if ([math]::Abs($New.meanTemperature - $Old.meanTemperature) -ge $TEMPERATURE_TOLERANCE) {
		Add-Change "planet" "mean temperature" ("{0:F1}C" -f $Old.meanTemperature) ("{0:F1}C" -f $New.meanTemperature) 1
	}
	if ([math]::Abs($New.riverShare - $Old.riverShare) -ge $SHARE_TOLERANCE) {
		Add-Change "planet" "land with rivers" (Format-Share $Old.riverShare) (Format-Share $New.riverShare) 1
	}
	Compare-Biomes "planet" $Old.biomes $New.biomes
	
	# Each band of latitude
	for ($index = 0; $index -lt $New.bands.Count; $index++) {
		$oldBand = $Old.bands[$index]
		$newBand = $New.bands[$index]
		if ($oldBand.landTiles -eq 0 -or $newBand.landTiles -eq 0) {
			continue
		}
		
		$where = $newBand.band
		if ([math]::Abs($newBand.meanRainfall - $oldBand.meanRainfall) -ge $oldBand.meanRainfall * $RAINFALL_TOLERANCE) {
			Add-Change $where "mean rainfall" ("{0:F0}mm" -f $oldBand.meanRainfall) ("{0:F0}mm" -f $newBand.meanRainfall) 0.5
		}
		foreach ($field in "aridShare", "soakedShare", "waterloggedShare") {
			$difference = [math]::Abs($newBand.$field - $oldBand.$field)
			if ($difference -ge $SHARE_TOLERANCE * 2) {
				Add-Change $where ($field -replace "Share$", "") (Format-Share $oldBand.$field) (Format-Share $newBand.$field) $difference
			}
		}
		Compare-Biomes $where $oldBand.biomes $newBand.biomes
	}
	
	return $changes
}

# Warns when Planetsmith's passes got slower than in the baseline, naming the passes that did.
function Compare-Speed($Old, $New) {
	# Baselines from before pass timings were recorded have none to compare
	if (-not $Old.PSObject.Properties["planetsmithMilliseconds"] -or $Old.planetsmithMilliseconds -le 0 -or $Old.seconds -le 0 -or $New.seconds -le 0) {
		return
	}
	
	# Compare the total and Planetsmith's share of the whole generation
	$added = $New.planetsmithMilliseconds - $Old.planetsmithMilliseconds
	$growth = $New.planetsmithMilliseconds / $Old.planetsmithMilliseconds - 1
	$shareGrowth = ($New.planetsmithMilliseconds / $New.seconds) / ($Old.planetsmithMilliseconds / $Old.seconds) - 1
	if ($added -lt $SPEED_MINIMUM_MS -or $growth -lt $SPEED_TOLERANCE -or $shareGrowth -lt $SPEED_TOLERANCE) {
		return
	}
	Write-Host ("{0}: Planetsmith's passes took {1}ms against the baseline's {2}ms (+{3:P0}), a larger share of the whole generation." -f $New.name, $New.planetsmithMilliseconds, $Old.planetsmithMilliseconds, $growth) -ForegroundColor Yellow
	
	# Name the passes that slowed
	foreach ($pass in $New.passes) {
		$oldPass = $Old.passes | Where-Object pass -eq $pass.pass | Select-Object -First 1
		if (-not $oldPass -or $oldPass.milliseconds -le 0) {
			continue
		}
		$passAdded = $pass.milliseconds - $oldPass.milliseconds
		if ($passAdded -ge $SPEED_MINIMUM_MS / 2 -and $pass.milliseconds / $oldPass.milliseconds - 1 -ge $SPEED_TOLERANCE) {
			Write-Host ("  {0}: {1}ms against {2}ms" -f $pass.pass, $pass.milliseconds, $oldPass.milliseconds) -ForegroundColor Yellow
		}
	}
}

#### Run

# Check the job and the machine
$jobPath = Join-Path $TESTS_ROOT "jobs\$Job.xml"
if (-not (Test-Path $jobPath)) {
	throw "No job named '$Job' in tests/jobs."
}
$jobProfile = ([xml](Get-Content $jobPath -Raw)).TestJob.profile
if (-not (Get-Process steam -ErrorAction SilentlyContinue)) {
	throw "Steam is not running. Harmony and the helper mods load from the Workshop, so start Steam first."
}
if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) {
	throw "RimWorld is already running. Close it first."
}

# Build Planetsmith and the runner
# The runner's project builds Planetsmith first, since it references it.
Write-Step "Building"
$buildOutput = dotnet build -c Release (Join-Path $TESTS_ROOT "Runner\PlanetsmithTestRunner.csproj") --nologo -v quiet 2>&1
if ($LASTEXITCODE -ne 0) {
	$buildOutput | Out-Host
	throw "Build failed."
}

# Install the build under test under its own id
# The Workshop id file is left out, so the game never takes the copy for the
# published item.
Install-Mod (Join-Path $REPO_ROOT "Mod") $TEST_BUILD_INSTALL
Remove-Item (Join-Path $TEST_BUILD_INSTALL "About\PublishedFileId.txt") -ErrorAction SilentlyContinue
$aboutPath = Join-Path $TEST_BUILD_INSTALL "About\About.xml"
$about = (Get-Content $aboutPath -Raw).Replace("<packageId>$PLANETSMITH_ID</packageId>", "<packageId>$TEST_BUILD_ID</packageId>").Replace("<name>Planetsmith</name>", "<name>Planetsmith (test build)</name>")
if (-not $about.Contains("<packageId>$TEST_BUILD_ID</packageId>")) {
	throw "Could not give the test build its own package id in $aboutPath."
}
Set-Content -Path $aboutPath -Value $about -NoNewline -Encoding utf8NoBOM

# Install the runner
# It only does anything when the game is started with -planetsmithtest.
Install-Mod (Join-Path $TESTS_ROOT "RunnerMod") $RUNNER_INSTALL

# Prepare the save data folder
Write-Step "Preparing job '$Job' with profile '$jobProfile'"
Write-ModsConfig (Read-Profile $jobProfile)
New-Item -ItemType Directory -Force $TEST_FOLDER | Out-Null
Copy-Item $jobPath (Join-Path $TEST_FOLDER "job.xml") -Force

# Install the test presets where Planetsmith keeps presets, replacing any from an earlier run
$presetFolder = Join-Path $SAVE_DATA "Planetsmith\Presets"
New-Item -ItemType Directory -Force $presetFolder | Out-Null
Remove-Item (Join-Path $presetFolder "*.xml") -ErrorAction SilentlyContinue
Copy-Item (Join-Path $TESTS_ROOT "presets\*.xml") $presetFolder
$resultsPath = Join-Path $TEST_FOLDER "results.json"
$donePath = Join-Path $TEST_FOLDER "done"
Remove-Item $resultsPath, $donePath -ErrorAction SilentlyContinue

# Run the game
# -logFile keeps this run's Player.log out of the one the player's own game writes. The
# window is kept small, since nobody needs to watch it.
Write-Step "Starting RimWorld"
$logPath = Join-Path $OUTPUT_ROOT "Player.log"
$arguments = "-savedatafolder=$SAVE_DATA -planetsmithtest -logFile `"$logPath`" -screen-fullscreen 0 -screen-width 1280 -screen-height 720"
$started = Get-Date
$game = Start-Process $GAME_EXE -ArgumentList $arguments -WorkingDirectory $GameDir -PassThru

# Wait for the runner to finish, or the game to close on its own after a failure
# Once the runner is done the game is closed here: its own quit has taken over three
# minutes after a finished run, and nothing in this save data folder needs saving.
$deadline = $started.AddMinutes($TimeoutMinutes)
while (-not $game.HasExited -and -not (Test-Path $donePath)) {
	if ((Get-Date) -gt $deadline) {
		Stop-Process -Id $game.Id -Force
		throw "RimWorld did not finish within $TimeoutMinutes minutes and was closed. See $logPath."
	}
	Start-Sleep -Seconds 1
}
Write-Host ("Finished in {0:F0}s." -f ((Get-Date) - $started).TotalSeconds)
if (-not $game.WaitForExit(10000)) {
	Stop-Process -Id $game.Id -Force
	$game.WaitForExit()
}

# Read the results
if (-not (Test-Path $resultsPath)) {
	throw "The runner wrote no results. See $logPath."
}
$results = Get-Content $resultsPath -Raw | ConvertFrom-Json
$resultsCopy = Join-Path $OUTPUT_ROOT ("results\{0}-{1:yyyyMMdd-HHmmss}.json" -f $Job, $started)
New-Item -ItemType Directory -Force (Split-Path $resultsCopy) | Out-Null
Copy-Item $resultsPath $resultsCopy
$isFailed = $false
if (-not $results.isComplete) {
	Write-Host "The run did not finish: $($results.failure)" -ForegroundColor Red
	$isFailed = $true
}

# Summarize each world
Write-Step "Worlds"
$results.worlds | Format-Table -AutoSize @(
	@{ Label = "World"; Expression = { "$($_.name) #$($_.repeat)" } },
	@{ Label = "Seconds"; Expression = { "{0:F1}" -f $_.seconds } },
	@{ Label = "Passes"; Expression = { "$($_.planetsmithMilliseconds)ms" } },
	@{ Label = "Land"; Expression = { $_.landTiles } },
	@{ Label = "Rain"; Expression = { "{0:F0}mm" -f $_.meanRainfall } },
	@{ Label = "Temp"; Expression = { "{0:F1}C" -f $_.meanTemperature } },
	@{ Label = "Errors"; Expression = { $_.errorCount } },
	@{ Label = "Tiles"; Expression = { $_.tileFingerprint } },
	@{ Label = "Climate"; Expression = { $_.climateFingerprint } }
) | Out-Host
foreach ($world in $results.worlds | Where-Object errorCount -gt 0) {
	Write-Host "Errors in $($world.name) #$($world.repeat):" -ForegroundColor Red
	$world.errors | ForEach-Object { Write-Host "  $($_.Split("`n")[0])" }
	$isFailed = $true
}

# Check that repeats of a world came out identical
foreach ($group in $results.worlds | Group-Object name | Where-Object Count -gt 1) {
	$fingerprints = $group.Group | ForEach-Object { "$($_.tileFingerprint)/$($_.climateFingerprint)" } | Sort-Object -Unique
	if (@($fingerprints).Count -eq 1) {
		Write-Host "$($group.Name): all $($group.Count) repeats identical." -ForegroundColor Green
	}
	else {
		Write-Host "$($group.Name): repeats differ, so this seed is not deterministic." -ForegroundColor Red
		$isFailed = $true
	}
}

# Check what survived saving and loading
foreach ($world in $results.worlds | Where-Object { $_.roundTrip.isChecked }) {
	$trip = $world.roundTrip
	$label = "$($world.name) #$($world.repeat) save and load"
	if ($trip.tilesChanged -eq 0) {
		Write-Host ("{0}: all {1} tiles came back as generated ({2:F1}s)." -f $label, $trip.tilesChecked, $trip.seconds) -ForegroundColor Green
	}
	else {
		$fields = "elevation", "hilliness", "temperature", "rainfall", "swampiness", "biome", "rivers" | Where-Object { $trip.$_ -gt 0 } | ForEach-Object { "$_ $($trip.$_)" }
		Write-Host "${label}: $($trip.tilesChanged) of $($trip.tilesChecked) tiles changed ($($fields -join ', '))." -ForegroundColor Red
		$isFailed = $true
	}
	
	# Planetsmith's climate fields have to come back within their 16-bit storage
	if (-not $trip.climateRestored) {
		Write-Host "${label}: Planetsmith's climate fields did not come back, so the map modes that need them stay unavailable." -ForegroundColor Red
		$isFailed = $true
	}
	elseif ($trip.climateValuesChanged -gt 0) {
		Write-Host "${label}: $($trip.climateValuesChanged) climate field values came back further off than their storage allows." -ForegroundColor Red
		$isFailed = $true
	}
	else {
		Write-Host "${label}: Planetsmith's climate fields came back as generated." -ForegroundColor Green
	}
	
	# A save from before the fields were stored has to load and leave them empty
	if ($trip.oldSaveChecked -and $trip.oldSaveHasNoClimate) {
		Write-Host "${label}: a save without the climate fields still loads, with those map modes unavailable." -ForegroundColor Green
	}
	elseif ($trip.oldSaveChecked) {
		Write-Host "${label}: a save without the climate fields loaded with climate fields anyway." -ForegroundColor Red
		$isFailed = $true
	}
}

# Compare with the baseline
$baselinePath = Join-Path $TESTS_ROOT "baselines\$Job.json"
if (Test-Path $baselinePath) {
	$baseline = Get-Content $baselinePath -Raw | ConvertFrom-Json
	Write-Step "Against the baseline"
	if ($baseline.gameVersion -ne $results.gameVersion) {
		Write-Host "Baseline came from game $($baseline.gameVersion), this run from $($results.gameVersion)." -ForegroundColor Yellow
	}
	if (($baseline.mods -join ",") -ne ($results.mods -join ",")) {
		Write-Host "Baseline used a different mod list, so differences may come from that." -ForegroundColor Yellow
	}
	foreach ($world in $results.worlds | Where-Object repeat -eq 1) {
		$old = $baseline.worlds | Where-Object { $_.name -eq $world.name -and $_.repeat -eq 1 } | Select-Object -First 1
		if (-not $old) {
			Write-Host "$($world.name): not in the baseline." -ForegroundColor Yellow
			continue
		}
		Compare-Speed $old $world
		if ($old.tileFingerprint -eq $world.tileFingerprint -and $old.climateFingerprint -eq $world.climateFingerprint) {
			Write-Host "$($world.name): identical." -ForegroundColor Green
			continue
		}
		
		$changes = Compare-World $old $world
		if ($changes.Count -eq 0) {
			Write-Host "$($world.name): changed, but everything stayed within tolerance." -ForegroundColor Yellow
			continue
		}
		Write-Host "$($world.name): changed." -ForegroundColor Yellow
		$changes | Sort-Object Size -Descending | Format-Table -AutoSize Where, What, Before, After | Out-Host
	}
}
else {
	Write-Host "No baseline for '$Job' yet." -ForegroundColor Yellow
}

# Keep this run as the baseline when asked, or when there is none yet
if ($results.isComplete -and ($UpdateBaseline -or -not (Test-Path $baselinePath))) {
	Copy-Item $resultsPath $baselinePath -Force
	Write-Host "Saved as the baseline for '$Job'."
}
Write-Host "Results: $resultsCopy"
# robocopy leaves a nonzero exit code even when it succeeds, so the result is set here
exit ($isFailed ? 1 : 0)
