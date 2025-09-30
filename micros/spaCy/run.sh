#!/bin/bash

# spaCy NLP Service Setup and Run Script

echo "Setting up spaCy NLP Service..."

# Check if Python is installed
if ! command -v python3 &> /dev/null; then
    echo "Python 3 is required but not installed."
    exit 1
fi

# Create virtual environment if it doesn't exist
if [ ! -d "venv" ]; then
    echo "Creating virtual environment..."
    python3 -m venv venv
fi

# Activate virtual environment
echo "Activating virtual environment..."
source venv/bin/activate

# Install dependencies
echo "Installing dependencies..."
pip install --upgrade pip
pip install -r requirements.txt

# Download spaCy language model
echo "Downloading spaCy language model..."
python -m spacy download en_core_web_md

# Run the service
echo "Starting spaCy NLP Service..."
python app.py