import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import { validateRuntimeTarget } from "./runtime-target.mjs";

test("portable RIDs must match the host Node platform and architecture", () => {
  for (const [rid, platform, arch] of [
    ["win-x64", "win32", "x64"],
    ["win-x86", "win32", "ia32"],
    ["linux-arm64", "linux", "arm64"],
    ["osx-arm64", "darwin", "arm64"],
  ]) {
    assert.doesNotThrow(() =>
      validateRuntimeTarget(rid, platform, arch, "2.38"),
    );
  }
  assert.doesNotThrow(() => validateRuntimeTarget("", "win32", "x64"));
  assert.doesNotThrow(() => validateRuntimeTarget("win-x64", "win32", "x64"));
  assert.doesNotThrow(() =>
    validateRuntimeTarget("osx-arm64", "darwin", "arm64"),
  );
  assert.throws(
    () => validateRuntimeTarget("win-arm64", "win32", "x64"),
    /does not match/,
  );
  assert.throws(
    () => validateRuntimeTarget("linux-x64", "win32", "x64"),
    /does not match/,
  );
  assert.throws(
    () => validateRuntimeTarget("linux-musl-x64", "linux", "x64"),
    /Unsupported/,
  );
  assert.throws(
    () => validateRuntimeTarget("linux-unknown", "linux", "x64"),
    /Unsupported/,
  );
  assert.throws(
    () => validateRuntimeTarget("linux-x64", "linux", "x64"),
    /glibc-based host Node/,
  );
  assert.throws(
    () => validateRuntimeTarget("linux-arm64", "linux", "arm64"),
    /glibc-based host Node/,
  );
});

test("mismatched publish RID fails before creating the runtime directory", () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), "acc-rid-"));
  try {
    const helper = fileURLToPath(
      new URL("./package-runtime.mjs", import.meta.url),
    );
    const mismatchedRid =
      process.platform === "win32" ? "linux-x64" : "win-x64";
    const result = spawnSync(
      process.execPath,
      [helper, path.join(directory, "app.dll"), mismatchedRid],
      { encoding: "utf8" },
    );
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /does not match host Node/);
    assert.equal(
      fs.existsSync(path.join(directory, "typescript-runtime")),
      false,
    );
  } finally {
    if (fs.readdirSync(directory).length === 0) fs.rmdirSync(directory);
  }
});
