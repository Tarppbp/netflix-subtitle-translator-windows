import { readFile } from "node:fs/promises";
import assert from "node:assert/strict";
import vm from "node:vm";

const samples = [];
const local = "http://127.0.0.1:17434";
const extension = "chrome-extension://fixture/";
let listener;
let resolveChat;
const chat = new Promise(resolve => { resolveChat = resolve; });
const code = await readFile(new URL("../extension/worker.js", import.meta.url), "utf8");
vm.runInNewContext(code, {
  AbortSignal,
  chrome: {runtime: {getURL: file => extension + file, onMessage: {addListener: fn => { listener = fn; }}}},
  async fetch(url, options) {
    samples.push({url, options});
    if (url === "http://127.0.0.1:17435/settings")
      return {ok: true, json: async () => ({enabled:true, chineseOnly:true})};
    assert.equal(url, local + "/api/chat");
    await chat;
    const prompt = JSON.parse(options.body).messages[0].content;
    const content = prompt.includes("简洁的日语") ? "行こう。" : "走吧。";
    return {ok: true, json: async () => ({message: {content}, done_reason: "stop"})};
  }
});

const watch = {url: "https://www.netflix.com/watch/123"};
const request = {type: "translate", cue: "行こう", context: [], model: "qwen3:4b-instruct", episode: "123"};
const send = (message, sender) => new Promise(resolve => {
  if (!listener(message, sender, resolve)) resolve(null);
});
assert.equal(await send(request, {url: "https://www.netflix.com.invalid/watch/123"}), null);
assert.ok((await send({...request, model: "sample:cloud"}, watch)).error);
const first = send(request, watch);
const duplicate = send(request, watch);
assert.equal(samples.length, 1);
assert.equal(samples[0].options.redirect, "error");
assert.equal(JSON.parse(samples[0].options.body).model, "qwen3:4b-instruct");
assert.match(JSON.parse(samples[0].options.body).messages[0].content, /日语对白.*简体中文/);
resolveChat();
assert.equal((await first).text, "走吧。");
assert.equal((await duplicate).text, "走吧。");
assert.equal((await send(request, watch)).cached, true);
assert.equal(samples.length, 1);
const englishToJapanese = {...request, sourceLanguage: "en", targetLanguage: "ja"};
assert.equal((await send(englishToJapanese, watch)).text, "行こう。");
assert.match(JSON.parse(samples.at(-1).options.body).messages[0].content, /英语对白.*简洁的日语/);
assert.equal((await send(englishToJapanese, watch)).cached, true);
assert.equal((await send({...englishToJapanese, sourceLanguage: "ko"}, watch)).cached, false);
assert.equal(samples.length, 3);
assert.equal((await send({...request, sourceLanguage: "fr"}, watch)).cached, false);
assert.match(JSON.parse(samples.at(-1).options.body).messages[0].content, /法语对白.*简体中文/);
assert.equal((await send({...englishToJapanese, targetLanguage: "zh-CN"}, watch)).cached, false);
assert.match(JSON.parse(samples.at(-1).options.body).messages[0].content, /英语对白.*简体中文/);
const specialized = {...request, sourceLanguage: "fr", targetLanguage: "ko", model: "translategemma:4b"};
assert.ok((await send(specialized, watch)).text);
assert.match(JSON.parse(samples.at(-1).options.body).messages[0].content, /French \(fr\) to Korean \(ko\)/);
assert.ok((await send({...request, sourceLanguage: "invalid"}, watch)).error);
assert.ok((await send({...request, targetLanguage: "invalid"}, watch)).error);
assert.equal((await send({...request, targetLanguage: "ja"}, watch)).text, request.cue);
assert.equal(samples.length, 6);
assert.equal(await send({type:"models"}, {url: extension + "popup.html"}), null);
assert.equal(await send({type:"desktopSettings"}, {url: extension + "popup.html"}), null);
assert.equal((await send({type:"desktopSettings", stats:[3,2,1,125],phase:"displaying"}, watch)).desktop.chineseOnly, true);
assert.equal(samples.at(-1).options.headers["X-Netflix-Subtitle-Stats"], "3,2,1,125");
assert.equal(samples.at(-1).options.headers["X-Netflix-Subtitle-State"], "displaying");
assert.equal(samples.at(-1).options.headers["X-Netflix-Subtitle-Languages"], "1");
console.log("PASS: local-only translation, desktop bridge, default pair, language switching, prompt and cache isolation");
