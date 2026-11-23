import { createServer } from "node:http";
import { spawn } from "node:child_process";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { isAbsolute, join, relative, resolve } from "node:path";
import { tmpdir } from "node:os";

const edge = process.env.EDGE_EXECUTABLE ?? join(process.env["ProgramFiles(x86)"] ?? "C:/Program Files (x86)",
  "Microsoft", "Edge", "Application", "msedge.exe");
const type = {html: "text/html", js: "text/javascript", css: "text/css"};
const files = new Map([
  ["/watch/1", [new URL("./edge-fixture.html", import.meta.url), "html"]],
  ["/settings.js", [new URL("../extension/settings.js", import.meta.url), "js"]],
  ["/player.js", [new URL("../extension/player.js", import.meta.url), "js"]],
  ["/player.css", [new URL("../extension/player.css", import.meta.url), "css"]]
]);
const server = createServer(async (request, response) => {
  const entry = files.get(request.url);
  if (!entry) { response.writeHead(404).end(); return; }
  response.setHeader("Content-Type", type[entry[1]] + "; charset=utf-8");
  response.end(await readFile(entry[0]));
});
const profile = await mkdtemp(join(tmpdir(), "subtitle-edge-fixture-"));
try {
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  const browser = spawn(edge, ["--headless=new", "--no-first-run", "--dump-dom", "--virtual-time-budget=5500",
    "--user-data-dir=" + profile, "http://127.0.0.1:" + server.address().port + "/watch/1"],
    {windowsHide: true, stdio: ["ignore", "pipe", "pipe"]});
  let output = "", errors = "";
  browser.stdout.on("data", piece => { output += piece; });
  browser.stderr.on("data", piece => { errors += piece; });
  const kill = setTimeout(() => browser.kill(), 20000);
  try {
    const code = await new Promise((resolve, reject) => { browser.on("error", reject); browser.on("close", resolve); });
    const result = /<pre id="result">([^<]+)<\/pre>/.exec(output)?.[1];
    if (code !== 0 || !result?.startsWith("PASS:")) throw new Error(result ?? errors);
    console.log(result);
  } finally { clearTimeout(kill); }
} finally {
  server.close();
  const child = relative(resolve(tmpdir()), resolve(profile));
  if (!isAbsolute(child) && /^subtitle-edge-fixture-[a-z0-9]+$/i.test(child)) {
    await rm(profile, {recursive: true, force: true, maxRetries: 6, retryDelay: 300}).catch(() => {});
  }
}
