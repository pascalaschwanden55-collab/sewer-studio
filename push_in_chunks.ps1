$ErrorActionPreference = 'Continue'
$Branch = 'feature/pdf-import-beobachtungen'
$RemoteRef = "refs/heads/$Branch"
$BatchSize = 5

$commits = @(git log --reverse --format="%H" "origin/$Branch..HEAD")
$total = $commits.Count
Write-Host "Pending commits: $total"
Write-Host "Batch size: $BatchSize commits per push"
Write-Host "Estimated batches: $([Math]::Ceiling($total / $BatchSize))"
Write-Host ""

$batchNum = 0
for ($i = 0; $i -lt $total; $i += $BatchSize) {
    $end = [Math]::Min($i + $BatchSize - 1, $total - 1)
    $target = $commits[$end]
    $batchNum++
    $batchCount = $end - $i + 1

    Write-Host "[$batchNum] Pushing $batchCount commits ($($i+1)-$($end+1) of $total) up to $($target.Substring(0,8))..."
    git push origin "${target}:$RemoteRef"
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "BATCH FAILED - trying single commits one by one..."
        for ($j = $i; $j -le $end; $j++) {
            $single = $commits[$j]
            Write-Host "  Single push: $($single.Substring(0,8))"
            git push origin "${single}:$RemoteRef"
            if ($LASTEXITCODE -ne 0) {
                Write-Host ""
                Write-Host "STUCK at commit $single - manual intervention needed."
                Write-Host "Pushed so far: $j of $total"
                exit 1
            }
        }
    }
}

Write-Host ""
Write-Host "=== DONE - all $total commits pushed ==="
