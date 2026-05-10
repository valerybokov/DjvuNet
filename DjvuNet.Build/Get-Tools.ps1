#
# Get-Tools.ps1
#

param(
    [parameter(Mandatory=$true)]$ToolsRemotePath,
    [parameter(Mandatory=$true)]$ToolsLocalPath,
    [parameter(Mandatory=$true)]$ToolsPath,
    [parameter(Mandatory=$true)]$ToolsName,
    [parameter(Mandatory=$true)]$ToolsSemaphoreFileName,
    [parameter(Mandatory=$true)]$MessagePrefix
)

$ErrorActionPreference = "Stop"
$MessagePrefix = $MessagePrefix.Trim()
$retryCount = 0
$success = $false
$semaphoreFile = [System.IO.Path]::Combine($ToolsPath, $ToolsSemaphoreFileName)
$semaphoreFile = [System.IO.Path]::GetFullPath($semaphoreFile)

if ([System.IO.Directory]::Exists($ToolsPath)) {
    if ([System.IO.File]::Exists($semaphoreFile)) {
        Write-Output "$MessagePrefix $ToolsName already restored"
        exit
    }
    else {
        Write-Output "$MessagePrefix Not found sempahore file: $semaphoreFile"
        [System.IO.Directory]::Delete($ToolsPath, $true)
        Write-Output "$MessagePrefix Deleted $ToolsPath directory"
    }
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$baseTimeout = 120
$maxAttempts = 5

do {
    try {
        $currentTimeout = $baseTimeout * ($retryCount + 1)
        Write-Output "$MessagePrefix Downloading $ToolsName from $ToolsRemotePath (Timeout: ${currentTimeout}s)"
        
        $request = [System.Net.WebRequest]::Create($ToolsRemotePath)
        $request.Timeout = $currentTimeout * 1000
        
        $response = $null
        $stream = $null
        $fileStream = $null
        try {
            try {
                $response = $request.GetResponse()
            } catch [System.Net.WebException] {
                if ($null -ne $_.Exception.Response) {
                    try { $_.Exception.Response.Close() } catch {}
                }
                throw
            }
            $stream = $response.GetResponseStream()
            $fileStream = [System.IO.File]::Create($ToolsLocalPath)
            $stream.CopyTo($fileStream)
        } finally {
            if ($null -ne $fileStream) { try { $fileStream.Dispose() } catch {} }
            if ($null -ne $stream) { try { $stream.Dispose() } catch {} }
            if ($null -ne $response) { try { $response.Close() } catch {} }
        }
        
        $success = $true
    } catch {
        if ($retryCount -ge ($maxAttempts - 1)) {
            Write-Output "$MessagePrefix Maximum of $maxAttempts retries exceeded. Aborting"
            throw
        }
        else {
            $retryTime = 10 * [Math]::Pow(2, $retryCount)
            $retryCount++
            Write-Output "$MessagePrefix Download failed. Retrying in $retryTime seconds (Attempt $retryCount of $maxAttempts)..."
            Start-Sleep -Seconds $retryTime
        }
    }
} while ($success -eq $false)

Write-Output "$MessagePrefix Download of $ToolsName finished"


Add-Type -Assembly 'System.IO.Compression.FileSystem' -ErrorVariable AddTypeErrors
if ($AddTypeErrors.Count -eq 0) {
    [System.IO.Compression.ZipFile]::ExtractToDirectory($ToolsLocalPath, $ToolsPath)
    Write-Output "$MessagePrefix Extracted $ToolsName into $ToolsPath"
}
else {
    (New-Object -com shell.application).namespace($DotnetPath).CopyHere((new-object -com shell.application).namespace($DotnetLocalPath).Items(), 16)
    Write-Output "$MessagePrefix Extracted $ToolsName into $ToolsPath with explorer"
}

[System.IO.File]::WriteAllText($semaphoreFile, "")
Write-Host "$MessagePrefix Created $ToolsName semaphore $semaphoreFile"
