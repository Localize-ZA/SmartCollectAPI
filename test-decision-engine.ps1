# Test Decision Engine
$baseUrl = "http://localhost:5082"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  Decision Engine Test Suite" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Test 1: Get test cases
Write-Host "`n1. Getting Test Cases..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/run-tests" -Method Get
    
    Write-Host "`n✓ Results:" -ForegroundColor Green
    foreach ($result in $response) {
        Write-Host "`n  File: $($result.fileName)" -ForegroundColor White
        if ($result.success) {
            Write-Host "    Status: ✓ Success" -ForegroundColor Green
            Write-Host "    Document Type: $($result.plan.documentType)" -ForegroundColor Cyan
            Write-Host "    Chunking: $($result.plan.chunkingStrategy) (size: $($result.plan.chunkSize), overlap: $($result.plan.chunkOverlap))" -ForegroundColor Cyan
            Write-Host "    Embedding: $($result.plan.embeddingProvider)" -ForegroundColor Cyan
            Write-Host "    Language: $($result.plan.language)" -ForegroundColor Cyan
            Write-Host "    Priority: $($result.plan.priority)" -ForegroundColor Cyan
            Write-Host "    OCR Required: $($result.plan.requiresOCR)" -ForegroundColor Cyan
            Write-Host "    NER Required: $($result.plan.requiresNER)" -ForegroundColor Cyan
            Write-Host "    Use Reranking: $($result.plan.useReranking)" -ForegroundColor Cyan
            Write-Host "    Estimated Cost: $($result.plan.estimatedCost) units" -ForegroundColor Cyan
            if ($result.plan.decisionReasons) {
                Write-Host "    Reasoning:" -ForegroundColor Yellow
                foreach ($reason in $result.plan.decisionReasons) {
                    Write-Host "      - $reason" -ForegroundColor Gray
                }
            }
        } else {
            Write-Host "    Status: ✗ Failed" -ForegroundColor Red
            Write-Host "    Error: $($result.error)" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "✗ Test failed" -ForegroundColor Red
    Write-Host $_.Exception.Message
}

# Test 2: Analyze a custom document
Write-Host "`n`n2. Testing Custom Document Analysis..." -ForegroundColor Yellow
$customDoc = @{
    fileName = "research_paper.pdf"
    fileSize = 500000
    mimeType = "application/pdf"
    contentPreview = "Abstract: This paper presents a novel approach to machine learning using transformers. We introduce a new architecture that improves upon previous state-of-the-art results..."
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/analyze" -Method Post -Body $customDoc -ContentType "application/json"
    
    Write-Host "`n✓ Analysis Complete:" -ForegroundColor Green
    Write-Host "  Document Type: $($response.documentType)" -ForegroundColor Cyan
    Write-Host "  Chunking Strategy: $($response.chunkingStrategy)" -ForegroundColor Cyan
    Write-Host "  Chunk Size: $($response.chunkSize)" -ForegroundColor Cyan
    Write-Host "  Embedding Provider: $($response.embeddingProvider)" -ForegroundColor Cyan
    Write-Host "  Priority: $($response.priority)" -ForegroundColor Cyan
    Write-Host "  Estimated Cost: $($response.estimatedCost) units" -ForegroundColor Cyan
    
    Write-Host "`n  Decision Reasoning:" -ForegroundColor Yellow
    foreach ($reason in $response.decisionReasons) {
        Write-Host "    - $reason" -ForegroundColor Gray
    }
} catch {
    Write-Host "✗ Analysis failed" -ForegroundColor Red
    Write-Host $_.Exception.Message
}

# Test 3: Test different document types
Write-Host "`n`n3. Testing Document Type Detection..." -ForegroundColor Yellow

$testDocuments = @(
    @{
        name = "Legal Contract"
        fileName = "nda.pdf"
        fileSize = 100000
        mimeType = "application/pdf"
        contentPreview = "NON-DISCLOSURE AGREEMENT. WHEREAS the parties hereinafter referred to as Discloser and Recipient agree to the following terms and conditions..."
    },
    @{
        name = "Medical Record"
        fileName = "patient_chart.txt"
        fileSize = 50000
        mimeType = "text/plain"
        contentPreview = "Patient: Jane Smith, DOB: 1985-03-15. Chief Complaint: Chest pain. Diagnosis: Acute coronary syndrome. Treatment plan: Aspirin 325mg, Nitroglycerin..."
    },
    @{
        name = "Python Code"
        fileName = "model.py"
        fileSize = 25000
        mimeType = "text/x-python"
        contentPreview = "import torch\nfrom transformers import AutoModel\n\nclass CustomModel(torch.nn.Module):\n    def __init__(self):\n        super().__init__()"
    },
    @{
        name = "Chinese Document"
        fileName = "chinese_doc.txt"
        fileSize = 30000
        mimeType = "text/plain"
        contentPreview = "这是一份中文文档。机器学习是人工智能的一个分支。深度学习使用神经网络来处理数据。"
    }
)

foreach ($doc in $testDocuments) {
    Write-Host "`n  Testing: $($doc.name)" -ForegroundColor White
    $body = @{
        fileName = $doc.fileName
        fileSize = $doc.fileSize
        mimeType = $doc.mimeType
        contentPreview = $doc.contentPreview
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/DecisionEngine/analyze" -Method Post -Body $body -ContentType "application/json"
        Write-Host "    ✓ Type: $($response.documentType), Language: $($response.language), Strategy: $($response.chunkingStrategy)" -ForegroundColor Green
    } catch {
        Write-Host "    ✗ Failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "  Test Suite Complete!" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
