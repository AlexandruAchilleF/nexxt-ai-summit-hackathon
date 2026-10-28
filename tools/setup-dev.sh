#!/usr/bin/env bash
# One-time developer setup: .NET 10, build, Playwright Chromium, optional C# language server, git identity check.
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  echo "Installing .NET 10 SDK into ~/.dotnet"
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir "$HOME/.dotnet"
fi
dotnet --version

dotnet build -warnaserror -v q

# Playwright's .NET package ships its own node driver, so no pwsh or global node is needed.
driver=tests/HackathonStarter.E2E/bin/Debug/net10.0/.playwright
"$driver/node/linux-x64/node" "$driver/package/cli.js" install chromium

dotnet tool list -g | grep -q csharp-ls || dotnet tool install -g csharp-ls || echo "csharp-ls not installed (optional)"

name=$(git config user.name || true); email=$(git config user.email || true)
if [[ -z "$name" || -z "$email" ]]; then
  echo "Set your own identity: git config user.name \"Your Name\"; git config user.email you@example.com"
else
  echo "git identity: $name <$email>"
fi
echo "Done. Next: dotnet test tests/HackathonStarter.Tests && dotnet test tests/HackathonStarter.E2E"
