let sdk = null;
let isInitialized = false;

export function init(onMuteCallback) {
  return new Promise((resolve) => {
    if (isInitialized) {
      resolve();
      return;
    }

    const checkSDK = () => {
      if (window.CrazyGames && window.CrazyGames.SDK) {
        sdk = window.CrazyGames.SDK;
        isInitialized = true;
        console.log("CrazyGames SDK detected and initialized.");

        try {
          if (sdk.game && sdk.game.addSettingsChangeListener) {
            sdk.game.addSettingsChangeListener((newSettings) => {
              if (newSettings && typeof newSettings.muteAudio !== 'undefined') {
                onMuteCallback(newSettings.muteAudio);
              }
            });
            if (sdk.game.settings && typeof sdk.game.settings.muteAudio !== 'undefined') {
              onMuteCallback(sdk.game.settings.muteAudio);
            }
          }
        } catch (e) {
          console.error("Error setting up CrazyGames settings listener:", e);
        }

        resolve();
      } else {
        console.log("CrazyGames SDK not found. Running in standalone mode.");
        resolve();
      }
    };

    if (window.CrazyGames) {
      checkSDK();
    } else {
      setTimeout(checkSDK, 100);
    }
  });
}

export function gameplayStart() {
  if (sdk && sdk.game && sdk.game.gameplayStart) {
    try {
      sdk.game.gameplayStart();
    } catch (e) {
      console.error(e);
    }
  }
}

export function gameplayStop() {
  if (sdk && sdk.game && sdk.game.gameplayStop) {
    try {
      sdk.game.gameplayStop();
    } catch (e) {
      console.error(e);
    }
  }
}

export function getInviteParams() {
  if (sdk && sdk.game) {
    try {
      if (sdk.game.getInviteLinkParameters) {
        return sdk.game.getInviteLinkParameters() || {};
      }
      if (sdk.game.inviteLinkParameters) {
        return sdk.game.inviteLinkParameters || {};
      }
    } catch (e) {
      console.error(e);
    }
  }
  try {
    const urlParams = new URLSearchParams(window.location.search);
    if (urlParams.has("room")) {
      return { roomId: urlParams.get("room") };
    }
    const hashParams = new URLSearchParams(window.location.hash.slice(1));
    if (hashParams.has("room")) {
      return { roomId: hashParams.get("room") };
    }
  } catch (e) {}
  return {};
}

export function getInviteLink(roomId) {
  if (sdk && sdk.game && sdk.game.inviteLink) {
    try {
      return sdk.game.inviteLink({ roomId: roomId });
    } catch (e) {
      console.error(e);
    }
  }
  return window.location.origin + window.location.pathname + "?room=" + roomId;
}

export function updateRoom(roomId, isJoinable) {
  if (sdk && sdk.game && sdk.game.updateRoom) {
    try {
      sdk.game.updateRoom({
        roomId: roomId,
        isJoinable: isJoinable,
        inviteParams: { roomId: roomId }
      });
    } catch (e) {
      console.error(e);
    }
  }
}

export function addRoomJoinListener(callback) {
  if (sdk && sdk.game && sdk.game.addRoomJoinListener) {
    try {
      sdk.game.addRoomJoinListener((params) => {
        if (params && params.roomId) {
          callback(params.roomId);
        }
      });
    } catch (e) {
      console.error(e);
    }
  }
}

export async function isUserAvailable() {
  if (sdk && sdk.user && sdk.user.isUserAccountAvailable) {
    try {
      return await sdk.user.isUserAccountAvailable();
    } catch (e) {
      console.error(e);
      return false;
    }
  }
  return false;
}

export async function getUser() {
  if (sdk && sdk.user && sdk.user.getUser) {
    try {
      return await sdk.user.getUser();
    } catch (e) {
      console.error(e);
      return null;
    }
  }
  return null;
}

export async function showAuthPrompt() {
  if (sdk && sdk.user && sdk.user.showAuthPrompt) {
    try {
      return await sdk.user.showAuthPrompt();
    } catch (e) {
      console.error(e);
      return null;
    }
  }
  return null;
}

export function addAuthListener(callback) {
  if (sdk && sdk.user && sdk.user.addAuthListener) {
    try {
      sdk.user.addAuthListener(callback);
    } catch (e) {
      console.error(e);
    }
  }
}
