<#
.SYNOPSIS
    Adds a directory to the current user's persistent PATH environment variable, if it isn't
    already there. Invoked automatically by the LocalInstall publish profile after every publish.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Directory
)

$target = $Directory.TrimEnd('\')
$currentPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$entries = @()
if ($currentPath) {
    $entries = $currentPath -split ';' | Where-Object { $_ -ne '' }
}

$alreadyOnPath = $entries | Where-Object { $_.TrimEnd('\') -ieq $target }

if ($alreadyOnPath) {
    Write-Host "sb: '$target' is already on your user PATH."
} else {
    $newPath = if ($currentPath) { "$currentPath;$target" } else { $target }
    [Environment]::SetEnvironmentVariable('Path', $newPath, 'User')
    Write-Host "sb: added '$target' to your user PATH."
    Write-Host "sb: open a new terminal for the updated PATH to take effect."
}
