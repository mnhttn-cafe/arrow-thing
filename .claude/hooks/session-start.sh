#!/bin/bash
set -euo pipefail

# Only run in remote (cloud) environments.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
    exit 0
fi

# Run asynchronously — installs happen in background while session starts.
echo '{"async": true, "asyncTimeout": 300000}'

# ── .NET SDK (for CSharpier formatter) ───────────────────────────────
if ! command -v dotnet &>/dev/null; then
    echo "Installing .NET SDK 10.0..."
    apt-get update -qq
    apt-get install -y -qq dotnet-sdk-10.0 2>&1 | tail -1
fi

# ── Dotnet local tools (CSharpier) ───────────────────────────────────
cd "$CLAUDE_PROJECT_DIR"
dotnet tool restore 2>&1 | tail -1

# ── Git hooks ────────────────────────────────────────────────────────
git config core.hooksPath .githooks

echo "Session setup complete: dotnet $(dotnet --version), csharpier ready, hooks active."
