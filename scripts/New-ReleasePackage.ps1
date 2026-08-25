[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$SptRoot,

    [string]$OutputDirectory,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '2.0.0',

    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$SptVersion = '4.1.2'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Add-DeterministicZipEntry {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Archive,

        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$EntryName
    )

    $entry = $Archive.CreateEntry($EntryName, [System.IO.Compression.CompressionLevel]::NoCompression)
    $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
    $inputStream = [System.IO.File]::OpenRead($SourcePath)
    $outputStream = $entry.Open()

    try {
        $inputStream.CopyTo($outputStream)
    }
    finally {
        $outputStream.Dispose()
        $inputStream.Dispose()
    }
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$resolvedSptRoot = [System.IO.Path]::GetFullPath($SptRoot)
$projectPath = Join-Path $repositoryRoot 'HollywoodGraphics\HollywoodGraphics.csproj'
$textureDirectory = Join-Path $repositoryRoot 'assets\bloom'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\release'
}
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

$requiredSptFile = Join-Path $resolvedSptRoot 'EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll'
if (-not (Test-Path -LiteralPath $requiredSptFile -PathType Leaf)) {
    throw "SptRoot is not an official build root with Assembly-CSharp.dll: $resolvedSptRoot"
}

$expectedTextureHashes = [ordered]@{
    'LensDust1.png' = 'BB3C364525D336439C4B6E41AA95CB37D1F93CDD99ECFBA7BD72A80512A7AFEE'
    'LensDust2.png' = '7FBEEDE90187FAC057C0978C654FA977753F45E9EAE045C9C4123AD35EF5F881'
    'LensDust3.png' = 'DD0FD89607DA872B6EB8277CC0522AFF44DA5A821E6EB45C32C27F23D5B148DA'
    'LensDust4.png' = 'D291ED122E4D30C97EA8BC5526730E19BB063808D2D631B472597466A13BAFCA'
    'LensDust4A.png' = '764E89E8B7979703015A71C1602EC6977A9728B3BF496A92CED6FC7F24396340'
    'LensDust4B.png' = '9B994BB1929E69E6D58611E39FBFFAB3AD7C2C59215A702E5EAB479F57D25B5C'
    'LensDust5.png' = '2F96B3CEE90DC7C017DF6D8A1F7DCF8DD578541A7F17421BDC01C5C152D29C58'
    'LensDust6.png' = 'E5150523AA797C0FD48039C78FEF7A8AB2EEDD0845C3A2C67393EDA6A2140C7E'
    'LensDust7.png' = 'BBFEDBBB1EE903F208D52CFF5B38DBD93E72A8EF5970904E91591BE8998011C6'
    'LensDust8.png' = '32A08DE2E20A4E523DFF27408640110C8342C0237C684DA8E9A503B5AC43CCC8'
}

$actualTextureNames = @(
    Get-ChildItem -LiteralPath $textureDirectory -File -Filter '*.png' |
        Select-Object -ExpandProperty Name |
        Sort-Object
)
$expectedTextureNames = @($expectedTextureHashes.Keys | Sort-Object)
$textureDifference = Compare-Object -ReferenceObject $expectedTextureNames -DifferenceObject $actualTextureNames
if ($null -ne $textureDifference) {
    throw "Tracked bloom texture set differs from the release manifest: $($textureDifference | Out-String)"
}

Write-Host 'CHECK: verifying the ten tracked bloom textures.'
foreach ($textureName in $expectedTextureNames) {
    $texturePath = Join-Path $textureDirectory $textureName
    $textureHash = (Get-FileHash -LiteralPath $texturePath -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($textureHash -ne $expectedTextureHashes[$textureName]) {
        throw "Texture SHA-256 mismatch for $textureName. Expected $($expectedTextureHashes[$textureName]); found $textureHash."
    }
}

Write-Host 'CHECK: building HollywoodGraphics without deploying it.'
$buildArguments = @(
    'build',
    $projectPath,
    '--configuration',
    $Configuration,
    '--nologo',
    "-p:SptRoot=$resolvedSptRoot",
    '-p:TreatWarningsAsErrors=true'
)
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$dllPath = Join-Path $repositoryRoot "HollywoodGraphics\bin\$Configuration\netstandard2.1\HollywoodGraphics.dll"
if (-not (Test-Path -LiteralPath $dllPath -PathType Leaf)) {
    throw "Build succeeded but the expected DLL was not found: $dllPath"
}

New-Item -Path $resolvedOutputDirectory -ItemType Directory -Force | Out-Null
$zipPath = Join-Path $resolvedOutputDirectory "HollywoodGraphics-$Version-SPT-$SptVersion.zip"
$checksumPath = "$zipPath.sha256"

if (Test-Path -LiteralPath $zipPath) {
    [System.IO.File]::Delete($zipPath)
}
if (Test-Path -LiteralPath $checksumPath) {
    [System.IO.File]::Delete($checksumPath)
}

$packageEntries = @(
    [pscustomobject]@{
        Source = $dllPath
        Entry = 'BepInEx/plugins/HollywoodGraphics/HollywoodGraphics.dll'
    }
)
foreach ($textureName in $expectedTextureNames) {
    $packageEntries += [pscustomobject]@{
        Source = Join-Path $textureDirectory $textureName
        Entry = "BepInEx/plugins/HollywoodGraphics/bloom/$textureName"
    }
}
$packageEntries = @($packageEntries | Sort-Object -Property Entry)

Add-Type -AssemblyName System.IO.Compression.FileSystem
Write-Host 'CHECK: creating the release archive.'
$zipStream = [System.IO.File]::Open(
    $zipPath,
    [System.IO.FileMode]::CreateNew,
    [System.IO.FileAccess]::ReadWrite,
    [System.IO.FileShare]::None
)
$archive = [System.IO.Compression.ZipArchive]::new(
    $zipStream,
    [System.IO.Compression.ZipArchiveMode]::Create,
    $false
)

try {
    foreach ($packageEntry in $packageEntries) {
        Add-DeterministicZipEntry -Archive $archive -SourcePath $packageEntry.Source -EntryName $packageEntry.Entry
    }
}
finally {
    $archive.Dispose()
    $zipStream.Dispose()
}

$readArchive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $actualEntries = @(
        $readArchive.Entries |
            Where-Object { -not [string]::IsNullOrEmpty($_.Name) } |
            ForEach-Object { $_.FullName -replace '\\', '/' } |
            Sort-Object
    )
}
finally {
    $readArchive.Dispose()
}

$expectedEntries = @($packageEntries.Entry | Sort-Object)
$entryDifference = Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $actualEntries
if ($null -ne $entryDifference) {
    throw "Release archive contents differ from the expected manifest: $($entryDifference | Out-String)"
}

$packageHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
$checksumLine = "$packageHash  $([System.IO.Path]::GetFileName($zipPath))`r`n"
[System.IO.File]::WriteAllText($checksumPath, $checksumLine, [System.Text.Encoding]::ASCII)

$packageItem = Get-Item -LiteralPath $zipPath
Write-Host "OK: $($packageItem.FullName)"
Write-Host "OK: $($packageItem.Length) bytes; SHA-256 $packageHash"

[pscustomobject]@{
    Package = $packageItem.FullName
    Bytes = $packageItem.Length
    Sha256 = $packageHash
    TextureCount = $expectedTextureNames.Count
}
