# PowerShell script to test spaCy integration by uploading a file
$boundary = [System.Guid]::NewGuid().ToString()
$filePath = "test-pdf-text.txt" 
$uri = "http://localhost:5082/api/ingest"

# Read the file content
$fileContent = [System.IO.File]::ReadAllBytes($filePath)
$fileName = [System.IO.Path]::GetFileName($filePath)

# Create multipart form data
$LF = "`r`n"
$bodyLines = @(
    "--$boundary",
    "Content-Disposition: form-data; name=`"file`"; filename=`"$fileName`"",
    "Content-Type: text/plain",
    "",
    [System.Text.Encoding]::UTF8.GetString($fileContent),
    "--$boundary--"
) -join $LF

# Send the request
try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -Body $bodyLines -ContentType "multipart/form-data; boundary=$boundary"
    Write-Host "SUCCESS! Upload response:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response body: $responseBody" -ForegroundColor Yellow
    }
}