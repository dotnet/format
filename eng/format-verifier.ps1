[CmdletBinding(PositionalBinding = $false)]
Param(
    [string]$logPath,
    [string]$testPath
)

$repos = @(
    "https://github.com/dotnet/format",
    "https://github.com/dotnet/roslyn"
    "https://github.com/dotnet/cli",
    "https://github.com/dotnet/project-system",
    "https://github.com/Microsoft/msbuild",
    "https://github.com/dotnet/corefx",
    #"https://github.com/aspnet/AspNetCore",
    "https://github.com/aspnet/Blazor",
    "https://github.com/aspnet/EntityFrameworkCore",
    "https://github.com/aspnet/EntityFramework6"
)

$cloneLog = Join-Path $logPath "clone.log"
$restoreLog = Join-Path $logPath "restore.log"
$formatLog = Join-Path $logPath "format.log"

If (!(Test-Path $testPath)) {
    New-Item -ItemType Directory -Force -Path $testPath
}

If (!(Test-Path $logPath)) {
    New-Item -ItemType Directory -Force -Path $logPath
}

foreach ($repo in $repos) {
    try {
        $repoName = $repo.Substring(19)
        $folderName = $repoName.Split("/")[1]

        $repoPath = Join-Path $testPath $folderName

        Write-Output "$(Get-Date) - Cloning $repoName..."
        git.exe clone --depth 1 $repo $repoPath *> $cloneLog

        Write-Output "$(Get-Date) - Finding solutions..."
        $solutions = Get-ChildItem -Path $repoPath -Filter *.sln -Recurse -Depth 2 | Select-Object -ExpandProperty FullName

        foreach ($solution in $solutions) {
            Write-Output "$(Get-Date) - Restoring $solution..."
            dotnet.exe restore $solution *> $restoreLog

            Write-Output "$(Get-Date) - Formatting $solution..."
            dotnet.exe run -p .\src\dotnet-format.csproj -c Release -- -w $solution -v d --dry-run *> $formatLog
            
            if ($LastExitCode -ne 0) { 
                Write-Output "$(Get-Date) - Formatting failed with error code $LastExitCode. Check the format.log for details."
                exit -1
            }
            
            $output = Get-Content -Path $formatLog | Out-String
            if (($output -notmatch "(?m)Formatted \d+ of (\d+) files") -or ($Matches[1] -eq "0")) {
                Write-Output "$(Get-Date) - No files found for project. Check the format.log for details."
                exit -1
            }

            Write-Output "$(Get-Date) - Formatted $solution."
        }
    }
    catch {
        exit -1
    }
    finally {
        Remove-Item $repoPath -Force -Recurse
        Write-Output "$(Get-Date) - Deleted $repoName."

        Write-Output ""       
    }
}

Remove-Item *.log