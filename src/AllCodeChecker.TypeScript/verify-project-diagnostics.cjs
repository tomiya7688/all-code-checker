"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");

const helper = path.join(__dirname, "analyze.cjs");

function withProject(options, sourceText, run) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), "acc-project-"));
  const source = path.join(directory, "source.ts");
  const config = path.join(directory, "tsconfig.json");
  try {
    fs.writeFileSync(source, sourceText);
    fs.writeFileSync(
      config,
      JSON.stringify({ compilerOptions: options, include: ["*.ts"] }),
    );
    const analyze = (sourceFiles) => {
      const result = spawnSync(process.execPath, [helper], {
        input: JSON.stringify({
          boundaryPath: directory,
          sourceFiles,
          inputKind: "Project",
        }),
        encoding: "utf8",
      });
      assert.equal(result.status, 0, result.stderr);
      return JSON.parse(result.stdout);
    };
    run(source, analyze);
  } finally {
    fs.rmSync(source, { force: true });
    fs.rmSync(config, { force: true });
    fs.rmdirSync(directory);
  }
}

test("compiler option diagnostics mark analysis partial without discarding symbols", () => {
  withProject(
    { strict: false, exactOptionalPropertyTypes: true },
    "const value: string = 'ok';\n",
    (source, analyze) => {
      for (const sources of [[source], []]) {
        const output = analyze(sources);
        assert.equal(output.partial, true);
        assert.ok(
          output.diagnostics.some(
            (item) =>
              item.ruleId === "ACI001" && item.reason.includes("TS5052"),
          ),
        );
        if (sources.length)
          assert.ok(output.symbols.some((item) => item.name === "value"));
      }
    },
  );
});

test("intersection types expose properties from both sides", () => {
  withProject(
    {},
    "const dto: { id: string } & { name: string } = { id: '1', name: 'n' };\n",
    (source, analyze) => {
      const output = analyze([source]);
      assert.equal(output.partial, false);
      const dto = output.symbols.find((item) => item.name === "dto");
      assert.ok(dto);
      assert.deepEqual([...dto.properties].sort(), ["id", "name"]);
    },
  );
});
