# One-time setup after cloning: Git LFS hooks + Unity SmartMerge for scenes/prefabs.
#   .\scripts\setup-git.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$merge = 'C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Data/Tools/UnityYAMLMerge.exe'

git -C $root lfs install --local
git -C $root lfs pull

if (Test-Path $merge) {
    git -C $root config merge.unityyamlmerge.name 'Unity SmartMerge'
    git -C $root config merge.unityyamlmerge.driver "'$merge' merge -h -p --force %O %B %A %A"
    git -C $root config merge.unityyamlmerge.recursive binary
    Write-Host 'Unity SmartMerge enabled.'
} else {
    Write-Warning "UnityYAMLMerge not found at $merge. Install Unity 6000.3.26f1 via Unity Hub, then re-run."
}
Write-Host 'Git setup done.'
