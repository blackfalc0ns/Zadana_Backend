$ApiKey = "re_br32Lwek_NG8RPNtkH4z8kYxFNoFKNXkQ"
$Uri = "https://api.resend.com/emails"
$Headers = @{
    "Authorization" = "Bearer $ApiKey"
    "Content-Type"  = "application/json"
}
$Body = @{
    from    = "Zadana <no-reply@zadna.blackfalcons.net>"
    to      = "e9e4f12f1a@emailax.pro"
    subject = "Test from Zadana"
    html    = "<strong>It works!</strong>"
} | ConvertTo-Json

try {
    $Response = Invoke-WebRequest -Method Post -Uri $Uri -Headers $Headers -Body $Body -UseBasicParsing
    Write-Host "Success!"
    Write-Host $Response.Content
} catch {
    Write-Host "Failed!"
    Write-Host $_.Exception.Message
    if ($_.ErrorDetails.Message) {
        Write-Host "Details: $($_.ErrorDetails.Message)"
    }
}
