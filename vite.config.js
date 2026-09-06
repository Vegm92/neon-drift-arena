import { readFileSync, writeFileSync } from "node:fs";

const domain = new URL("src/Core/Domain.fs", import.meta.url);
const strings = JSON.parse(readFileSync(new URL("strings.json", import.meta.url), "utf8"));

const storeRow = () =>
  strings.stores
    .map((s) =>
      s.url
        ? `<a class="store" href="${s.url}" target="_blank" rel="noopener">${s.label}</a>`
        : `<span class="store soon">${s.label}<i>${strings.storeSoon}</i></span>`,
    )
    .join("");

const siteStrings = {
  name: "site-strings",
  transformIndexHtml: (html) =>
    html.replace("{{stores}}", storeRow).replace(/\{\{(\w+)\}\}/g, (_, k) => strings[k] ?? `{{${k}}}`),
};

const bakeTweaks = {
  name: "bake-tweaks",
  configureServer(server) {
    server.middlewares.use("/__tweaks", (req, res) => {
      let body = "";
      req.on("data", (c) => (body += c));
      req.on("end", () => {
        let src = readFileSync(domain, "utf8");
        for (const [name, v] of Object.entries(JSON.parse(body))) {
          const lit = String(+(+v).toPrecision(4));
          src = src.replace(new RegExp(`(let mutable ${name} = )[-0-9.eE]+`), `$1${lit.includes(".") || lit.includes("e") ? lit : lit + "."}`);
        }
        writeFileSync(domain, src);
        res.end("ok");
      });
    });
  },
};

const modelTune = {
  name: "model-tune",
  configureServer(server) {
    server.middlewares.use("/__model-tune", (req, res) => {
      let body = "";
      req.on("data", (c) => (body += c));
      req.on("end", () => {
        writeFileSync(new URL("public/models/tune.json", import.meta.url), body);
        res.end("ok");
      });
    });
  },
};

const maps = new URL("src/Core/Maps.fs", import.meta.url);
const locale = new URL("src/Core/Strings.fs", import.meta.url);

const mapEdit = {
  name: "map-edit",
  configureServer(server) {
    server.middlewares.use("/__maps", (req, res) => {
      let body = "";
      req.on("data", (c) => (body += c));
      req.on("end", () => {
        const { custom, names } = JSON.parse(body);
        const src = readFileSync(maps, "utf8");
        const a = src.indexOf("let private custom: Layout array =");
        const b = src.indexOf("\nlet layouts", a);
        writeFileSync(maps, src.slice(0, a) + "let private custom: Layout array =\n" + custom + "\n" + src.slice(b + 1));
        const loc = readFileSync(locale, "utf8");
        const list = names.map((n) => `"${n.replace(/"/g, "")}"`).join("; ");
        writeFileSync(locale, loc.replace(/(Arenas = \[\| ).*?( \|\])/, `$1${list}$2`));
        res.end("ok");
      });
    });
  },
};

const phonePad = {
  name: "phone-pad",
  configureServer(server) {
    let host = null;
    const seen = new WeakSet();
    server.ws.on("nda:pad", (data) => server.ws.send("nda:pad", data));
    server.ws.on("nda:host", (data, client) => {
      if (!seen.has(client)) {
        seen.add(client);
        client.socket.on("close", () => host === client && (host = null));
        host = client;
      }
      host ??= client;
      if (host === client) server.ws.send("nda:host", data);
    });
    server.ws.on("nda:state", (data, client) => {
      if (client === host) for (const c of server.ws.clients) if (c !== host) c.send("nda:state", data);
    });
    server.middlewares.use("/__pad-url", (_, res) => {
      const net = server.resolvedUrls?.network[0] ?? "";
      res.end(net && net + "pad.html");
    });
  },
};

export default {
  base: "./",
  plugins: [siteStrings, bakeTweaks, phonePad, mapEdit, modelTune],
  build: { rollupOptions: { input: { main: "index.html", play: "play/index.html", pad: "pad.html", combat: "combat.html" } } },
  server: { port: process.env.PORT ? +process.env.PORT : undefined },
};
