// Render the editable SVG sources. Requires Node.js and the sharp package.
// npm install --prefix artifacts/brand-tools sharp
// NODE_PATH=./artifacts/brand-tools/node_modules node tools/build-brand-assets.cjs
const path = require('node:path');
const sharp = require('sharp');
const directory = path.resolve(__dirname, '../Images/branding');

async function main() {
  for (const size of [128, 512]) {
    await sharp(path.join(directory, 'project-z-icon.svg'), { density: 384 })
      .resize(size, size, { kernel: 'lanczos3' }).png({ compressionLevel: 9 })
      .toFile(path.join(directory, `project-z-icon${size === 128 ? '' : '-512'}.png`));
  }
  await sharp(path.join(directory, 'project-z-social.svg'), { density: 144 })
    .resize(1280, 640, { kernel: 'lanczos3' }).png({ compressionLevel: 9 })
    .toFile(path.join(directory, 'project-z-social.png'));
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
