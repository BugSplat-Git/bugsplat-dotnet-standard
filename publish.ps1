function WriteLine($value) {
    Write-Output "`n$value"
}

WriteLine("Running dotnet build...")

$buildOutput = [string](dotnet.exe build .\BugSplatDotNetStandard\BugSplatDotNetStandard.csproj --no-incremental -c Release)

WriteLine("$buildOutput")

$buildSuccess = $buildOutput -match "Build succeeded"

if (-not $buildSuccess) {
    throw 'Build failed'
}

$buildOutput -match "Successfully created package '(.*)'"
$nupkgPath = $Matches[1];

WriteLine("Signing $nupkgPath...")

# Requires folder containing nuget.exe be added to PATH
# https://www.nuget.org/downloads
$signOutput = [string](nuget.exe sign $nupkgPath -CertificatePath .\BugSplat.pfx -Timestamper http://timestamp.comodoca.com/)

WriteLine("$signOutput")

$signSuccess = $signOutput.Contains("Package(s) signed successfully.")

if (-not $signSuccess) {
    throw 'Code signing failed'
}

WriteLine("Publishing $nupkgPath...")

$publishOutput = [string](dotnet nuget push $nupkgPath --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json)

WriteLine("$publishOutput")

$publishSuccess = $publishOutput.Contains("Your package was pushed.");

if (!not $publishSuccess) {
    throw 'Publish failed'
}

WriteLine("Great success!")