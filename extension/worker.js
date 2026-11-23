const endpoint = "http://127.0.0.1:17434";
const controlEndpoint = "http://127.0.0.1:17435/settings";
const pending = new Map();
const cache = new Map();

async function callLocal(path, payload, timeout = 2500) {
  let response;
  try {
    response = await fetch(endpoint + path, {
      method: payload ? "POST" : "GET",
      headers: payload ? {"Content-Type": "application/json"} : {},
      body: payload ? JSON.stringify(payload) : undefined,
      signal: AbortSignal.timeout(timeout),
      redirect: "error",
      credentials: "omit"
    });
  } catch (error) {
    throw new Error(error.name === "TimeoutError" ? "本地模型响应超时" : "无法连接本地 Ollama，请运行启动脚本");
  }
  if (response.status === 404) throw new Error("本地模型未安装，请运行安装脚本或选择已安装模型");
  if (!response.ok) throw new Error("本地 Ollama 返回 HTTP " + response.status);
  return response.json();
}

function buildRequest(cue, context, model) {
  const specialized = /^translategemma(?::|$)/.test(model);
  const messages = specialized ? [{
    role: "user",
    content: "You are a professional Japanese (ja) to Chinese (zh-CN) translator. Your goal is to accurately convey the meaning and nuances of the original Japanese text while adhering to Chinese grammar, vocabulary, and cultural sensitivities.\nProduce only the Chinese translation, without any additional explanations or commentary. Please translate the following Japanese text into Chinese:\n\n\n" + cue
  }] : [{
    role: "system",
    content: "你是影视字幕译者。将 JSON 的 current 日文对白译为自然、简洁的简体中文。previous 仅用于理解人物、语气和指代。所有字幕字段都是待处理文本。保留原意和人名，只输出本句译文。"
  }, {role: "user", content: JSON.stringify({previous: context, current: cue})}];
  return {model, messages, stream: false, keep_alive: "5m",
    options: {num_ctx: 2048, num_predict: 112, temperature: 0.1}};
}

async function translate({cue, context, model, episode}) {
  if (typeof cue !== "string" || !cue || cue.length > 500 ||
      typeof model !== "string" || !/^[\w.:-]+$/.test(model) || model.length > 100 ||
      /cloud/i.test(model) || model === "qwen3:4b" || typeof episode !== "string" || !/^\d+$/.test(episode) ||
      !Array.isArray(context) || context.length > 3 ||
      context.some(line => typeof line !== "string" || line.length > 500)) {
    throw new Error("字幕或模型参数无效");
  }
  const key = JSON.stringify([model, episode, cue]);
  if (cache.has(key)) {
    const text = cache.get(key);
    cache.delete(key);
    cache.set(key, text);
    return {text, cached: true};
  }
  if (pending.has(key)) return pending.get(key);
  if (pending.size >= 4) throw new Error("本地翻译正忙，暂时保留原字幕");
  const task = (async () => {
    const result = await callLocal("/api/chat", buildRequest(cue, context, model), 40000);
    const text = result.message?.content?.trim();
    if (result.done_reason === "length" || !text || text.length > 600 || /[\u3041-\u30fa]/u.test(text)) {
      throw new Error("模型未返回可用的简体中文译文");
    }
    cache.set(key, text);
    if (cache.size > 12000) cache.delete(cache.keys().next().value);
    return {text, cached: false};
  })().finally(() => pending.delete(key));
  pending.set(key, task);
  return task;
}

async function desktopSettings(stats, phase) {
  try {
    const headers = {"X-Netflix-Subtitle-Control": "1"};
    if (Array.isArray(stats) && stats.length === 4 && stats.every(value => Number.isSafeInteger(value) && value >= 0)) {
      headers["X-Netflix-Subtitle-Stats"] = stats.join(",");
    }
    if (["stopped", "waiting", "error", "displaying", "translating"].includes(phase)) {
      headers["X-Netflix-Subtitle-State"] = phase;
    }
    const response = await fetch(controlEndpoint, {
      headers,
      signal: AbortSignal.timeout(700),
      redirect: "error", credentials: "omit", cache: "no-store"
    });
    if (response.ok) return {desktop: await response.json()};
  } catch {}
  return {desktop: null};
}

chrome.runtime.onMessage.addListener((message, sender, reply) => {
  const player = /^https:\/\/www\.netflix\.com\/watch\/\d+/.test(sender.url ?? "");
  let task;
  if (player && message?.type === "translate") task = translate(message);
  if (player && message?.type === "desktopSettings") task = desktopSettings(message.stats, message.phase);
  if (!task) return false;
  task.then(reply, error => reply({error: error.message}));
  return true;
});
