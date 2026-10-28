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
os=$(uname -s)
arch=$(uname -m)
case "$os/$arch" in
  Linux/x86_64) platform=linux-x64 ;;
  Linux/aarch64|Linux/arm64) platform=linux-arm64 ;;
  Darwin/x86_64) platform=darwin-x64 ;;
  Darwin/arm64|Darwin/aarch64) platform=darwin-arm64 ;;
  *)
    printf 'Unsupported Playwright platform: %s/%s. Supported: Linux or Darwin on x86_64 or arm64/aarch64.\n' "$os" "$arch" >&2
    exit 1
    ;;
esac

driver=tests/HackathonStarter.E2E/bin/Debug/net10.0/.playwright
node="$driver/node/$platform/node"
cli="$driver/package/cli.js"
if [[ ! -f "$node" || ! -x "$node" || ! -f "$cli" ]]; then
  printf 'Playwright driver missing or not executable for %s: %s; CLI: %s. Run dotnet build -warnaserror -v q and retry.\n' "$platform" "$node" "$cli" >&2
  exit 1
fi
"$node" "$cli" install chromium

dotnet tool list -g | grep -q csharp-ls || dotnet tool install -g csharp-ls || echo "csharp-ls not installed (optional)"

name=$(git config user.name || true); email=$(git config user.email || true)
if [[ -z "$name" || -z "$email" ]]; then
  echo "Set your own identity: git config user.name \"Your Name\"; git config user.email you@example.com"
else
  echo "git identity: $name <$email>"
fi
echo "Done. Next: dotnet test tests/HackathonStarter.Tests && dotnet test tests/HackathonStarter.E2E"
