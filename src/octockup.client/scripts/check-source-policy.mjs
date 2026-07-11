import { readdir, readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const sourceRoot = fileURLToPath(new URL("../src", import.meta.url));
const sourceExtensions = new Set([".ts", ".tsx", ".css"]);
const viewportUnitPattern = /(?:^|[^a-z])\d*\.?\d+(?:d|s|l)?v(?:h|w)(?![a-z])/gi;

const files = await findSourceFiles(sourceRoot);
const failures = [];
for (const file of files) {
  const source = await readFile(file, "utf8");
  const code = stripComments(source);
  const lines = code.split(/\r?\n/);
  lines.forEach((line, index) => {
    viewportUnitPattern.lastIndex = 0;
    if (viewportUnitPattern.test(line)) {
      failures.push(
        `${path.relative(sourceRoot, file)}:${index + 1}: viewport units are prohibited`,
      );
    }
  });
}

if (failures.length > 0) {
  process.stderr.write(failures.join("\n") + "\n");
  process.exitCode = 1;
}

async function findSourceFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const nested = await Promise.all(
    entries.map(async (entry) => {
      const fullPath = path.join(directory, entry.name);
      if (entry.isDirectory()) {
        return findSourceFiles(fullPath);
      }
      return sourceExtensions.has(path.extname(entry.name)) ? [fullPath] : [];
    }),
  );
  return nested.flat();
}

function stripComments(source) {
  return source
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .replace(/(^|[^:])\/\/.*$/gm, "$1");
}
