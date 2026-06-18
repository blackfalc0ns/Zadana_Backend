param(
    [string]$BaseUrl = "https://api.zadna0.com",
    [int]$WarmSamples = 12,
    [string]$Culture = "ar",
    [string]$OutputPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputPath = Join-Path $PSScriptRoot "..\artifacts\api-performance-report-$timestamp.md"
}

$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$baseUri = [Uri]($BaseUrl.TrimEnd('/'))
$httpClientHandler = [System.Net.Http.HttpClientHandler]::new()
$httpClientHandler.AutomaticDecompression = [System.Net.DecompressionMethods]::GZip -bor [System.Net.DecompressionMethods]::Deflate
$httpClient = [System.Net.Http.HttpClient]::new($httpClientHandler)
$httpClient.BaseAddress = $baseUri
$httpClient.Timeout = [TimeSpan]::FromSeconds(60)
$httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd($Culture)
$httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json")

function Invoke-TimedRequest {
    param(
        [string]$RelativeUrl
    )

    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $RelativeUrl)
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = $httpClient.SendAsync($request).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $stopwatch.Stop()

    $contentLength = 0
    if ($response.Content.Headers.ContentLength) {
        $contentLength = [int64]$response.Content.Headers.ContentLength
    } elseif (-not [string]::IsNullOrEmpty($body)) {
        $contentLength = [System.Text.Encoding]::UTF8.GetByteCount($body)
    }

    [PSCustomObject]@{
        Url = $RelativeUrl
        StatusCode = [int]$response.StatusCode
        ElapsedMs = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 2)
        Bytes = $contentLength
        Success = $response.IsSuccessStatusCode
    }
}

function Get-Stats {
    param(
        [double[]]$Values
    )

    $sorted = @($Values | Sort-Object)
    $count = $sorted.Count
    if ($count -eq 0) {
        return $null
    }

    $percentile = {
        param([double[]]$Data, [double]$Percent)
        if ($Data.Count -eq 1) {
            return $Data[0]
        }

        $rank = ($Percent / 100.0) * ($Data.Count - 1)
        $lowerIndex = [math]::Floor($rank)
        $upperIndex = [math]::Ceiling($rank)

        if ($lowerIndex -eq $upperIndex) {
            return $Data[$lowerIndex]
        }

        $weight = $rank - $lowerIndex
        return $Data[$lowerIndex] + (($Data[$upperIndex] - $Data[$lowerIndex]) * $weight)
    }

    [PSCustomObject]@{
        Count = $count
        Min = [math]::Round(($sorted | Select-Object -First 1), 2)
        Avg = [math]::Round((($sorted | Measure-Object -Average).Average), 2)
        P50 = [math]::Round((& $percentile $sorted 50), 2)
        P95 = [math]::Round((& $percentile $sorted 95), 2)
        Max = [math]::Round(($sorted | Select-Object -Last 1), 2)
    }
}

function Measure-Endpoint {
    param(
        [string]$Name,
        [string]$RelativeUrl
    )

    $cold = Invoke-TimedRequest -RelativeUrl $RelativeUrl
    $warmRuns = @()
    for ($i = 0; $i -lt $WarmSamples; $i++) {
        $warmRuns += Invoke-TimedRequest -RelativeUrl $RelativeUrl
    }

    $warmStats = Get-Stats -Values ($warmRuns | ForEach-Object { [double]$_.ElapsedMs })
    $coldVsWarmDelta = if ($warmStats.Avg -gt 0) {
        [math]::Round((($cold.ElapsedMs - $warmStats.Avg) / $cold.ElapsedMs) * 100, 2)
    } else {
        0
    }

    [PSCustomObject]@{
        Name = $Name
        Url = $RelativeUrl
        Cold = $cold
        WarmStats = $warmStats
        WarmRuns = $warmRuns
        WarmImprovementPercent = $coldVsWarmDelta
    }
}

$endpoints = @(
    @{ Name = "Health"; Url = "/health" },
    @{ Name = "Geography Regions"; Url = "/api/geography/regions" },
    @{ Name = "Home Header"; Url = "/api/home" },
    @{ Name = "Home Content"; Url = "/api/home/content" },
    @{ Name = "Home Banners"; Url = "/api/home/banners?take=6" },
    @{ Name = "Brands"; Url = "/api/brands" },
    @{ Name = "Products Search"; Url = "/api/products/search?query=s&page=1&per_page=12" }
)

$results = foreach ($endpoint in $endpoints) {
    try {
        Measure-Endpoint -Name $endpoint.Name -RelativeUrl $endpoint.Url
    }
    catch {
        [PSCustomObject]@{
            Name = $endpoint.Name
            Url = $endpoint.Url
            Error = $_.Exception.Message
        }
    }
}

$successfulResults = $results | Where-Object { $_.PSObject.Properties.Name -contains "Cold" }
$overallP95 = Get-Stats -Values ($successfulResults | ForEach-Object { [double]$_.WarmStats.P95 })
$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# API Performance Report")
$lines.Add("")
$lines.Add("- Generated at: $generatedAt")
$lines.Add("- Base URL: $($baseUri.AbsoluteUri.TrimEnd('/'))")
$lines.Add("- Accept-Language: $Culture")
$lines.Add("- Warm samples per endpoint: $WarmSamples")
$lines.Add("")
$lines.Add("## Summary")
$lines.Add("")

if ($overallP95) {
    $lines.Add("- Warm P95 across measured endpoints: $($overallP95.Avg) ms")
    $lines.Add("- Measured endpoints: $($successfulResults.Count)")
}
else {
    $lines.Add("- No successful measurements were collected.")
}

$lines.Add("")
$lines.Add("## Endpoint Results")
$lines.Add("")
$lines.Add("| Endpoint | Status | Cold ms | Warm avg ms | Warm p50 ms | Warm p95 ms | Max ms | Payload bytes | Warm gain |")
$lines.Add("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")

foreach ($result in $results) {
    if ($result.PSObject.Properties.Name -contains "Error") {
        $lines.Add("| $($result.Name) | error | - | - | - | - | - | - | $($result.Error.Replace('|', '/')) |")
        continue
    }

    $payloadBytes = ($result.WarmRuns | Measure-Object -Property Bytes -Maximum).Maximum
    $lines.Add("| $($result.Name) | $($result.Cold.StatusCode) | $($result.Cold.ElapsedMs) | $($result.WarmStats.Avg) | $($result.WarmStats.P50) | $($result.WarmStats.P95) | $($result.WarmStats.Max) | $payloadBytes | $($result.WarmImprovementPercent)% |")
}

$lines.Add("")
$lines.Add("## Notes")
$lines.Add("")
$lines.Add("- Cold time means the first request captured by this script for that endpoint.")
$lines.Add("- Warm time means repeated requests immediately after the cold request.")
$lines.Add("- Warm gain is the percentage drop from cold request time to warm average time.")
$lines.Add("- Public read endpoints are the main target because the new caching design optimizes those paths.")

[System.IO.File]::WriteAllLines($resolvedOutputPath, $lines)

Write-Host "Performance report written to: $resolvedOutputPath"
$results | ConvertTo-Json -Depth 6
