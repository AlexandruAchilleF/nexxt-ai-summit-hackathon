#!/usr/bin/env bash
# Offline whole-script tests. Darwin cases simulate uname; they do not validate a macOS runtime.
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
setup_script="$root/tools/setup-dev.sh"
bash_binary=$BASH
temp_base=${TMPDIR:-/tmp}
if [[ -d /tmp/opencode ]]; then temp_base=/tmp/opencode; fi
sandbox=$(mktemp -d "$temp_base/setup-dev-tests.XXXXXX")
trap 'rm -rf "$sandbox"' EXIT

total=0
passed=0
failed=0

die() { printf 'Fixture error: %s\n' "$*" >&2; exit 1; }

new_case() {
  fake_os=$1
  fake_arch=$2
  case_dir="$sandbox/case-$total"
  fake_home="$case_dir/synthetic home"
  fake_bin="$case_dir/bin"
  fake_repo="$case_dir/repo"
  driver="$fake_repo/tests/HackathonStarter.E2E/bin/Debug/net10.0/.playwright"
  trace="$case_dir/calls.log"
  output="$case_dir/output.log"
  status=0
  fake_sdks='10.0.100 [synthetic-sdk]'
  fake_tool_present=1
  fake_tool_install_exit=0
  fake_build_exit=0
  fake_curl_exit=0
  fake_name='Synthetic Developer'
  fake_email='developer@example.invalid'
  mkdir -p "$fake_home/.dotnet/tools" "$fake_bin" "$fake_repo/tools" "$driver/package" "$case_dir/tmp" || die mkdir
  cp "$setup_script" "$fake_repo/tools/setup-dev.sh" || die 'copy setup script'
  : > "$trace"
  printf '// Synthetic CLI; never executed by a real node.\n' > "$driver/package/cli.js"

  # Deliberately no host PATH fallback: only harmless helpers and the fakes below are executable.
  ln -s "$bash_binary" "$fake_bin/bash" || die 'link bash'
  for helper in dirname grep cat; do
    helper_path=$(command -v "$helper") || die "find $helper"
    ln -s "$helper_path" "$fake_bin/$helper" || die "link $helper"
  done

  cat > "$fake_bin/uname" <<'MOCK' || die 'write uname'
#!/usr/bin/env bash
set -euo pipefail
printf 'uname %s\n' "$*" >> "$FAKE_LOG"
case "$*" in
  -s) printf '%s\n' "$FAKE_OS" ;;
  -m) printf '%s\n' "$FAKE_ARCH" ;;
  *) printf 'UNEXPECTED uname %s\n' "$*" >> "$FAKE_LOG"; exit 91 ;;
esac
MOCK

  cat > "$fake_bin/dotnet" <<'MOCK' || die 'write dotnet'
#!/usr/bin/env bash
set -euo pipefail
printf 'dotnet %s\n' "$*" >> "$FAKE_LOG"
case "$*" in
  --list-sdks) printf '%s\n' "$FAKE_SDKS" ;;
  --version)
    if [[ -f "$HOME/sdk-installed" ]] || printf '%s\n' "$FAKE_SDKS" | grep -q '^10\.'; then
      printf '10.0.100\n'
    else
      printf 'UNEXPECTED version check before SDK bootstrap\n' >> "$FAKE_LOG"; exit 92
    fi ;;
  'build -warnaserror -v q') exit "$FAKE_BUILD_EXIT" ;;
  'tool list -g')
    printf 'Package Id      Version      Commands\n'
    if [[ "$FAKE_TOOL_PRESENT" == 1 ]]; then printf 'csharp-ls       0.0.0        csharp-ls\n'; fi ;;
  'tool install -g csharp-ls') exit "$FAKE_TOOL_INSTALL_EXIT" ;;
  *) printf 'UNEXPECTED dotnet %s\n' "$*" >> "$FAKE_LOG"; exit 93 ;;
esac
MOCK

  cat > "$fake_bin/curl" <<'MOCK' || die 'write curl'
#!/usr/bin/env bash
set -euo pipefail
printf 'curl %s\n' "$*" >> "$FAKE_LOG"
if [[ "$*" != '-sSL https://dot.net/v1/dotnet-install.sh' ]]; then
  printf 'UNEXPECTED curl %s\n' "$*" >> "$FAKE_LOG"; exit 94
fi
if [[ "$FAKE_CURL_EXIT" != 0 ]]; then exit "$FAKE_CURL_EXIT"; fi
cat <<'INSTALLER'
set -euo pipefail
printf 'sdk-install %s\n' "$*" >> "$FAKE_LOG"
if [[ "$#" != 4 || "$1" != --channel || "$2" != 10.0 || "$3" != --install-dir || "$4" != "$HOME/.dotnet" ]]; then
  printf 'UNEXPECTED SDK installer arguments\n' >> "$FAKE_LOG"; exit 95
fi
: > "$HOME/sdk-installed"
INSTALLER
MOCK

  cat > "$fake_bin/git" <<'MOCK' || die 'write git'
#!/usr/bin/env bash
set -euo pipefail
printf 'git %s\n' "$*" >> "$FAKE_LOG"
case "$*" in
  'config user.name')
    if [[ -z "$FAKE_NAME" ]]; then exit 1; fi
    printf '%s\n' "$FAKE_NAME" ;;
  'config user.email')
    if [[ -z "$FAKE_EMAIL" ]]; then exit 1; fi
    printf '%s\n' "$FAKE_EMAIL" ;;
  *) printf 'UNEXPECTED git write or other command: %s\n' "$*" >> "$FAKE_LOG"; exit 96 ;;
esac
MOCK

  for platform in linux-x64 linux-arm64 darwin-x64 darwin-arm64; do
    mkdir -p "$driver/node/$platform" || die 'driver directory'
    cat > "$driver/node/$platform/node" <<'MOCK' || die 'write bundled node'
#!/usr/bin/env bash
set -euo pipefail
platform=${0%/node}
platform=${platform##*/}
printf 'driver %s | %s | %s | %s\n' "$platform" "${1-}" "${2-}" "${3-}" >> "$FAKE_LOG"
if [[ "$#" != 3 || "$1" != */package/cli.js || "$2" != install || "$3" != chromium ]]; then
  printf 'UNEXPECTED Playwright arguments\n' >> "$FAKE_LOG"; exit 97
fi
# A missing CLI must be caught by setup, not hidden by this harmless fake.
MOCK
    chmod +x "$driver/node/$platform/node" || die 'chmod bundled node'
  done
  chmod +x "$fake_bin/uname" "$fake_bin/dotnet" "$fake_bin/git" "$fake_bin/curl" || die 'chmod fakes'
}

run_setup() {
  if env -i HOME="$fake_home" PATH="$fake_bin" TMPDIR="$case_dir/tmp" LC_ALL=C \
    FAKE_LOG="$trace" FAKE_OS="$fake_os" FAKE_ARCH="$fake_arch" FAKE_SDKS="$fake_sdks" \
    FAKE_TOOL_PRESENT="$fake_tool_present" FAKE_TOOL_INSTALL_EXIT="$fake_tool_install_exit" \
    FAKE_BUILD_EXIT="$fake_build_exit" FAKE_CURL_EXIT="$fake_curl_exit" \
    FAKE_NAME="$fake_name" FAKE_EMAIL="$fake_email" \
    "$bash_binary" "$fake_repo/tools/setup-dev.sh" > "$output" 2>&1; then
    status=0
  else
    status=$?
  fi
  if grep -q '^UNEXPECTED' "$trace"; then
    printf 'Unexpected command reached a fake; no real command was run.\n' >&2
    return 1
  fi
}

expect_status() {
  if [[ "$status" != "$1" ]]; then
    printf 'Expected exit %s, got %s\n' "$1" "$status" >&2
    return 1
  fi
}

expect_failure() {
  if [[ "$status" == 0 ]]; then printf 'Expected nonzero exit, got 0\n' >&2; return 1; fi
}

contains() {
  if ! grep -F -q -- "$2" "$1"; then printf 'Expected %s to contain: %s\n' "${1##*/}" "$2" >&2; return 1; fi
}

matches() {
  if ! grep -E -i -q -- "$2" "$1"; then printf 'Expected %s to match: %s\n' "${1##*/}" "$2" >&2; return 1; fi
}

excludes() {
  if grep -F -q -- "$2" "$1"; then printf 'Expected %s not to contain: %s\n' "${1##*/}" "$2" >&2; return 1; fi
}

identity_is_read_only() {
  contains "$trace" 'git config user.name' || return 1
  contains "$trace" 'git config user.email' || return 1
  local count
  count=$(grep -c '^git ' "$trace")
  if [[ "$count" != 2 ]]; then printf 'Expected exactly two read-only git calls, got %s\n' "$count" >&2; return 1; fi
}

platform_mapping() {
  new_case "$1" "$2"
  run_setup || return 1
  expect_status 0 || return 1
  contains "$trace" "driver $3 | " || return 1
  contains "$trace" '/package/cli.js | install | chromium' || return 1
  local count
  count=$(grep -c '^driver ' "$trace")
  if [[ "$count" != 1 ]]; then printf 'Expected one bundled driver invocation, got %s\n' "$count" >&2; return 1; fi
  contains "$trace" 'dotnet --list-sdks' || return 1
  contains "$trace" 'dotnet --version' || return 1
  contains "$trace" 'dotnet build -warnaserror -v q' || return 1
  excludes "$trace" 'curl ' || return 1
  excludes "$trace" 'dotnet tool install' || return 1
  contains "$output" 'git identity: Synthetic Developer <developer@example.invalid>' || return 1
  identity_is_read_only
}

unsupported_platform() {
  new_case "$1" "$2"
  run_setup || return 1
  expect_failure || return 1
  matches "$output" 'unsupported' || return 1
  contains "$output" "$1" || return 1
  contains "$output" "$2" || return 1
  excludes "$trace" 'driver '
}

missing_bundle() {
  new_case Linux x86_64
  case "$1" in
    missing-node) rm "$driver/node/linux-x64/node" || die 'remove fake node' ;;
    nonexecutable-node) chmod -x "$driver/node/linux-x64/node" || die 'make fake node nonexecutable' ;;
    missing-cli) rm "$driver/package/cli.js" || die 'remove fake CLI' ;;
  esac
  run_setup || return 1
  expect_failure || return 1
  matches "$output" 'playwright' || return 1
  matches "$output" 'missing|not found|not executable|non.?executable|incomplete|unavailable' || return 1
  if [[ "$1" == missing-cli ]]; then contains "$output" 'cli.js' || return 1
  else contains "$output" 'node' || return 1; fi
  excludes "$trace" 'driver '
}

installed_sdk_is_detected_among_other_versions() {
  new_case Linux x86_64
  fake_sdks=$'8.0.100 [synthetic-sdk]\n10.0.100 [synthetic-sdk]'
  run_setup || return 1
  expect_status 0 || return 1
  excludes "$trace" 'curl ' || return 1
  excludes "$trace" 'sdk-install '
}

missing_sdk_uses_only_the_mock_installer() {
  new_case Linux x86_64
  fake_sdks='9.0.100 [synthetic-sdk]'
  run_setup || return 1
  expect_status 0 || return 1
  contains "$trace" 'curl -sSL https://dot.net/v1/dotnet-install.sh' || return 1
  contains "$trace" "sdk-install --channel 10.0 --install-dir $fake_home/.dotnet" || return 1
  [[ -f "$fake_home/sdk-installed" ]] || { printf 'Mock SDK installer did not run\n' >&2; return 1; }
  contains "$trace" 'driver linux-x64 | '
}

optional_language_server() {
  new_case Linux x86_64
  fake_tool_present=0
  fake_tool_install_exit=$1
  run_setup || return 1
  expect_status 0 || return 1
  contains "$trace" 'dotnet tool install -g csharp-ls' || return 1
  if [[ "$1" != 0 ]]; then contains "$output" 'csharp-ls not installed (optional)' || return 1; fi
  identity_is_read_only
}

missing_identity_is_reported_never_written() {
  new_case Linux x86_64
  if [[ "$1" == name ]]; then fake_name=''; else fake_email=''; fi
  run_setup || return 1
  expect_status 0 || return 1
  contains "$output" 'Set your own identity:' || return 1
  contains "$output" 'git config user.name' || return 1
  contains "$output" 'git config user.email' || return 1
  identity_is_read_only
}

build_failure_prevents_driver_and_tool_install() {
  new_case Linux x86_64
  fake_build_exit=42
  run_setup || return 1
  expect_failure || return 1
  excludes "$trace" 'driver ' || return 1
  excludes "$trace" 'dotnet tool install'
}

sdk_download_failure_prevents_build_and_driver() {
  new_case Linux x86_64
  fake_sdks=''
  fake_curl_exit=22
  run_setup || return 1
  expect_failure || return 1
  excludes "$trace" 'dotnet build' || return 1
  excludes "$trace" 'driver '
}

run_test() {
  local label=$1
  shift
  total=$((total + 1))
  if "$@"; then
    passed=$((passed + 1))
    printf 'PASS: %s\n' "$label"
  else
    failed=$((failed + 1))
    printf 'FAIL: %s (setup exit %s)\n' "$label" "$status"
    printf '%s\n' '--- setup output ---'
    cat "$output"
    printf '%s\n' '--- mocked commands ---'
    cat "$trace"
  fi
}

run_test 'Linux x86_64 -> linux-x64 (mocked)' platform_mapping Linux x86_64 linux-x64
run_test 'Linux aarch64 -> linux-arm64 (mocked)' platform_mapping Linux aarch64 linux-arm64
run_test 'Linux arm64 -> linux-arm64 (mocked)' platform_mapping Linux arm64 linux-arm64
run_test 'Darwin x86_64 -> darwin-x64 (mocked)' platform_mapping Darwin x86_64 darwin-x64
run_test 'Darwin arm64 -> darwin-arm64 (mocked)' platform_mapping Darwin arm64 darwin-arm64
run_test 'Darwin aarch64 -> darwin-arm64 (mocked)' platform_mapping Darwin aarch64 darwin-arm64
run_test 'unsupported OS does not run a driver' unsupported_platform FreeBSD x86_64
run_test 'unsupported Linux architecture does not run a driver' unsupported_platform Linux riscv64
run_test 'unsupported Darwin architecture does not run a driver' unsupported_platform Darwin i386
run_test 'missing bundled node fails clearly' missing_bundle missing-node
run_test 'nonexecutable bundled node fails clearly' missing_bundle nonexecutable-node
run_test 'missing bundled cli.js fails before invoking a driver' missing_bundle missing-cli
run_test '.NET 10 is detected among other SDKs' installed_sdk_is_detected_among_other_versions
run_test 'missing .NET 10 uses a fake installer and quoted HOME' missing_sdk_uses_only_the_mock_installer
run_test 'missing csharp-ls is installed by the fake only' optional_language_server 0
run_test 'csharp-ls install failure is optional' optional_language_server 1
run_test 'missing git name is reported without writing identity' missing_identity_is_reported_never_written name
run_test 'missing git email is reported without writing identity' missing_identity_is_reported_never_written email
run_test 'build failure stops before driver and tools' build_failure_prevents_driver_and_tool_install
run_test 'SDK download failure stops before build and driver' sdk_download_failure_prevents_build_and_driver

printf 'setup-dev tests: %s passed, %s failed, %s total (mocked platforms; no installs or network)\n' "$passed" "$failed" "$total"
if [[ "$failed" != 0 ]]; then exit 1; fi
