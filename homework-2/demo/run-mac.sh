#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Intelligent Customer Support System - Web API"
echo
echo "This script starts the .NET Web API from the demo folder."
echo
echo "Prerequisites:"
echo "  - .NET 10 SDK installed"
echo
echo "The API will be available at:"
echo "  - HTTP:  http://localhost:5077"
echo "  - HTTPS: https://localhost:7076"
echo
echo "Useful endpoints:"
echo "  - Health check:         http://localhost:5077/health"
echo "  - Scalar API reference: http://localhost:5077/scalar/v1"
echo "  - OpenAPI document:     http://localhost:5077/openapi/v1.json"
echo
echo "Starting API..."
echo "Press Ctrl+C to stop the server."
echo

dotnet run --project "$SCRIPT_DIR/../src/API/API.csproj"
