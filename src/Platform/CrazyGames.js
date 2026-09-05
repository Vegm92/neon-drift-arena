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

// Monetization module (Video Ads)
export function requestAd(adType, adStartedCallback, adFinishedCallback, adErrorCallback) {
  if (sdk && sdk.ad && sdk.ad.requestAd) {
    sdk.ad.requestAd(adType, {
      adStarted: () => {
        console.log("CrazyGames ad started:", adType);
        adStartedCallback();
      },
      adFinished: () => {
        console.log("CrazyGames ad finished:", adType);
        adFinishedCallback();
      },
      adError: (error, errorData) => {
        console.warn("CrazyGames ad error:", error, errorData);
        adErrorCallback(JSON.stringify(errorData || {}));
      }
    });
  } else {
    // Local development mock for video ads
    console.log(`[Local Mock] Starting video ad simulation (${adType})...`);
    adStartedCallback();
    setTimeout(() => {
      console.log(`[Local Mock] Finished video ad simulation (${adType})`);
      adFinishedCallback();
    }, 1000);
  }
}

// Monetization module (Banner Ads)
export function requestBanner(containerId, width, height) {
  if (sdk && sdk.banner && sdk.banner.requestBanner) {
    try {
      const el = document.getElementById(containerId);
      if (el) {
        el.style.display = "block";
        sdk.banner.requestBanner({
          id: containerId,
          width: width,
          height: height
        });
      }
    } catch (e) {
      console.error("Error requesting CrazyGames banner:", e);
    }
  } else {
    // Local development mock for banners
    const el = document.getElementById(containerId);
    if (el) {
      el.style.display = "flex";
      el.style.alignItems = "center";
      el.style.justifyContent = "center";
      el.style.background = "rgba(10, 18, 38, 0.85)";
      el.style.border = "1.5px solid #00f6ff";
      el.style.boxShadow = "0 0 12px rgba(0, 246, 255, 0.45)";
      el.style.color = "#00f6ff";
      el.style.fontFamily = "monospace";
      el.style.fontSize = "11px";
      el.style.letterSpacing = "0.1em";
      el.innerHTML = `MOCK BANNER ${width}x${height}`;
    }
  }
}

export function clearAllBanners() {
  if (sdk && sdk.banner && sdk.banner.clearAllBanners) {
    try {
      sdk.banner.clearAllBanners();
    } catch (e) {
      console.error("Error clearing all CrazyGames banners:", e);
    }
  }
  // Hide visual wrappers
  ["cg-banner-1", "cg-banner-2"].forEach(id => {
    const el = document.getElementById(id);
    if (el) {
      el.style.display = "none";
      el.innerHTML = "";
    }
  });
}

// Data module (Cloud-synced progress)
export function dataSetItem(key, value) {
  if (sdk && sdk.data && sdk.data.setItem) {
    try {
      sdk.data.setItem(key, value);
    } catch (e) {
      console.error("Error saving data via SDK:", e);
      localStorage.setItem(key, value);
    }
  } else {
    localStorage.setItem(key, value);
  }
}

export function dataGetItem(key) {
  if (sdk && sdk.data && sdk.data.getItem) {
    try {
      return sdk.data.getItem(key);
    } catch (e) {
      console.error("Error reading data via SDK:", e);
      return localStorage.getItem(key);
    }
  } else {
    return localStorage.getItem(key);
  }
}

// Sync guest progress on login (Copy localStorage -> SDK.data cloud)
export function syncGuestProgressOnLogin() {
  try {
    const localWins = localStorage.getItem("nda-series-wins") || "0";
    const localMatches = localStorage.getItem("nda-matches-played") || "0";
    const localHighScore = localStorage.getItem("nda-high-score") || "0";

    const cloudWins = (sdk && sdk.data && sdk.data.getItem) ? (sdk.data.getItem("nda-series-wins") || "0") : "0";
    const cloudMatches = (sdk && sdk.data && sdk.data.getItem) ? (sdk.data.getItem("nda-matches-played") || "0") : "0";
    const cloudHighScore = (sdk && sdk.data && sdk.data.getItem) ? (sdk.data.getItem("nda-high-score") || "0") : "0";

    const mergedWins = Math.max(parseInt(localWins), parseInt(cloudWins));
    const mergedMatches = Math.max(parseInt(localMatches), parseInt(cloudMatches));
    const mergedHighScore = Math.max(parseInt(localHighScore), parseInt(cloudHighScore));

    if (sdk && sdk.data && sdk.data.setItem) {
      sdk.data.setItem("nda-series-wins", mergedWins.toString());
      sdk.data.setItem("nda-matches-played", mergedMatches.toString());
      sdk.data.setItem("nda-high-score", mergedHighScore.toString());
      console.log(`Cloud data updated after login. Wins: ${mergedWins}, Matches: ${mergedMatches}, High Kills: ${mergedHighScore}`);
    } else {
      localStorage.setItem("nda-series-wins", mergedWins.toString());
      localStorage.setItem("nda-matches-played", mergedMatches.toString());
      localStorage.setItem("nda-high-score", mergedHighScore.toString());
    }
  } catch (e) {
    console.error("Failed to sync guest progress on login:", e);
  }
}

export function addAuthListener(callback) {
  if (sdk && sdk.user && sdk.user.addAuthListener) {
    try {
      sdk.user.addAuthListener((user) => {
        if (user) {
          syncGuestProgressOnLogin();
        }
        callback(user);
      });
      return;
    } catch (e) {
      console.error(e);
    }
  }
  // Standalone auth listener simulation
  window.addEventListener("nda-mock-auth", (e) => {
    if (e.detail && e.detail.user) {
      syncGuestProgressOnLogin();
      callback(e.detail.user);
    }
  });
}

// Leaderboard module (Score Submission with Client-side Encryption helper)
export async function submitLeaderboardScore(score) {
  const key = (typeof import.meta !== 'undefined' && import.meta.env && import.meta.env.VITE_CG_LEADERBOARD_KEY) 
              || "NDA_DEFAULT_LEADERBOARD_KEY_PLACEHOLDER";
              
  if (sdk && sdk.leaderboard) {
    try {
      // Cleanly exclude guests from submission (complying with account-keyed integrity)
      const isAvailable = sdk.user && sdk.user.isUserAccountAvailable && await sdk.user.isUserAccountAvailable();
      if (!isAvailable) {
        console.log("Excluding Guest player from submitting to platform leaderboard.");
        return;
      }

      if (sdk.leaderboard.encryptScore && sdk.leaderboard.submitScore) {
        console.log(`Encrypting and submitting score: ${score}`);
        const encrypted = await sdk.leaderboard.encryptScore(score, key);
        await sdk.leaderboard.submitScore(encrypted);
        console.log("Encrypted score submitted successfully!");
      } else if (sdk.leaderboard.submitScore) {
        console.log(`Submitting raw score: ${score}`);
        await sdk.leaderboard.submitScore(score);
      }
    } catch (e) {
      console.error("Error submitting score to CrazyGames leaderboard:", e);
    }
  } else {
    console.log(`[Local Mock] Leaderboard score submission simulated. Score: ${score}`);
  }
}

// WebRTC Socket Wrapper for low-latency transport
export function createRelaySocket(url, isPad) {
  const ws = new WebSocket(url);
  const socketWrapper = {
    readyState: 0,
    onmessage: null,
    onclose: null,
    send: (data) => {
      if (ws.readyState === WebSocket.OPEN) {
        ws.send(data);
      }
    }
  };

  const peerConnections = new Map(); // padId -> RTCPeerConnection
  const dataChannels = new Map(); // padId -> RTCDataChannel
  let localPadId = null;
  let useWebRTC = false;

  const configuration = {
    iceServers: [
      { urls: "stun:stun.l.google.com:19302" },
      { urls: "stun:stun1.l.google.com:19302" }
    ]
  };

  ws.onopen = () => {
    socketWrapper.readyState = ws.readyState;
    
    if (isPad) {
      localPadId = window.sessionStorage.getItem("nda-pad-id") || Math.random().toString(36).slice(2, 10);
      window.sessionStorage.setItem("nda-pad-id", localPadId);
      
      setTimeout(() => {
        if (ws.readyState === WebSocket.OPEN) {
          ws.send(JSON.stringify({
            event: "webrtc:join",
            data: { from: localPadId }
          }));
        }
      }, 500);
    }
  };

  ws.onclose = (event) => {
    socketWrapper.readyState = ws.readyState;
    if (socketWrapper.onclose) socketWrapper.onclose(event);
    
    peerConnections.forEach(pc => pc.close());
    peerConnections.clear();
    dataChannels.clear();
  };

  ws.onmessage = async (event) => {
    try {
      const msg = JSON.parse(event.data);
      
      if (msg.event === "webrtc:join" && !isPad) {
        const padId = msg.data.from;
        setupHostPeerConnection(padId);
        return;
      }
      
      if (msg.event === "webrtc:offer" && isPad) {
        const { to, from, offer } = msg.data;
        if (to === localPadId) {
          setupPadPeerConnection(from, offer);
        }
        return;
      }
      
      if (msg.event === "webrtc:answer" && !isPad) {
        const { from, answer } = msg.data;
        const pc = peerConnections.get(from);
        if (pc) {
          await pc.setRemoteDescription(new RTCSessionDescription(answer));
        }
        return;
      }
      
      if (msg.event === "webrtc:candidate") {
        const { to, from, candidate } = msg.data;
        if (isPad && to === localPadId) {
          const pc = peerConnections.get(from);
          if (pc && candidate) {
            await pc.addIceCandidate(new RTCIceCandidate(candidate));
          }
        } else if (!isPad && to === "host") {
          const pc = peerConnections.get(from);
          if (pc && candidate) {
            await pc.addIceCandidate(new RTCIceCandidate(candidate));
          }
        }
        return;
      }
      
      if (isPad && useWebRTC && msg.event === "nda:state") {
        return;
      }
      
      if (socketWrapper.onmessage) {
        socketWrapper.onmessage(event);
      }
    } catch (e) {
      if (socketWrapper.onmessage) {
        socketWrapper.onmessage(event);
      }
    }
  };

  function setupHostPeerConnection(padId) {
    if (peerConnections.has(padId)) {
      peerConnections.get(padId).close();
    }
    
    const pc = new RTCPeerConnection(configuration);
    peerConnections.set(padId, pc);
    
    const dc = pc.createDataChannel("nda");
    dataChannels.set(padId, dc);
    
    dc.onopen = () => {
      console.log(`WebRTC DataChannel opened to pad: ${padId}`);
    };
    
    dc.onclose = () => {
      console.log(`WebRTC DataChannel closed to pad: ${padId}`);
      dataChannels.delete(padId);
    };
    
    dc.onmessage = (e) => {
      if (socketWrapper.onmessage) {
        socketWrapper.onmessage(e);
      }
    };
    
    pc.onicecandidate = (event) => {
      if (event.candidate && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({
          event: "webrtc:candidate",
          data: { to: padId, from: "host", candidate: event.candidate }
        }));
      }
    };
    
    pc.createOffer().then(async (offer) => {
      await pc.setLocalDescription(offer);
      if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({
          event: "webrtc:offer",
          data: { to: padId, from: "host", offer: offer }
        }));
      }
    }).catch(err => console.error("WebRTC offer error:", err));
  }

  function setupPadPeerConnection(hostId, offer) {
    const pc = new RTCPeerConnection(configuration);
    peerConnections.set(hostId, pc);
    
    pc.ondatachannel = (event) => {
      const dc = event.channel;
      dataChannels.set(hostId, dc);
      
      dc.onopen = () => {
        console.log("WebRTC DataChannel opened to host!");
        useWebRTC = true;
      };
      
      dc.onclose = () => {
        console.log("WebRTC DataChannel closed to host.");
        useWebRTC = false;
        dataChannels.delete(hostId);
      };
      
      dc.onmessage = (e) => {
        if (socketWrapper.onmessage) {
          socketWrapper.onmessage(e);
        }
      };
    };
    
    pc.onicecandidate = (event) => {
      if (event.candidate && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({
          event: "webrtc:candidate",
          data: { to: "host", from: localPadId, candidate: event.candidate }
        }));
      }
    };
    
    pc.setRemoteDescription(new RTCSessionDescription(offer))
      .then(() => pc.createAnswer())
      .then(async (answer) => {
        await pc.setLocalDescription(answer);
        if (ws.readyState === WebSocket.OPEN) {
          ws.send(JSON.stringify({
            event: "webrtc:answer",
            data: { to: "host", from: localPadId, answer: answer }
          }));
        }
      })
      .catch(err => console.error("WebRTC answer error:", err));
  }

  socketWrapper.send = (data) => {
    if (isPad) {
      const dc = dataChannels.get("host");
      if (useWebRTC && dc && dc.readyState === "open") {
        try {
          dc.send(data);
          return;
        } catch (e) {
          console.error("Failed to send over WebRTC, falling back to WS", e);
        }
      }
      if (ws.readyState === WebSocket.OPEN) {
        ws.send(data);
      }
    } else {
      let sentWebRTC = false;
      try {
        const parsed = JSON.parse(data);
        if (parsed.event === "nda:state" || parsed.event === "nda:host") {
          dataChannels.forEach((dc, padId) => {
            if (dc.readyState === "open") {
              try {
                dc.send(data);
                sentWebRTC = true;
              } catch (e) {
                console.error(`Failed to send to pad ${padId} over WebRTC`, e);
              }
            }
          });
        }
      } catch (e) {}

      if (ws.readyState === WebSocket.OPEN) {
        ws.send(data);
      }
    }
  };

  return socketWrapper;
}
