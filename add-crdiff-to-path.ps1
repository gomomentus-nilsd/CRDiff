$crdiffDir = "C:\Users\Nils.Drews\source\repos\Tools\CRDiff\CRDiff\bin\Debug"

$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")

if ($currentPath -split ';' -contains $crdiffDir) {
    Write-Output "Already on PATH: $crdiffDir"
} else {
    $newPath = "$currentPath;$crdiffDir"
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    Write-Output "Added to User PATH: $crdiffDir"
    Write-Output "Restart your terminal for it to take effect."
}
