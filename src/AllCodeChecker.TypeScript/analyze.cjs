"use strict";

const fs = require("node:fs");
const path = require("node:path");
const ts = require("typescript");

const normalize = file => {
  const resolved = path.resolve(file);
  return process.platform === "win32" ? resolved.toLowerCase() : resolved;
};

function location(diagnostic) {
  if (!diagnostic.file || diagnostic.start === undefined) return {};
  const start = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start);
  return { path: diagnostic.file.fileName, line: start.line + 1, column: start.character + 1 };
}

function diagnosticOf(diagnostic, ruleId) {
  return {
    ruleId,
    severity: diagnostic.category === ts.DiagnosticCategory.Error ? "Danger"
      : diagnostic.category === ts.DiagnosticCategory.Warning ? "Warning" : "Notice",
    reason: `TS${diagnostic.code}: ${ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n")}`,
    ...location(diagnostic)
  };
}

function hasFlag(type, flag) {
  return Boolean(type.flags & flag) || Boolean(type.isUnion?.() && type.types.some(part => hasFlag(part, flag)));
}

function symbolsOf(source, checker) {
  const symbols = [];
  const wanted = node => ts.isVariableDeclaration(node) || ts.isParameter(node)
    || ts.isPropertyDeclaration(node) || ts.isPropertySignature(node)
    || ts.isFunctionDeclaration(node) || ts.isInterfaceDeclaration(node)
    || ts.isTypeAliasDeclaration(node);

  function visit(node) {
    if (wanted(node) && node.name && ts.isIdentifier(node.name)) {
      const symbol = checker.getSymbolAtLocation(node.name);
      if (symbol) {
        const type = checker.getTypeAtLocation(node.name);
        const line = source.getLineAndCharacterOfPosition(node.name.getStart(source)).line + 1;
        const isObject = Boolean(type.flags & ts.TypeFlags.Object);
        symbols.push({
          filePath: source.fileName,
          name: symbol.getName(),
          type: checker.typeToString(type),
          line,
          isAny: hasFlag(type, ts.TypeFlags.Any),
          isUnknown: hasFlag(type, ts.TypeFlags.Unknown),
          includesNull: hasFlag(type, ts.TypeFlags.Null),
          includesUndefined: hasFlag(type, ts.TypeFlags.Undefined),
          properties: isObject ? checker.getPropertiesOfType(type).map(item => item.getName()).slice(0, 64) : []
        });
      }
    }
    ts.forEachChild(node, visit);
  }
  visit(source);
  return symbols;
}

function testScripts(boundary, diagnostics) {
  const packageFile = path.join(boundary, "package.json");
  if (!fs.existsSync(packageFile)) return [];
  try {
    const packageData = JSON.parse(fs.readFileSync(packageFile, "utf8"));
    return Object.entries(packageData.scripts || {})
      .filter(([name, command]) => /test/i.test(name) || /\b(vitest|jest|mocha|playwright)\b/.test(String(command)))
      .map(([name]) => name);
  } catch (error) {
    diagnostics.push({ ruleId: "ACI001", severity: "Danger",
      reason: `package.jsonを読み取れません: ${error.message}`, path: packageFile });
    return [];
  }
}

function analyze(request) {
  const boundary = path.resolve(request.boundaryPath);
  const configPath = path.join(boundary, "tsconfig.json");
  const allowed = new Set(request.sourceFiles.map(normalize));
  const diagnostics = [];
  let partial = false;
  let options = { noEmit: true, strict: true };
  let rootNames = request.sourceFiles;

  if (fs.existsSync(configPath)) {
    const config = ts.readConfigFile(configPath, ts.sys.readFile);
    if (config.error) {
      diagnostics.push(diagnosticOf(config.error, "ACI001"));
      partial = true;
    } else {
      const parsed = ts.parseJsonConfigFileContent(config.config, ts.sys, boundary, { noEmit: true }, configPath);
      options = parsed.options;
      diagnostics.push(...parsed.errors.map(item => diagnosticOf(item, "ACI001")));
      partial ||= parsed.errors.length > 0;
      if (request.inputKind !== "SourceFile") {
        rootNames = parsed.fileNames.filter(file => allowed.has(normalize(file)));
      }
    }
  }

  if (rootNames.length === 0) {
    return { partial: true, reason: "tsconfigの対象にTypeScript sourceがありません。", diagnostics,
      projectPath: configPath, strictNullChecks: Boolean(options.strictNullChecks ?? options.strict),
      symbols: [], testScripts: testScripts(boundary, diagnostics) };
  }

  const program = ts.createProgram({ rootNames, options });
  const checker = program.getTypeChecker();
  const symbols = [];
  for (const source of program.getSourceFiles()) {
    if (!allowed.has(normalize(source.fileName))) continue;
    const syntax = program.getSyntacticDiagnostics(source);
    diagnostics.push(...syntax.map(item => diagnosticOf(item, "ACI002")));
    diagnostics.push(...program.getSemanticDiagnostics(source).map(item => diagnosticOf(item, "ACI001")));
    partial ||= syntax.length > 0;
    symbols.push(...symbolsOf(source, checker));
  }
  const scripts = testScripts(boundary, diagnostics);
  partial ||= diagnostics.some(item => item.path === path.join(boundary, "package.json"));
  return { partial, reason: partial ? "TypeScript projectの一部を解析できませんでした。" : null,
    diagnostics, projectPath: fs.existsSync(configPath) ? configPath : boundary,
    strictNullChecks: Boolean(options.strictNullChecks ?? options.strict),
    symbols, testScripts: scripts };
}

try {
  const request = JSON.parse(fs.readFileSync(0, "utf8"));
  process.stdout.write(JSON.stringify(analyze(request)));
} catch (error) {
  process.stderr.write(String(error?.stack || error));
  process.exitCode = 2;
}
