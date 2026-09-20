param([string]$BaseUrl = "http://localhost:5000")
$ErrorActionPreference = 'Stop'

# Create a request chain whose trace can be looked up without guessing which UI entry is ours.
$traceId = [Guid]::NewGuid().ToString('N')
$headers = @{ traceparent = "00-$traceId-0123456789abcdef-01" }
$movie = Invoke-RestMethod "$BaseUrl/movies" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
    title = "Observability $traceId"; description = 'Demo'; genre = 'Test'; releaseYear = 2026
} | ConvertTo-Json)
$userId = Get-Random -Minimum 100000 -Maximum 2000000000
Invoke-RestMethod "$BaseUrl/watchlist" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
    userId = $userId; movieId = $movie.id; movieTitle = $movie.title
} | ConvertTo-Json) | Out-Null
Invoke-RestMethod "$BaseUrl/reviews" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{
    userId = $userId; movieId = $movie.id; username = 'demo'; rating = 4; comment = 'Observability demo'
} | ConvertTo-Json) | Out-Null

foreach ($port in 5000..5005) {
    Invoke-RestMethod "http://localhost:$port/health" | Out-Null
}
# Give the exporter and Prometheus bounded time to receive and scrape all six services.
$expected = @('ApiGateway','AuthService','MovieService','ReviewService','WatchlistService','NotificationService')
$ready = $false
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    $query = [Uri]::EscapeDataString('sum by (service_name) (http_server_request_duration_seconds_count)')
    $metrics = Invoke-RestMethod "http://localhost:9090/api/v1/query?query=$query"
    $names = @($metrics.data.result | ForEach-Object { $_.metric.service_name })
    if (@($expected | Where-Object { $_ -notin $names }).Count -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 2
}
if (!$ready) { throw "Missing application metrics: $($expected | Where-Object { $_ -notin $names })" }

$traceReady = $false
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    try { $trace = Invoke-RestMethod "http://localhost:16686/api/traces/$traceId" } catch { $trace = $null }
    $services = @($trace.data.processes.PSObject.Properties.Value.serviceName)
    if (@('ApiGateway','MovieService','WatchlistService','ReviewService','NotificationService' |
        Where-Object { $_ -notin $services }).Count -eq 0) { $traceReady = $true; break }
    Start-Sleep -Seconds 2
}
if (!$traceReady) { throw 'The distributed trace did not contain all five participating services.' }
$producer = $trace.data.spans | Where-Object operationName -EQ 'review-created publish'
$consumer = $trace.data.spans | Where-Object operationName -EQ 'review-created process'
if (!$producer -or !$consumer -or $producer.spanID -notin $consumer.references.spanID) {
    throw 'RabbitMQ consumer span is not linked to the producer span.'
}
$notifications = Invoke-RestMethod "$BaseUrl/notifications/user/$userId"
if (!($notifications | Where-Object movieId -EQ $movie.id)) { throw 'Notification was not persisted.' }

$dashboard = Get-Content "$PSScriptRoot/../docker/grafana/dashboards/moviereviewhub.json" -Raw | ConvertFrom-Json
foreach ($panel in $dashboard.panels) {
    $query = [Uri]::EscapeDataString($panel.targets[0].expr)
    # rate() needs at least two scrapes, including on a fresh CI stack.
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $result = Invoke-RestMethod "http://localhost:9090/api/v1/query?query=$query"
        if ($result.status -eq 'success' -and $result.data.result.Count -gt 0) { break }
        Start-Sleep -Seconds 2
    }
    if ($result.status -ne 'success' -or $result.data.result.Count -eq 0) { throw "Dashboard query failed: $($panel.title)" }
}
Invoke-RestMethod 'http://localhost:3000/api/health' | Out-Null
Write-Output "PASS: metrics for all six services, all dashboard queries, and asynchronous notification."
Write-Output "PASS: gateway, reactive HTTP and RabbitMQ spans share trace $traceId"
Write-Output "View trace: http://localhost:16686/trace/$traceId"
