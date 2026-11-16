// Original additive synthesis; no recordings, samples or external sound fonts.
// Run: node assets/audio/generate-audio.cjs
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 22050;

function write(name, frequency, duration, bell = false) {
    const count = Math.floor(rate * duration);
    const wave = Buffer.alloc(44 + count * 2);
    wave.write('RIFF'); wave.writeUInt32LE(wave.length - 8, 4);
    wave.write('WAVEfmt ', 8); wave.writeUInt32LE(16, 16);
    wave.writeUInt16LE(1, 20); wave.writeUInt16LE(1, 22);
    wave.writeUInt32LE(rate, 24); wave.writeUInt32LE(rate * 2, 28);
    wave.writeUInt16LE(2, 32); wave.writeUInt16LE(16, 34);
    wave.write('data', 36); wave.writeUInt32LE(count * 2, 40);
    for (let i = 0; i < count; i++) {
        const t = i / rate;
        const envelope = Math.min(1, i / 100) * Math.pow(1 - i / count, bell ? 2 : 1.5);
        const fundamental = Math.sin(2 * Math.PI * frequency * t);
        const overtone = Math.sin(2 * Math.PI * frequency * (bell ? 2.76 : 2) * t);
        const value = Math.round((fundamental + 0.12 * overtone) * envelope * 7000);
        assert(Math.abs(value) < 32768);
        wave.writeInt16LE(value, 44 + i * 2);
    }
    assert.equal(wave.readUInt32LE(40), wave.length - 44);
    fs.writeFileSync(path.join(__dirname, name + '.wav'), wave);
}

for (let combo = 1; combo <= 13; combo++)
    write(combo === 1 ? 'completion' : `combo-${String(combo).padStart(2, '0')}`,
        660 * Math.pow(2, (combo - 1) / 12), 0.22);
write('tick', 1000, 0.035);
write('bell', 880, 0.8, true);
