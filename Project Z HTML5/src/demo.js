import {
  Button,
  Circle,
  Easing,
  ProjectZEngine,
  Rectangle,
  Scene,
  SceneElement,
  StackPanel,
  TextElement
} from "./engine.js";

const canvas = document.querySelector("#project-z-canvas");
const engine = new ProjectZEngine(canvas, { width: 1280, height: 720 });
const scene = new Scene({ background: "#06100e" });
engine.setScene(scene);

class GridBackdrop extends SceneElement {
  constructor() {
    super({ width: 1280, height: 720 });
    this.offset = 0;
  }

  update(delta, elapsed) {
    this.offset = (elapsed * 9) % 44;
    super.update(delta, elapsed);
  }

  draw(context) {
    const glow = context.createRadialGradient(860, 320, 0, 860, 320, 580);
    glow.addColorStop(0, "rgba(32, 188, 150, .17)");
    glow.addColorStop(0.5, "rgba(12, 74, 61, .08)");
    glow.addColorStop(1, "rgba(6, 16, 14, 0)");
    context.fillStyle = glow;
    context.fillRect(0, 0, this.width, this.height);
    context.strokeStyle = "rgba(91, 255, 216, .045)";
    context.lineWidth = 1;
    for (let x = -44 + this.offset; x < this.width; x += 44) {
      context.beginPath(); context.moveTo(x, 0); context.lineTo(x, this.height); context.stroke();
    }
    for (let y = -44 + this.offset; y < this.height; y += 44) {
      context.beginPath(); context.moveTo(0, y); context.lineTo(this.width, y); context.stroke();
    }
  }
}

class PulseGraph extends SceneElement {
  constructor(options) {
    super(options);
    this.samples = Array.from({ length: 84 }, (_, index) => Math.sin(index / 7) * 8);
    this.phase = 0;
    this.active = true;
  }

  update(delta, elapsed) {
    if (this.active) {
      this.phase += delta;
      this.samples.shift();
      this.samples.push(Math.sin(elapsed * 3.1) * 12 + Math.sin(elapsed * 7.4) * 4 + Math.random() * 3);
    }
    super.update(delta, elapsed);
  }

  draw(context) {
    const gradient = context.createLinearGradient(0, 0, this.width, 0);
    gradient.addColorStop(0, "#1f997e");
    gradient.addColorStop(0.55, "#35e7c4");
    gradient.addColorStop(1, "#b0ffe9");
    context.strokeStyle = gradient;
    context.lineWidth = 2;
    context.shadowColor = "#35e7c4";
    context.shadowBlur = 12;
    context.beginPath();
    this.samples.forEach((sample, index) => {
      const x = index / (this.samples.length - 1) * this.width;
      const y = this.height / 2 + sample;
      if (index === 0) context.moveTo(x, y); else context.lineTo(x, y);
    });
    context.stroke();
    context.shadowColor = "transparent";
  }
}

const backdrop = new GridBackdrop();
scene.add(backdrop);

const title = new TextElement({ x: 70, y: 60, text: "PROJECT Z", color: "#f3fff9", fontSize: 30, fontWeight: 800 });
const subtitle = new TextElement({ x: 72, y: 104, text: "HTML5 ENGINE  /  CANVAS2D RUNTIME", color: "#62d6ba", fontSize: 12, fontWeight: 700 });
scene.add(title, subtitle);

const statusCard = new Rectangle({ x: 70, y: 155, width: 760, height: 445, fill: "rgba(8, 27, 23, .88)", stroke: "rgba(83, 232, 198, .28)", radius: 24, shadowColor: "rgba(0, 0, 0, .55)", shadowBlur: 32, shadowOffsetY: 16 });
scene.add(statusCard);

statusCard.add(
  new TextElement({ x: 36, y: 34, text: "LIVE SCENE", color: "#77f8d8", fontSize: 12, fontWeight: 800 }),
  new TextElement({ x: 36, y: 62, text: "A browser-native Project Z runtime", color: "#f4fffb", fontSize: 28, fontWeight: 750 }),
  new TextElement({ x: 36, y: 110, text: "Scene graph · HiDPI canvas · routed input · layout · animation", color: "#8ca9a1", fontSize: 15 })
);

const viewport = new Rectangle({ x: 36, y: 157, width: 688, height: 185, fill: "#05120f", stroke: "rgba(53, 231, 196, .18)", radius: 18, clip: true });
statusCard.add(viewport);
const graph = new PulseGraph({ x: 28, y: 48, width: 632, height: 86 });
viewport.add(graph);

const nodes = [];
for (let index = 0; index < 7; index++) {
  const node = new Circle({ x: 48 + index * 96, y: 65 + Math.sin(index) * 25, radius: index === 3 ? 7 : 4, fill: index === 3 ? "#eafff9" : "#35e7c4", glow: index === 3 ? 22 : 10 });
  node.baseY = node.y;
  const baseUpdate = node.update.bind(node);
  node.update = (delta, elapsed) => {
    if (graph.active) node.y = node.baseY + Math.sin(elapsed * 1.8 + index * 0.8) * 18;
    baseUpdate(delta, elapsed);
  };
  nodes.push(node);
  viewport.add(node);
}

const badge = new Rectangle({ x: 36, y: 370, width: 120, height: 39, fill: "rgba(53, 231, 196, .12)", stroke: "rgba(53, 231, 196, .35)", radius: 20 });
badge.add(new Circle({ x: 15, y: 15, radius: 4, fill: "#5affd7", glow: 9 }));
badge.add(new TextElement({ x: 34, y: 12, text: "RUNNING", color: "#aaffea", fontSize: 11, fontWeight: 800 }));
statusCard.add(badge);

const fpsText = new TextElement({ x: 576, y: 384, text: "-- FPS", color: "#75938b", fontSize: 12, fontWeight: 700 });
statusCard.add(fpsText);
const originalSceneUpdate = scene.update.bind(scene);
scene.update = (delta, elapsed) => {
  fpsText.text = `${engine.stats.fps || "--"} FPS`;
  originalSceneUpdate(delta, elapsed);
};

const sidePanel = new Rectangle({ x: 865, y: 155, width: 345, height: 445, fill: "rgba(9, 29, 25, .78)", stroke: "rgba(83, 232, 198, .2)", radius: 24 });
scene.add(sidePanel);
sidePanel.add(
  new TextElement({ x: 28, y: 32, text: "ENGINE CONTROLS", color: "#77f8d8", fontSize: 12, fontWeight: 800 }),
  new TextElement({ x: 28, y: 60, text: "Interactive runtime", color: "#f4fffb", fontSize: 23, fontWeight: 750 }),
  new TextElement({ x: 28, y: 98, text: "Pointer and keyboard events route\nthrough the canvas scene graph.", color: "#8ca9a1", fontSize: 14, lineHeight: 22 })
);

const controls = new StackPanel({ x: 28, y: 168, width: 289, height: 220, orientation: "vertical", gap: 14 });
const motionButton = new Button({ name: "motion", width: 289, height: 52, text: "PAUSE MOTION", radius: 13, stroke: "rgba(83, 232, 198, .28)" });
const pulseButton = new Button({ name: "pulse", width: 289, height: 52, text: "PULSE SCENE", radius: 13, stroke: "rgba(83, 232, 198, .28)" });
const resetButton = new Button({ name: "reset", width: 289, height: 52, text: "RESET", radius: 13, stroke: "rgba(83, 232, 198, .28)" });
controls.add(motionButton, pulseButton, resetButton);
sidePanel.add(controls);
sidePanel.layout();

motionButton.onClick.on(() => {
  graph.active = !graph.active;
  motionButton.label.text = graph.active ? "PAUSE MOTION" : "RESUME MOTION";
  canvas.dataset.motion = graph.active ? "running" : "paused";
  canvas.setAttribute("aria-label", `Interactive Project Z HTML5 engine demo; motion ${canvas.dataset.motion}`);
});

pulseButton.onClick.on(() => {
  engine.tween(statusCard, { scaleX: 1.018, scaleY: 1.018 }, 0.12, {
    ease: Easing.easeOutCubic,
    onComplete: () => engine.tween(statusCard, { scaleX: 1, scaleY: 1 }, 0.32, { ease: Easing.easeOutCubic })
  });
  for (const [index, node] of nodes.entries()) {
    engine.tween(node, { scaleX: 2.2, scaleY: 2.2 }, 0.12 + index * 0.015, {
      onComplete: () => engine.tween(node, { scaleX: 1, scaleY: 1 }, 0.4)
    });
  }
});

resetButton.onClick.on(() => {
  graph.active = true;
  motionButton.label.text = "PAUSE MOTION";
  canvas.dataset.motion = "running";
  canvas.setAttribute("aria-label", "Interactive Project Z HTML5 engine demo; motion running");
  statusCard.scaleX = statusCard.scaleY = 1;
  nodes.forEach((node) => { node.scaleX = node.scaleY = 1; });
});

scene.add(
  new TextElement({ x: 70, y: 645, text: "ZERO DEPENDENCIES", color: "#4e746a", fontSize: 11, fontWeight: 800 }),
  new TextElement({ x: 210, y: 645, text: "SCENE GRAPH", color: "#4e746a", fontSize: 11, fontWeight: 800 }),
  new TextElement({ x: 325, y: 645, text: "60 FPS LOOP", color: "#4e746a", fontSize: 11, fontWeight: 800 }),
  new TextElement({ x: 1122, y: 645, text: "v0.1", color: "#4e746a", fontSize: 11, fontWeight: 800 })
);

canvas.dataset.engineReady = "true";
canvas.dataset.motion = "running";
canvas.setAttribute("aria-label", "Interactive Project Z HTML5 engine demo; motion running");
engine.start();
globalThis.projectZ = { engine, scene };
