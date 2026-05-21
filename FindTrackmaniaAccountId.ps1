$ErrorActionPreference = "SilentlyContinue"

$accountId = "08242041-438c-4d60-bd98-230335bd678b"
$accountIdNoDashes = $accountId.Replace("-", "")
$maxFileSizeBytes = 200MB

function Convert-HexToBytes([string]$hex)
{
    $bytes = New-Object byte[] ($hex.Length / 2)

    for ($i = 0; $i -lt $bytes.Length; $i++)
    {
        $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16)
    }

    return $bytes
}

function Find-BytePattern
{
    param(
        [byte[]]$Data,
        [byte[]]$Pattern
    )

    if ($Pattern.Length -eq 0 -or $Data.Length -lt $Pattern.Length)
    {
        return $false
    }

    for ($i = 0; $i -le ($Data.Length - $Pattern.Length); $i++)
    {
        $found = $true

        for ($j = 0; $j -lt $Pattern.Length; $j++)
        {
            if ($Data[$i + $j] -ne $Pattern[$j])
            {
                $found = $false
                break
            }
        }

        if ($found)
        {
            return $true
        }
    }

    return $false
}

$utf8 = [System.Text.Encoding]::UTF8
$utf16 = [System.Text.Encoding]::Unicode

$patterns = @(
    [pscustomobject]@{
        Name = "UTF-8 account id"
        Bytes = $utf8.GetBytes($accountId)
    },
    [pscustomobject]@{
        Name = "UTF-8 account id without dashes"
        Bytes = $utf8.GetBytes($accountIdNoDashes)
    },
    [pscustomobject]@{
        Name = "UTF-16 account id"
        Bytes = $utf16.GetBytes($accountId)
    },
    [pscustomobject]@{
        Name = "UTF-16 account id without dashes"
        Bytes = $utf16.GetBytes($accountIdNoDashes)
    },
    [pscustomobject]@{
        Name = "GUID binary little-endian"
        Bytes = ([Guid]$accountId).ToByteArray()
    },
    [pscustomobject]@{
        Name = "GUID binary big-endian"
        Bytes = [byte[]](Convert-HexToBytes $accountIdNoDashes)
    }
)

$roots = @(
    "$env:USERPROFILE\Documents\Trackmania",
    "$env:LOCALAPPDATA\Trackmania",
    "$env:APPDATA\Trackmania",
    "$env:LOCALAPPDATA\Ubisoft Game Launcher",
    "$env:APPDATA\Ubisoft",
    "$env:PROGRAMDATA\Ubisoft"
) | Where-Object { Test-Path $_ }

Write-Host ""
Write-Host "Searching these folders:"
$roots | ForEach-Object { Write-Host " - $_" }
Write-Host ""

$files = foreach ($root in $roots)
{
    Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Length -le $maxFileSizeBytes }
}

$files = @($files)

Write-Host "Files to scan: $($files.Count)"
Write-Host ""

$matches = New-Object System.Collections.Generic.List[object]

for ($index = 0; $index -lt $files.Count; $index++)
{
    $file = $files[$index]

    Write-Progress `
        -Activity "Searching Trackmania files for account ID" `
        -Status $file.FullName `
        -PercentComplete ((($index + 1) / [Math]::Max($files.Count, 1)) * 100)

    try
    {
        [byte[]]$bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    }
    catch
    {
        continue
    }

    foreach ($pattern in $patterns)
    {
        if (Find-BytePattern -Data $bytes -Pattern $pattern.Bytes)
        {
            $matches.Add([pscustomobject]@{
                Pattern = $pattern.Name
                File = $file.FullName
                SizeKB = [Math]::Round($file.Length / 1KB, 2)
                LastWriteTime = $file.LastWriteTime
            }) | Out-Null
        }
    }
}

Write-Progress -Activity "Searching Trackmania files for account ID" -Completed

Write-Host ""
Write-Host "Search complete."
Write-Host ""

if ($matches.Count -eq 0)
{
    Write-Host "NO MATCHES FOUND."
    Write-Host ""
    Write-Host "That means the account ID is probably not stored as plain text or simple GUID bytes in local Trackmania files."
}
else
{
    Write-Host "MATCHES FOUND:"
    Write-Host ""

    $matches |
        Sort-Object File, Pattern |
        Format-Table -AutoSize

    $outputPath = Join-Path (Get-Location) "account-id-search-results.csv"

    $matches |
        Sort-Object File, Pattern |
        Export-Csv -LiteralPath $outputPath -NoTypeInformation -Encoding UTF8

    Write-Host ""
    Write-Host "Saved results to:"
    Write-Host $outputPath
}
