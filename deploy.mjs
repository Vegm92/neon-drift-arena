import { cpSync, mkdirSync, rmSync } from "node:fs";
import { execSync } from "node:child_process";

const run = (cmd) => execSync(cmd, { stdio: "inherit" });
run("npm run build");
rmSync(".deploy", { recursive: true, force: true });
mkdirSync(".deploy");
cpSync("deploy", ".deploy", { recursive: true });
cpSync("dist", ".deploy/site", { recursive: true });
run("railway up .deploy --path-as-root --no-gitignore --ci --service site");
