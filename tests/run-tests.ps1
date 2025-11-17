# MCP Server Test Runner

Write-Host "MCP Server Tests" -ForegroundColor Cyan
Write-Host "================" -ForegroundColor Cyan
Write-Host ""

$projectPath = "src/PipeDreamMcp/PipeDreamMcp.csproj"
$passCount = 0
$failCount = 0

# Test 1: Initialize
Write-Host "Test: Initialize handshake" -ForegroundColor Yellow
$result1 = Get-Content "tests/test-init.json" | dotnet run --project $projectPath 2>$null
$json1 = $result1 | ConvertFrom-Json
if ($json1.result.protocolVersion -eq "2024-11-05") {
    Write-Host "   PASS" -ForegroundColor Green
    $passCount++
} else {
    Write-Host "   FAIL" -ForegroundColor Red
    $failCount++
}
Write-Host ""

# Test 2: Tools/List
Write-Host "Test: Tools list (empty)" -ForegroundColor Yellow
$result2 = Get-Content "tests/test-tools-list.json" | dotnet run --project $projectPath 2>$null
$json2 = $result2 | ConvertFrom-Json
if ($json2.result.tools -is [array] -and $json2.result.tools.Count -eq 0) {
    Write-Host "   PASS" -ForegroundColor Green
    $passCount++
} else {
    Write-Host "   FAIL" -ForegroundColor Red
    $failCount++
}
Write-Host ""

# Test 3: Invalid Method
Write-Host "Test: Invalid method error handling" -ForegroundColor Yellow
$result3 = Write-Output '{"jsonrpc":"2.0","id":3,"method":"invalid/method"}' | dotnet run --project $projectPath 2>$null
$json3 = $result3 | ConvertFrom-Json
if ($json3.error.code -eq -32601) {
    Write-Host "   PASS" -ForegroundColor Green
    $passCount++
} else {
    Write-Host "   FAIL" -ForegroundColor Red
    $failCount++
}
Write-Host ""

# Summary
Write-Host "================" -ForegroundColor Cyan
Write-Host "Results: $passCount passed, $failCount failed" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Yellow" })
if ($failCount -eq 0) {
    Write-Host "All tests passed!" -ForegroundColor Green
}
