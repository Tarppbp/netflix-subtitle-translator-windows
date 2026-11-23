(() => {
  const selector = ".player-timedtext";
  const lineSelector = ".player-timedtext-text-container";
  const state = {read: 0, shown: 0, cacheHits: 0, failed: false, samples: []};
  let config = {...subtitleSettings}, route = "", source, video, seen = "", current = "", zh = "";
  let checkingDesktop = false;
  let token = 0, debounce, lastStarted = -1, context = [], active = false, seek = false;
  let arrival = 0;
  const host = document.createElement("div");
  host.setAttribute("aria-live", "off");
  const root = host.attachShadow({mode: "closed"});
  const caption = document.createElement("div");
  root.append(caption);
  const observer = new MutationObserver(schedule);
  let mediaEvents = new AbortController();

  function visibleLines() {
    return source ? [...source.querySelectorAll(lineSelector)].filter(node => node.getClientRects().length) : [];
  }

  function originalPosition() {
    if (!config.followSubtitle || !source) return null;
    let left = Infinity, right = -Infinity, top = Infinity, bottom = -Infinity;
    const lines = visibleLines();
    for (const line of lines.length ? lines : [source]) {
      const range = document.createRange();
      range.selectNodeContents(line);
      const bounds = range.getBoundingClientRect();
      if (!bounds.width || !bounds.height) continue;
      left = Math.min(left, bounds.left);
      right = Math.max(right, bounds.right);
      top = Math.min(top, bounds.top);
      bottom = Math.max(bottom, bounds.bottom);
    }
    if (!Number.isFinite(bottom)) return null;
    return {center: (left + right) / 2, bottom: innerHeight - (config.chineseOnly ? bottom : top - 8)};
  }

  function render() {
    const full = document.fullscreenElement;
    const parent = full instanceof HTMLVideoElement ? null : (full ?? document.body);
    const canShow = Boolean(config.enabled && route && video && parent);
    const visible = seen === current ? zh : "";
    document.documentElement.toggleAttribute("data-local-zh-hidden", canShow && config.chineseOnly);
    if (!canShow || !visible) {
      host.remove();
      return;
    }
    if (host.parentElement !== parent) parent.append(host);
    const bounds = video?.getBoundingClientRect();
    const anchor = originalPosition();
    const bottom = bounds && bounds.height > 100 ? innerHeight - bounds.bottom + bounds.height * config.bottom / 100
      : innerHeight * config.bottom / 100;
    Object.assign(host.style, {
      all: "initial", position: "fixed", left: anchor ? anchor.center + "px" : bounds ? bounds.left + bounds.width / 2 + "px" : "50%",
      bottom: Math.max(0, anchor?.bottom ?? bottom) + "px", transform: "translateX(-50%)", zIndex: "2147483646",
      display: "flex", justifyContent: "center", width: "82vw", pointerEvents: "none"
    });
    Object.assign(caption.style, {
      fontFamily: config.font + ", Noto Sans SC, Microsoft YaHei UI, sans-serif", fontWeight: config.font === "Noto Sans SC" ? "500" : "600",
      whiteSpace: "pre-wrap", overflowWrap: "anywhere", textAlign: "center",
      color: config.color, fontSize: "min(" + config.size + "px, 6vw)",
      lineHeight: "1.22", paintOrder: "stroke fill",
      webkitTextStroke: (config.stroke * 2) + "px #000",
      background: "rgba(0,0,0," + config.background / 100 + ")", borderRadius: "6px",
      padding: config.background ? "4px 10px" : "0"
    });
    caption.textContent = visible;
  }

  function read() {
    if (!source?.isConnected || !source.getClientRects().length || !config.enabled || seek) return "";
    const lines = visibleLines();
    const text = (lines.length ? lines.map(node => node.innerText.trim()).filter(Boolean).join("\n")
      : source.innerText).trim();
    return text.length <= 500 ? text : "";
  }

  function clear() {
    seen = current = zh = "";
    state.failed = false;
    token++;
    clearTimeout(debounce);
    context = [];
    render();
  }

  function schedule() {
    const value = read();
    if (seen === value) {
      if (zh) render();
      return;
    }
    seen = value;
    render();
    clearTimeout(debounce);
    debounce = setTimeout(() => {
      if (read() !== value || value === current) return;
      if (current) context = [...context, current].slice(-3);
      current = value;
      zh = "";
      state.failed = false;
      token++;
      if (value) {
        state.read++;
        arrival = performance.now();
      }
      render();
      void translate();
    }, value ? 65 : 95);
  }

  async function translate() {
    if (active || !current || !config.enabled || lastStarted === token) return;
    active = true;
    const revision = token;
    lastStarted = revision;
    try {
      const result = await chrome.runtime.sendMessage({
        type: "translate", cue: current, context: config.context ? context : [],
        model: config.model, episode: route,
        sourceLanguage: config.sourceLanguage, targetLanguage: config.targetLanguage
      });
      if (revision !== token || read() !== current) return;
      if (!result?.text) throw new Error(result?.error || "本地翻译未返回结果");
      zh = result.text;
      state.shown++;
      if (result.cached) state.cacheHits++;
      state.samples.push(Math.round(performance.now() - arrival));
      if (state.samples.length > 100) state.samples.shift();
      render();
    } catch {
      if (revision === token) state.failed = true;
    } finally {
      active = false;
      if (revision !== token) void translate();
    }
  }

  function sync() {
    const next = /^\/watch\/(\d+)(?:\/|$)/.exec(location.pathname)?.[1] ?? "";
    if (next !== route) {
      route = next;
      clear();
      state.read = state.shown = state.cacheHits = 0;
      state.samples = [];
    }
    const player = route && config.enabled ? document.querySelector("video") : null;
    if (player !== video) {
      mediaEvents.abort();
      mediaEvents = new AbortController();
      video = player;
      seek = false;
      clear();
      const signal = mediaEvents.signal;
      video?.addEventListener("seeking", () => { seek = true; clear(); }, {signal});
      video?.addEventListener("seeked", () => { seek = false; schedule(); }, {signal});
      video?.addEventListener("resize", render, {signal});
      render();
    }
    const layer = video ? document.querySelector(selector) : null;
    if (layer !== source) {
      observer.disconnect();
      source = layer;
      if (source) observer.observe(source, {
        subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: ["style", "class"]
      });
      schedule();
      render();
      return;
    }
    schedule();
  }

  addEventListener("resize", render);
  document.addEventListener("fullscreenchange", render);
  setInterval(sync, 250);

  function applySettings(desktop) {
    const next = {...subtitleSettings, ...desktop};
    const changedKeys = Object.keys(subtitleSettings).filter(key => config[key] !== next[key]);
    if (!changedKeys.length) return;
    config = next;
    const languageChanged = changedKeys.includes("sourceLanguage") || changedKeys.includes("targetLanguage");
    if (languageChanged) {
      state.read = state.shown = state.cacheHits = 0;
      state.samples = [];
    }
    if (languageChanged || ["enabled", "model", "context"].some(key => changedKeys.includes(key))) clear();
    sync();
    render();
  }

  async function checkDesktop() {
    if (!route || checkingDesktop) return;
    checkingDesktop = true;
    try {
      const average = state.samples.length
        ? Math.round(state.samples.reduce((sum, value) => sum + value, 0) / state.samples.length) : 0;
      const phase = !config.enabled ? "stopped" : !seen ? "waiting"
        : state.failed ? "error" : seen === current && zh ? "displaying" : "translating";
      const result = await chrome.runtime.sendMessage({
        type: "desktopSettings", stats: [state.read, state.shown, state.cacheHits, average], phase
      });
      applySettings(result?.desktop);
    } catch {
      applySettings();
    } finally {
      checkingDesktop = false;
    }
  }

  setInterval(checkDesktop, 350);
  sync();
  void checkDesktop();
})();
