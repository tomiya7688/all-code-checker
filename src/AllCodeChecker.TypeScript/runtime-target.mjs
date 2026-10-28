export function validateRuntimeTarget(rid, platform, arch, glibcVersion) {
  if (!rid) return;

  const match = /^(win|linux|osx)-(x86|x64|arm|arm64)$/.exec(rid);
  if (!match) {
    throw new Error(
      `Unsupported target RID '${rid}'; use a portable RID matching the host Node runtime.`,
    );
  }
  const hostOs = { win32: "win", linux: "linux", darwin: "osx" }[platform];
  const hostArch = arch === "ia32" ? "x86" : arch;
  if (match[1] !== hostOs || match[2] !== hostArch) {
    throw new Error(
      `Target RID '${rid}' does not match host Node platform '${platform}' and architecture '${arch}'.`,
    );
  }
  if (hostOs === "linux" && !glibcVersion) {
    throw new Error(
      "Linux RID packaging requires a glibc-based host Node runtime.",
    );
  }
}
