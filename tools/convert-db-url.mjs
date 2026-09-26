import { createInterface } from "node:readline";
import { spawn } from "node:child_process";
import { pipeline } from "node:stream/promises";

function quoteValue(value) {
  if (/[";]/.test(value) || /^\s|\s$/.test(value)) {
    return `"${value.replace(/"/g, '""')}"`;
  }
  return value;
}

export function convert(url) {
  const parsed = new URL(url);
  const host = parsed.hostname;
  const port = parsed.port || "5432";
  const database = parsed.pathname.replace(/^\/+/, "") || "postgres";
  const username = decodeSafe(parsed.username);
  const password = decodeSafe(parsed.password);

  const parts = [
    `Host=${quoteValue(host)}`,
    `Port=${quoteValue(port)}`,
    `Database=${quoteValue(database)}`,
  ];
  if (username) parts.push(`Username=${quoteValue(username)}`);
  if (password) parts.push(`Password=${quoteValue(password)}`);
  parts.push("SSL Mode=Require", "Trust Server Certificate=true");

  return parts.join(";");
}

function decodeSafe(value) {
  if (!value) return value;
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

function copyToClipboard(text) {
  return new Promise((resolve, reject) => {
    const child = spawn("clip.exe", [], { stdio: ["pipe", "ignore", "inherit"] });
    child.on("error", reject);
    child.on("close", (code) => (code === 0 ? resolve() : reject(new Error(`clip.exe salió con código ${code}`))));
    child.stdin.end(text);
  });
}

function printResult(connectionString, copy) {
  console.log("\n--- Connection string ---");
  console.log(connectionString);
  console.log("------------------------\n");
  if (copy) {
    copyToClipboard(connectionString)
      .then(() => console.log("Copiada al portapapeles."))
      .catch((err) => console.error(`No se pudo copiar: ${err.message}`));
  }
}

async function interactive(copy) {
  const rl = createInterface({ input: process.stdin, output: process.stdout });
  const ask = (q) => new Promise((resolve) => rl.question(q, resolve));

  console.log("Pegá la DATABASE_URL de Railway y presioná Enter (vacío para salir).");
  while (true) {
    const input = (await ask("\nDATABASE_URL > ")).trim();
    if (!input) break;
    try {
      printResult(convert(input), copy);
    } catch (err) {
      console.error(`No es una URL válida: ${err.message}`);
    }
  }
  rl.close();
}

const copy = process.argv.includes("--copy");
const argUrl = process.argv.slice(2).find((a) => !a.startsWith("--"));

if (argUrl) {
  try {
    printResult(convert(argUrl), copy);
  } catch (err) {
    console.error(`No es una URL válida: ${err.message}`);
    process.exit(1);
  }
} else {
  await interactive(copy);
}