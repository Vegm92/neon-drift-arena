import { cp, readFile, writeFile, rm } from "node:fs/promises";


const at = (p) => new URL(`../${p}`, import.meta.url);
const siteOnly = ["index.html", "og.jpg", "trailer.mp4", "trailer.jpg", "still-action.jpg", "still-arena.jpg", "still-race.jpg"];

await rm(at("dist-game"), { recursive: true, force: true });
await cp(at("dist"), at("dist-game"), { recursive: true });

const { siteUrl } = JSON.parse(await readFile(at("strings.json"), "utf8"));
const play = await readFile(at("dist-game/play/index.html"), "utf8");
await writeFile(at("dist-game/index.html.tmp"), play.replaceAll("../", "./").replace('class="back" href="./"', `class="back" href="${siteUrl}"`));
await rm(at("dist-game/play"), { recursive: true });
await Promise.all(siteOnly.map((p) => rm(at(`dist-game/${p}`), { force: true })));
await cp(at("dist-game/index.html.tmp"), at("dist-game/index.html"));
await rm(at("dist-game/index.html.tmp"));
