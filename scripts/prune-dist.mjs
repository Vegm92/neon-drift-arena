import { rm } from "node:fs/promises";

const devOnly = ["models"];

await Promise.all(devOnly.map((p) => rm(new URL(`../dist/${p}`, import.meta.url), { recursive: true, force: true })));
