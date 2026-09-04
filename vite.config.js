import { readFileSync, writeFileSync } from "node:fs";

const domain = new URL("src/Core/Domain.fs", import.meta.url);

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

export default { plugins: [bakeTweaks, phonePad], server: { port: process.env.PORT ? +process.env.PORT : undefined } };
