"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");

const helper = path.join(__dirname, "analyze.cjs");
const cases = [
  [{}, true],
  [{ strict: false }, false],
  [{ strict: true }, true],
  [{ strict: true, strictNullChecks: false }, false],
  [{ strict: false, strictNullChecks: true }, true],
];

for (const [compilerOptions, expected] of cases) {
  test(`strictNullChecks follows compiler options ${JSON.stringify(compilerOptions)}`, () => {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), "acc-strict-"));
    try {
      const source = path.join(directory, "source.ts");
      fs.writeFileSync(source, "const value: string = null;\n");
      fs.writeFileSync(
        path.join(directory, "tsconfig.json"),
        JSON.stringify({ compilerOptions, include: ["*.ts"] }),
      );
      for (const sourceFiles of [[source], []]) {
        const result = spawnSync(process.execPath, [helper], {
          input: JSON.stringify({
            boundaryPath: directory,
            sourceFiles,
            inputKind: "Project",
          }),
          encoding: "utf8",
        });
        assert.equal(result.status, 0, result.stderr);
        const output = JSON.parse(result.stdout);
        assert.equal(output.strictNullChecks, expected);
        if (sourceFiles.length) {
          assert.equal(
            output.diagnostics.some((item) => item.reason.includes("TS2322")),
            expected,
          );
        } else {
          assert.equal(output.partial, true);
        }
      }
    } finally {
      fs.rmSync(path.join(directory, "source.ts"), { force: true });
      fs.rmSync(path.join(directory, "tsconfig.json"), { force: true });
      fs.rmdirSync(directory);
    }
  });
}

test("source files without tsconfig and inherited strict settings use compiler behavior", () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), "acc-strict-"));
  const source = path.join(directory, "source.ts");
  const config = path.join(directory, "tsconfig.json");
  const base = path.join(directory, "base.json");
  try {
    fs.writeFileSync(source, "const value: string = null;\n");
    const run = () => {
      const result = spawnSync(process.execPath, [helper], {
        input: JSON.stringify({
          boundaryPath: directory,
          sourceFiles: [source],
          inputKind: "SourceFile",
        }),
        encoding: "utf8",
      });
      assert.equal(result.status, 0, result.stderr);
      return JSON.parse(result.stdout);
    };
    const withoutConfig = run();
    assert.equal(withoutConfig.strictNullChecks, true);
    assert.equal(
      withoutConfig.diagnostics.some((item) => item.reason.includes("TS2322")),
      true,
    );
    fs.writeFileSync(
      base,
      JSON.stringify({ compilerOptions: { strict: false } }),
    );
    fs.writeFileSync(config, JSON.stringify({ extends: "./base.json" }));
    const inherited = run();
    assert.equal(inherited.strictNullChecks, false);
    assert.equal(
      inherited.diagnostics.some((item) => item.reason.includes("TS2322")),
      false,
    );
  } finally {
    for (const file of [source, config, base]) fs.rmSync(file, { force: true });
    fs.rmdirSync(directory);
  }
});
