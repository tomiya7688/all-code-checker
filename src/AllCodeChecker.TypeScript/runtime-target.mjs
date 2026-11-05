const linuxSystemLibraries = new Set([
  "linux-vdso.so.1",
  "libc.so.6",
  "libm.so.6",
  "libpthread.so.0",
  "libdl.so.2",
  "librt.so.1",
  "libstdc++.so.6",
  "libgcc_s.so.1",
  "libatomic.so.1",
]);

export function validateRuntimeTarget(rid, platform, arch, report) {
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
  if (hostOs === "linux" && !report?.header?.glibcVersionRuntime) {
    throw new Error(
      "Linux RID packaging requires a glibc-based host Node runtime.",
    );
  }
  if (hostOs === "linux") {
    if (
      !Array.isArray(report.sharedObjects) ||
      report.sharedObjects.length === 0
    ) {
      throw new Error("Cannot inspect Linux Node shared-library dependencies.");
    }
    const unexpected = report.sharedObjects.filter((library) => {
      if (typeof library !== "string") return true;
      const name = library.split("/").pop();
      return (
        !linuxSystemLibraries.has(name) &&
        !/^ld-linux(?:-x86-64|-aarch64|-armhf)?\.so\.\d+$/.test(name)
      );
    });
    if (unexpected.length) {
      throw new Error(
        `Linux Node depends on unsupported shared libraries: ${unexpected.join(", ")}`,
      );
    }
  }
}
