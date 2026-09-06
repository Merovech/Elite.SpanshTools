#!/usr/bin/env bash
set -euo pipefail

dotnet build -c Release
./utilities/Elite.SpanshTools.Utilities/bin/Release/net8.0/ModelVerifier.exe --data-location /d/data --data-file https://downloads.spansh.co.uk/galaxy.json.gz