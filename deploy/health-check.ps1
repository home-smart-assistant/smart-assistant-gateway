$urls = @(
    "http://localhost:8080/health",
    "http://localhost:8091/health",
    "http://localhost:8092/health"
)

foreach ($url in $urls) {
    try {
        $resp = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 4
        Write-Host "[OK] $url"
        $resp | ConvertTo-Json -Depth 5
    }
    catch {
        Write-Host "[FAIL] $url - $($_.Exception.Message)" -ForegroundColor Red
    }
}
