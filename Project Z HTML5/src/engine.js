const TAU = Math.PI * 2;

export const Easing = Object.freeze({
  linear: (t) => t,
  easeOutCubic: (t) => 1 - Math.pow(1 - t, 3),
  easeInOutCubic: (t) => t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2
});

export class EventSignal {
  #listeners = new Set();

  on(listener) {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  }

  emit(payload) {
    for (const listener of [...this.#listeners]) listener(payload);
  }

  clear() {
    this.#listeners.clear();
  }
}

export class SceneElement {
  constructor(options = {}) {
    this.name = options.name || "";
    this.x = options.x || 0;
    this.y = options.y || 0;
    this.width = options.width || 0;
    this.height = options.height || 0;
    this.scaleX = options.scaleX ?? 1;
    this.scaleY = options.scaleY ?? 1;
    this.rotation = options.rotation || 0;
    this.opacity = options.opacity ?? 1;
    this.visible = options.visible ?? true;
    this.enabled = options.enabled ?? true;
    this.interactive = options.interactive ?? false;
    this.cursor = options.cursor || "default";
    this.zIndex = options.zIndex || 0;
    this.parent = null;
    this.children = [];
    this.hovered = false;
    this.pressed = false;
    this.onPointerEnter = new EventSignal();
    this.onPointerLeave = new EventSignal();
    this.onPointerDown = new EventSignal();
    this.onPointerUp = new EventSignal();
    this.onClick = new EventSignal();
    this.onKeyDown = new EventSignal();
  }

  add(...elements) {
    for (const element of elements) {
      if (!(element instanceof SceneElement)) throw new TypeError("Children must be SceneElement instances.");
      element.remove();
      element.parent = this;
      this.children.push(element);
    }
    return elements.at(-1);
  }

  remove(element) {
    if (element) {
      const index = this.children.indexOf(element);
      if (index >= 0) {
        this.children.splice(index, 1);
        element.parent = null;
      }
      return element;
    }
    if (this.parent) this.parent.remove(this);
    return this;
  }

  find(name) {
    if (this.name === name) return this;
    for (const child of this.children) {
      const match = child.find(name);
      if (match) return match;
    }
    return null;
  }

  update(delta, elapsed) {
    for (const child of this.children) child.update(delta, elapsed);
  }

  layout() {
    for (const child of this.children) child.layout();
  }

  contains(localX, localY) {
    return localX >= 0 && localY >= 0 && localX <= this.width && localY <= this.height;
  }

  globalToLocal(x, y) {
    const chain = [];
    for (let current = this; current; current = current.parent) chain.unshift(current);
    let point = { x, y };
    for (const current of chain) point = current.parentToLocal(point.x, point.y);
    return point;
  }

  parentToLocal(x, y) {
    const translatedX = x - this.x;
    const translatedY = y - this.y;
    const cosine = Math.cos(-this.rotation);
    const sine = Math.sin(-this.rotation);
    return {
      x: (translatedX * cosine - translatedY * sine) / this.scaleX,
      y: (translatedX * sine + translatedY * cosine) / this.scaleY
    };
  }

  hitTest(x, y) {
    if (!this.visible || !this.enabled || this.opacity <= 0) return null;
    const local = this.parentToLocal(x, y);
    const ordered = [...this.children].sort((a, b) => b.zIndex - a.zIndex);
    for (const child of ordered) {
      const hit = child.hitTest(local.x, local.y);
      if (hit) return hit;
    }
    return this.interactive && this.contains(local.x, local.y) ? this : null;
  }

  render(context) {
    if (!this.visible || this.opacity <= 0) return;
    context.save();
    context.translate(this.x, this.y);
    if (this.rotation) context.rotate(this.rotation);
    if (this.scaleX !== 1 || this.scaleY !== 1) context.scale(this.scaleX, this.scaleY);
    context.globalAlpha *= this.opacity;
    this.draw(context);
    const ordered = [...this.children].sort((a, b) => a.zIndex - b.zIndex);
    for (const child of ordered) child.render(context);
    context.restore();
  }

  draw(_context) {}
}

export class Scene extends SceneElement {
  constructor(options = {}) {
    super(options);
    this.background = options.background || "#07110f";
    this.interactive = false;
  }

  draw(context) {
    context.fillStyle = this.background;
    context.fillRect(0, 0, this.width, this.height);
  }
}

export class Rectangle extends SceneElement {
  constructor(options = {}) {
    super(options);
    this.fill = options.fill ?? "transparent";
    this.stroke = options.stroke ?? null;
    this.strokeWidth = options.strokeWidth ?? 1;
    this.radius = options.radius ?? 0;
    this.shadowColor = options.shadowColor ?? "transparent";
    this.shadowBlur = options.shadowBlur ?? 0;
    this.shadowOffsetX = options.shadowOffsetX ?? 0;
    this.shadowOffsetY = options.shadowOffsetY ?? 0;
    this.clip = options.clip ?? false;
  }

  path(context) {
    const radius = Math.max(0, Math.min(this.radius, this.width / 2, this.height / 2));
    context.beginPath();
    if (typeof context.roundRect === "function") context.roundRect(0, 0, this.width, this.height, radius);
    else context.rect(0, 0, this.width, this.height);
  }

  draw(context) {
    this.path(context);
    context.shadowColor = this.shadowColor;
    context.shadowBlur = this.shadowBlur;
    context.shadowOffsetX = this.shadowOffsetX;
    context.shadowOffsetY = this.shadowOffsetY;
    if (this.fill !== "transparent") {
      context.fillStyle = this.fill;
      context.fill();
    }
    context.shadowColor = "transparent";
    if (this.stroke) {
      context.strokeStyle = this.stroke;
      context.lineWidth = this.strokeWidth;
      context.stroke();
    }
    if (this.clip) context.clip();
  }
}

export class TextElement extends SceneElement {
  constructor(options = {}) {
    super(options);
    this.text = options.text ?? "";
    this.color = options.color ?? "#ffffff";
    this.fontSize = options.fontSize ?? 16;
    this.fontFamily = options.fontFamily ?? "Inter, Segoe UI, sans-serif";
    this.fontWeight = options.fontWeight ?? 400;
    this.align = options.align ?? "left";
    this.baseline = options.baseline ?? "top";
    this.lineHeight = options.lineHeight ?? this.fontSize * 1.35;
  }

  draw(context) {
    context.fillStyle = this.color;
    context.font = `${this.fontWeight} ${this.fontSize}px ${this.fontFamily}`;
    context.textAlign = this.align;
    context.textBaseline = this.baseline;
    const lines = String(this.text).split("\n");
    for (let index = 0; index < lines.length; index++) {
      context.fillText(lines[index], 0, index * this.lineHeight);
    }
  }
}

export class Circle extends SceneElement {
  constructor(options = {}) {
    const radius = options.radius ?? 10;
    super({ ...options, width: radius * 2, height: radius * 2 });
    this.radius = radius;
    this.fill = options.fill ?? "#35e7c4";
    this.stroke = options.stroke ?? null;
    this.strokeWidth = options.strokeWidth ?? 1;
    this.glow = options.glow ?? 0;
  }

  contains(localX, localY) {
    const dx = localX - this.radius;
    const dy = localY - this.radius;
    return dx * dx + dy * dy <= this.radius * this.radius;
  }

  draw(context) {
    context.beginPath();
    context.arc(this.radius, this.radius, this.radius, 0, TAU);
    context.fillStyle = this.fill;
    context.shadowColor = this.fill;
    context.shadowBlur = this.glow;
    context.fill();
    context.shadowColor = "transparent";
    if (this.stroke) {
      context.strokeStyle = this.stroke;
      context.lineWidth = this.strokeWidth;
      context.stroke();
    }
  }
}

export class Button extends Rectangle {
  constructor(options = {}) {
    super({ ...options, interactive: true, cursor: "pointer" });
    this.normalFill = options.fill ?? "#123129";
    this.hoverFill = options.hoverFill ?? "#19473a";
    this.pressedFill = options.pressedFill ?? "#35e7c4";
    this.label = new TextElement({
      text: options.text ?? "Button",
      color: options.textColor ?? "#eafff9",
      fontSize: options.fontSize ?? 14,
      fontWeight: 700,
      align: "center",
      baseline: "middle",
      x: this.width / 2,
      y: this.height / 2
    });
    this.add(this.label);
  }

  draw(context) {
    this.fill = this.pressed ? this.pressedFill : this.hovered ? this.hoverFill : this.normalFill;
    this.label.color = this.pressed ? "#051713" : "#eafff9";
    super.draw(context);
  }
}

export class StackPanel extends SceneElement {
  constructor(options = {}) {
    super(options);
    this.orientation = options.orientation ?? "vertical";
    this.gap = options.gap ?? 0;
    this.padding = options.padding ?? 0;
  }

  layout() {
    let cursor = this.padding;
    for (const child of this.children) {
      if (this.orientation === "vertical") {
        child.x = this.padding;
        child.y = cursor;
        cursor += child.height + this.gap;
      } else {
        child.x = cursor;
        child.y = this.padding;
        cursor += child.width + this.gap;
      }
      child.layout();
    }
  }
}

export class Tween {
  constructor(target, to, duration = 0.3, options = {}) {
    this.target = target;
    this.to = { ...to };
    this.from = Object.fromEntries(Object.keys(to).map((key) => [key, Number(target[key])]));
    this.duration = Math.max(0.0001, duration);
    this.ease = options.ease ?? Easing.easeOutCubic;
    this.elapsed = 0;
    this.done = false;
    this.onComplete = options.onComplete;
  }

  update(delta) {
    if (this.done) return true;
    this.elapsed += delta;
    const progress = Math.min(1, this.elapsed / this.duration);
    const amount = this.ease(progress);
    for (const [key, destination] of Object.entries(this.to)) {
      this.target[key] = this.from[key] + (destination - this.from[key]) * amount;
    }
    if (progress === 1) {
      this.done = true;
      this.onComplete?.();
    }
    return this.done;
  }
}

export class AssetStore {
  #images = new Map();

  async image(key, url) {
    if (this.#images.has(key)) return this.#images.get(key);
    if (typeof Image === "undefined") throw new Error("Image loading requires a browser.");
    const promise = new Promise((resolve, reject) => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = () => reject(new Error(`Unable to load image: ${url}`));
      image.src = url;
    });
    this.#images.set(key, promise);
    return promise;
  }

  get(key) {
    return this.#images.get(key);
  }
}

export class ProjectZEngine {
  constructor(canvas, options = {}) {
    if (!canvas?.getContext) throw new TypeError("ProjectZEngine requires an HTMLCanvasElement.");
    this.canvas = canvas;
    this.context = canvas.getContext("2d", { alpha: false });
    this.width = options.width ?? 1280;
    this.height = options.height ?? 720;
    this.maxDelta = options.maxDelta ?? 1 / 20;
    this.scene = null;
    this.running = false;
    this.elapsed = 0;
    this.lastTime = 0;
    this.hoverTarget = null;
    this.pressTarget = null;
    this.focusTarget = null;
    this.tweens = [];
    this.assets = new AssetStore();
    this.stats = { fps: 0, frameMs: 0, frames: 0 };
    this.#fpsElapsed = 0;
    this.#fpsFrames = 0;
    this.#bindInput();
    this.resize();
  }

  #fpsElapsed;
  #fpsFrames;
  #listeners = [];

  setScene(scene) {
    if (!(scene instanceof Scene)) throw new TypeError("setScene expects a Scene.");
    this.scene = scene;
    scene.width = this.width;
    scene.height = this.height;
    scene.layout();
    return scene;
  }

  tween(target, to, duration, options) {
    const tween = new Tween(target, to, duration, options);
    this.tweens.push(tween);
    return tween;
  }

  resize(pixelRatio = globalThis.devicePixelRatio || 1) {
    const ratio = Math.max(1, Math.min(3, pixelRatio));
    this.canvas.width = Math.round(this.width * ratio);
    this.canvas.height = Math.round(this.height * ratio);
    this.canvas.style.aspectRatio = `${this.width} / ${this.height}`;
    this.context.setTransform(ratio, 0, 0, ratio, 0, 0);
    this.pixelRatio = ratio;
  }

  start() {
    if (this.running) return;
    this.running = true;
    this.lastTime = performance.now();
    requestAnimationFrame(this.#frame);
  }

  stop() {
    this.running = false;
  }

  step(delta) {
    const safeDelta = Math.max(0, Math.min(this.maxDelta, delta));
    this.elapsed += safeDelta;
    this.tweens = this.tweens.filter((tween) => !tween.update(safeDelta));
    this.scene?.update(safeDelta, this.elapsed);
    this.render();
    this.stats.frameMs = safeDelta * 1000;
    this.stats.frames++;
    this.#fpsElapsed += safeDelta;
    this.#fpsFrames++;
    if (this.#fpsElapsed >= 0.5) {
      this.stats.fps = Math.round(this.#fpsFrames / this.#fpsElapsed);
      this.#fpsElapsed = 0;
      this.#fpsFrames = 0;
    }
  }

  render() {
    this.context.save();
    this.context.setTransform(this.pixelRatio, 0, 0, this.pixelRatio, 0, 0);
    this.context.clearRect(0, 0, this.width, this.height);
    this.scene?.render(this.context);
    this.context.restore();
  }

  destroy() {
    this.stop();
    for (const [target, type, listener] of this.#listeners) target.removeEventListener(type, listener);
    this.#listeners = [];
  }

  #frame = (now) => {
    if (!this.running) return;
    const delta = (now - this.lastTime) / 1000;
    this.lastTime = now;
    this.step(delta);
    requestAnimationFrame(this.#frame);
  };

  #listen(target, type, listener, options) {
    target.addEventListener(type, listener, options);
    this.#listeners.push([target, type, listener]);
  }

  #point(event) {
    const bounds = this.canvas.getBoundingClientRect();
    return {
      x: (event.clientX - bounds.left) * this.width / bounds.width,
      y: (event.clientY - bounds.top) * this.height / bounds.height,
      originalEvent: event
    };
  }

  #bindInput() {
    this.canvas.tabIndex = this.canvas.tabIndex >= 0 ? this.canvas.tabIndex : 0;
    this.#listen(this.canvas, "pointermove", (event) => {
      const point = this.#point(event);
      const target = this.scene?.hitTest(point.x, point.y) || null;
      if (target !== this.hoverTarget) {
        if (this.hoverTarget) {
          this.hoverTarget.hovered = false;
          this.hoverTarget.onPointerLeave.emit(point);
        }
        this.hoverTarget = target;
        if (target) {
          target.hovered = true;
          target.onPointerEnter.emit(point);
        }
      }
      this.canvas.style.cursor = target?.cursor || "default";
    });
    this.#listen(this.canvas, "pointerdown", (event) => {
      const point = this.#point(event);
      const target = this.scene?.hitTest(point.x, point.y) || null;
      this.canvas.focus();
      if (!target) return;
      this.pressTarget = this.focusTarget = target;
      target.pressed = true;
      target.onPointerDown.emit(point);
      this.canvas.setPointerCapture?.(event.pointerId);
    });
    this.#listen(this.canvas, "pointerup", (event) => {
      const point = this.#point(event);
      const target = this.scene?.hitTest(point.x, point.y) || null;
      if (this.pressTarget) {
        const pressed = this.pressTarget;
        pressed.pressed = false;
        pressed.onPointerUp.emit(point);
        if (pressed === target) pressed.onClick.emit(point);
        this.pressTarget = null;
      }
      this.canvas.releasePointerCapture?.(event.pointerId);
    });
    this.#listen(this.canvas, "pointerleave", (event) => {
      if (this.hoverTarget) {
        this.hoverTarget.hovered = false;
        this.hoverTarget.onPointerLeave.emit(this.#point(event));
        this.hoverTarget = null;
      }
      this.canvas.style.cursor = "default";
    });
    this.#listen(this.canvas, "keydown", (event) => {
      this.focusTarget?.onKeyDown.emit({ key: event.key, originalEvent: event });
      if ((event.key === "Enter" || event.key === " ") && this.focusTarget) {
        event.preventDefault();
        this.focusTarget.onClick.emit({ key: event.key, originalEvent: event });
      }
    });
  }
}
