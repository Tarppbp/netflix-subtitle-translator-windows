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
    return {ok: true, json: async () => ({message: {content: "走吧。"}, done_reason: "stop"})};
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
resolveChat();
assert.equal((await first).text, "走吧。");
assert.equal((await duplicate).text, "走吧。");
assert.equal((await send(request, watch)).cached, true);
assert.equal(samples.length, 1);
assert.equal(await send({type:"models"}, {url: extension + "popup.html"}), null);
assert.equal(await send({type:"desktopSettings"}, {url: extension + "popup.html"}), null);
assert.equal((await send({type:"desktopSettings", stats:[3,2,1,125],phase:"displaying"}, watch)).desktop.chineseOnly, true);
assert.equal(samples[1].options.headers["X-Netflix-Subtitle-Stats"], "3,2,1,125");
assert.equal(samples[1].options.headers["X-Netflix-Subtitle-State"], "displaying");
console.log("PASS: sender scope, desktop-only bridge, statistics, local-only translation, cloud filtering, request deduplication, cache");

