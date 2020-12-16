[CmdletBinding(PositionalBinding = $false)]
Param(
    [string]$repo,
    [string]$sha,
    [string]$branchName,
    [string]$targetSolution,
    [string]$testPath,
    [string]$stage  # Valid values are "prepare", "format-workspace", "format-folder"
)

if ($stage -eq "prepare") {
    Write-Output "$(Get-Date) - Building dotnet-format."
    dotnet.exe build ./src -c Release
}

$currentLocation = Get-Location

if (!(Test-Path $testPath)) {
    New-Item -ItemType Directory -Force -Path $testPath | Out-Null
}

try {
    $repoName = $repo.Substring(19)
    $folderName = $repoName.Split("/")[1]
    $repoPath = Join-Path $testPath $folderName

    if (!(Test-Path $repoPath)) {
        New-Item -ItemType Directory -Force -Path $repoPath | Out-Null
    }

    Set-Location $repoPath

    if ($stage -eq "prepare") {
        Write-Output "$(Get-Date) - Cloning $repoName."
        git.exe init
        git.exe remote add origin $repo
        git.exe fetch --progress --no-tags --depth=1 origin $sha
        git.exe checkout $sha
    }

    # We invoke build.ps1 ourselves because running `restore.cmd` invokes the build.ps1
    # in a child process which means added .NET Core SDKs aren't visible to this process.
    if (Test-Path '.\eng\Build.ps1') {
        Write-Output "$(Get-Date) - Running Build.ps1 -restore"
        .\eng\Build.ps1 -restore
    }
    elseif (Test-Path '.\eng\common\Build.ps1') {
        Write-Output "$(Get-Date) - Running Build.ps1 -restore"
        .\eng\common\Build.ps1 -restore
    }

    if ($stage -eq "prepare" -or $stage -eq "format-workspace") {
        Write-Output "$(Get-Date) - Finding solutions."
        $solutions = Get-ChildItem -Filter *.sln -Recurse -Depth 2 | Select-Object -ExpandProperty FullName | Where-Object { $_ -match '.sln$' }

        foreach ($solution in $solutions) {
            $solutionPath = Split-Path $solution
            $solutionFile = Split-Path $solution -leaf

            if ($solutionFile -ne $targetSolution) {
                continue
            }

            Set-Location $solutionPath

            if ($stage -eq "prepare") {
                Write-Output "$(Get-Date) - $solutionFile - Restoring"
                dotnet.exe restore $solution
            }

            if ($stage -eq "format-workspace") {
                Write-Output "$(Get-Date) - $solutionFile - Formatting Workspace"
                $output = dotnet.exe "$currentLocation/artifacts/bin/dotnet-format/Release/netcoreapp2.1/dotnet-format.dll" $solution -wsa -v diag --check | Out-String
                Write-Output $output.TrimEnd()

                # Ignore CheckFailedExitCode since we don't expect these repos to be properly formatted.
                if ($LastExitCode -ne 0 -and $LastExitCode -ne 2) {
                    Write-Output "$(Get-Date) - Formatting failed with error code $LastExitCode."
                    exit -1
                }

                if (($output -notmatch "(?m)Formatted \d+ of (\d+) files") -or ($Matches[1] -eq "0")) {
                    Write-Output "$(Get-Date) - No files found for solution."
                    # The dotnet/sdk has a toolset solution with no files.
                    # exit -1
                }
            }

            Write-Output "$(Get-Date) - $solutionFile - Complete"
        }
    }

    if ($stage -eq "format-folder") {
        Write-Output "$(Get-Date) - $folderName - Formatting Folder"
        $output = dotnet.exe "$currentLocation/artifacts/bin/dotnet-format/Release/netcoreapp2.1/dotnet-format.dll" -f $repoPath -v diag --check | Out-String
        Write-Output $output.TrimEnd()

        # Ignore CheckFailedExitCode since we don't expect these repos to be properly formatted.
        if ($LastExitCode -ne 0 -and $LastExitCode -ne 2) {
            Write-Output "$(Get-Date) - Formatting failed with error code $LastExitCode."
            exit -1
        }

        if (($output -notmatch "(?m)Formatted \d+ of (\d+) files") -or ($Matches[1] -eq "0")) {
            Write-Output "$(Get-Date) - No files found for solution."
            exit -1
        }

        Write-Output "$(Get-Date) - $folderName - Complete"
    }

    exit 0
}
catch {
    exit -1
}
finally {
    Set-Location $currentLocation
}
