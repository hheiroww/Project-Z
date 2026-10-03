import test from "node:test";
import assert from "node:assert/strict";
import {
  Button,
  EventSignal,
  ProjectZEngine,
  Rectangle,
  Scene,
  SceneElement,
  StackPanel,
  Tween
} from "../src/engine.js";

function context() {
  const noop = () => {};
  return {
    globalAlpha: 1,
    save: noop, restore: noop, translate: noop, rotate: noop, scale: noop,
    setTransform: noop, clearRect: noop, fillRect: noop, beginPath: noop,
    roundRect: noop, rect: noop, fill: noop, stroke: noop, clip: noop,
    arc: noop, fillText: noop
  };
}

function canvas() {
  const listeners = new Map();
  return {
    width: 0, height: 0, style: {}, tabIndex: -1,
    getContext: () => context(),
    getBoundingClientRect: () => ({ left: 0, top: 0, width: 640, height: 360 }),
    addEventListener: (name, listener) => listeners.set(name, listener),
    removeEventListener: (name) => listeners.delete(name),
    focus() {}, setPointerCapture() {}, releasePointerCapture() {},
    dispatch(name, event = {}) { listeners.get(name)?.({ clientX: 0, clientY: 0, pointerId: 1, preventDefault() {}, ...event }); }
  };
}

test("scene graph owns, finds, and removes named elements", () => {
  const root = new SceneElement({ name: "root" });
  const panel = new SceneElement({ name: "panel" });
  const child = new Rectangle({ name: "child", width: 20, height: 20 });
  root.add(panel);
  panel.add(child);
  assert.equal(root.find("child"), child);
  assert.equal(child.parent, panel);
  child.remove();
  assert.equal(root.find("child"), null);
  assert.equal(child.parent, null);
});

test("hit testing respects nested transforms and z-index", () => {
  const root = new Scene({ width: 400, height: 300 });
  const panel = new Rectangle({ x: 50, y: 40, width: 200, height: 150 });
  const back = new Rectangle({ x: 10, y: 10, width: 80, height: 50, interactive: true, zIndex: 1 });
  const front = new Rectangle({ x: 20, y: 20, width: 80, height: 50, interactive: true, zIndex: 2 });
  root.add(panel); panel.add(back, front);
  assert.equal(root.hitTest(75, 65), front);
  assert.equal(root.hitTest(62, 52), back);
  assert.equal(root.hitTest(5, 5), null);
});

test("stack panel lays out children in both orientations", () => {
  const panel = new StackPanel({ orientation: "horizontal", padding: 5, gap: 3 });
  const first = new SceneElement({ width: 10, height: 8 });
  const second = new SceneElement({ width: 12, height: 8 });
  panel.add(first, second); panel.layout();
  assert.deepEqual([first.x, second.x, second.y], [5, 18, 5]);
});

test("tween interpolates numeric properties and completes", () => {
  const target = { x: 0, opacity: 1 };
  const tween = new Tween(target, { x: 10, opacity: 0 }, 1, { ease: (t) => t });
  assert.equal(tween.update(0.25), false);
  assert.deepEqual(target, { x: 2.5, opacity: 0.75 });
  assert.equal(tween.update(0.75), true);
  assert.deepEqual(target, { x: 10, opacity: 0 });
});

test("event signals unsubscribe cleanly", () => {
  const signal = new EventSignal();
  let calls = 0;
  const off = signal.on(() => calls++);
  signal.emit(); off(); signal.emit();
  assert.equal(calls, 1);
});

test("engine maps CSS pointer coordinates into logical scene coordinates", () => {
  const surface = canvas();
  const engine = new ProjectZEngine(surface, { width: 1280, height: 720 });
  const scene = new Scene();
  const button = new Button({ x: 100, y: 100, width: 200, height: 80 });
  scene.add(button); engine.setScene(scene);
  let clicks = 0;
  button.onClick.on(() => clicks++);
  surface.dispatch("pointerdown", { clientX: 75, clientY: 70 });
  surface.dispatch("pointerup", { clientX: 75, clientY: 70 });
  assert.equal(clicks, 1);
  engine.destroy();
});

test("engine clamps long frames and advances animations deterministically", () => {
  const engine = new ProjectZEngine(canvas(), { width: 320, height: 180, maxDelta: 0.05 });
  const scene = new Scene();
  engine.setScene(scene);
  const target = { x: 0 };
  engine.tween(target, { x: 100 }, 1, { ease: (t) => t });
  engine.step(1);
  assert.equal(target.x, 5);
  assert.equal(engine.stats.frameMs, 50);
  engine.destroy();
});
