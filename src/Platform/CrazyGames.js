let sdk = null;

export async function init(onMute) {
  const cg = window.CrazyGames?.SDK;
  if (!cg) return;
  try {
    await cg.init();
    sdk = cg;
    sdk.game.addSettingsChangeListener((s) => onMute(!!s.muteAudio));
    onMute(!!sdk.game.settings?.muteAudio);
  } catch (e) {
    console.error("CrazyGames SDK init failed", e);
    sdk = null;
  }
}

const call = (f) => { try { f(); } catch (e) { console.error(e); } };

export function gameplayStart() { if (sdk) call(() => sdk.game.gameplayStart()); }
export function gameplayStop() { if (sdk) call(() => sdk.game.gameplayStop()); }

export function getInviteRoom() {
  if (sdk) {
    try { const r = sdk.game.getInviteParam("roomId"); if (r) return r; } catch (e) { console.error(e); }
  }
  return new URLSearchParams(window.location.search).get("room") ?? "";
}

export function updateRoom(roomId, isJoinable) {
  if (sdk) call(() => sdk.game.updateRoom({ roomId, isJoinable, inviteParams: { roomId } }));
}

export function addJoinRoomListener(callback) {
  if (sdk) call(() => sdk.game.addJoinRoomListener((p) => p?.roomId && callback(p.roomId)));
}

export function isInstantMultiplayer() { return !!sdk?.game?.isInstantMultiplayer; }

export function leftRoom() { if (sdk) call(() => sdk.game.leftRoom()); }

export function isUserAvailable() { return !!sdk?.user?.isUserAccountAvailable; }

export async function getUser() {
  if (!isUserAvailable()) return null;
  try { return await sdk.user.getUser(); } catch (e) { console.error(e); return null; }
}

export async function showAuthPrompt() {
  if (!isUserAvailable()) return null;
  try { return await sdk.user.showAuthPrompt(); } catch (e) { console.error(e); return null; }
}

export function addAuthListener(callback) {
  if (isUserAvailable()) call(() => sdk.user.addAuthListener((u) => u && callback(u)));
}

export function requestAd(adType, adStarted, adFinished, adError) {
  if (!sdk) return adError("no-sdk");
  try {
    sdk.ad.requestAd(adType, { adStarted, adFinished, adError: (e) => adError(String(e?.code ?? e)) });
  } catch (e) {
    adError(String(e));
  }
}

export function dataSetItem(key, value) {
  try {
    (sdk?.data ?? localStorage).setItem(key, value);
  } catch (e) {
    console.error(e);
    call(() => localStorage.setItem(key, value));
  }
}

export function dataGetItem(key) {
  try {
    return (sdk?.data ?? localStorage).getItem(key);
  } catch (e) {
    console.error(e);
    try { return localStorage.getItem(key); } catch (e2) { console.error(e2); return null; }
  }
}
