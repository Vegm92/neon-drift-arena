import { readFileSync, writeFileSync } from "node:fs";

const domain = new URL("src/Domain.fs", import.meta.url);

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

export default { plugins: [bakeTweaks] };
