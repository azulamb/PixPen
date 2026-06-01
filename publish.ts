const PROJECT = "PixPen/PixPen.csproj";
const OUT_DIR = "bin";
const EXE = `${OUT_DIR}/PixPen.exe`;

// 1. Publish release build to bin/
console.log("Publishing release...");
const build = await new Deno.Command("dotnet", {
  args: [
    "publish",
    PROJECT,
    "-c",
    "Release",
    "-r",
    "win-x64",
    "--self-contained",
    "false",
    "/p:PublishSingleFile=true",
    "/p:EnableCompressionInSingleFile=true",
    "-o",
    OUT_DIR,
  ],
  stdout: "inherit",
  stderr: "inherit",
}).output();

if (!build.success) {
  console.error("Publish failed");
  Deno.exit(1);
}
console.log("Publish succeeded");

// 2. Get version from the built exe
const ps = await new Deno.Command("powershell", {
  args: [
    "-NoProfile",
    "-NonInteractive",
    "-Command",
    `(Get-Item '${EXE}').VersionInfo.ProductVersion`,
  ],
}).output();

if (!ps.success) {
  console.error(`Failed to read version from ${EXE}`);
  Deno.exit(1);
}

const version = new TextDecoder().decode(ps.stdout).trim().split("+")[0];
if (!version) {
  console.error("Version is empty");
  Deno.exit(1);
}
console.log(`Version: ${version}`);

// 3. Check if tag v{version} already exists
const tagList = await new Deno.Command("git", {
  args: ["tag", "-l", `v${version}`],
}).output();

const existing = new TextDecoder().decode(tagList.stdout).trim();
if (existing) {
  console.error(`Error: tag v${version} already exists`);
  Deno.exit(1);
}

console.log(`OK: v${version} is ready to release`);
