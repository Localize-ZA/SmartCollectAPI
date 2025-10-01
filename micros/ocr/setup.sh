#!/bin/bash

# Setup script for EasyOCR microservice

echo "Setting up EasyOCR microservice..."

# Create virtual environment
python -m venv venv

# Activate virtual environment
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" ]]; then
    source venv/Scripts/activate
else
    source venv/bin/activate
fi

# Upgrade pip
pip install --upgrade pip

# Install requirements
pip install -r requirements.txt

echo ""
echo "✓ EasyOCR microservice setup complete!"
echo ""
echo "To run the service:"
echo "  1. Activate virtual environment:"
echo "     Windows: .\\venv\\Scripts\\Activate.ps1"
echo "     Linux/Mac: source venv/bin/activate"
echo "  2. Start the service:"
echo "     uvicorn app:app --reload --port 5085"
echo ""
