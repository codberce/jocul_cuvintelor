window.wordGame = window.wordGame || {};

let syncingHexInput = false;
let audioContext = null;
let masterGain = null;
let currentVolume = 0.35;

const volumeStorageKey = "wordGame.volume";

const clampVolume = (value) => {
  const parsed = Number(value);
  if (Number.isNaN(parsed)) {
    return 0.35;
  }

  return Math.max(0, Math.min(1, parsed));
};

const loadVolume = () => {
  try {
    return clampVolume(window.localStorage?.getItem(volumeStorageKey) ?? "0.35");
  } catch {
    return 0.35;
  }
};

const saveVolume = (value) => {
  try {
    window.localStorage?.setItem(volumeStorageKey, String(value));
  } catch {
    // Volume persistence is optional.
  }
};

const ensureAudio = () => {
  if (!audioContext) {
    const AudioContextType = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextType) {
      return null;
    }

    audioContext = new AudioContextType();
    masterGain = audioContext.createGain();
    masterGain.gain.value = currentVolume;
    masterGain.connect(audioContext.destination);
  }

  if (audioContext.state === "suspended") {
    audioContext.resume();
  }

  return audioContext;
};

const setMasterVolume = (value) => {
  currentVolume = clampVolume(value);
  if (masterGain && audioContext) {
    masterGain.gain.setTargetAtTime(currentVolume, audioContext.currentTime, 0.015);
  }
  saveVolume(currentVolume);
};

const envelopeGain = (context, start, duration, peak = 0.16) => {
  const gain = context.createGain();
  gain.gain.setValueAtTime(0.0001, start);
  gain.gain.exponentialRampToValueAtTime(Math.max(0.0001, peak), start + Math.min(0.025, duration * 0.25));
  gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
  gain.connect(masterGain);
  return gain;
};

const tone = (context, frequency, start, duration, type = "sine", peak = 0.16) => {
  const oscillator = context.createOscillator();
  const gain = envelopeGain(context, start, duration, peak);
  oscillator.type = type;
  oscillator.frequency.setValueAtTime(frequency, start);
  oscillator.connect(gain);
  oscillator.start(start);
  oscillator.stop(start + duration + 0.03);
};

const sweep = (context, from, to, start, duration, type = "triangle", peak = 0.14) => {
  const oscillator = context.createOscillator();
  const gain = envelopeGain(context, start, duration, peak);
  oscillator.type = type;
  oscillator.frequency.setValueAtTime(from, start);
  oscillator.frequency.exponentialRampToValueAtTime(to, start + duration);
  oscillator.connect(gain);
  oscillator.start(start);
  oscillator.stop(start + duration + 0.03);
};

const noiseBurst = (context, start, duration, peak = 0.06) => {
  const sampleRate = context.sampleRate;
  const buffer = context.createBuffer(1, Math.max(1, Math.floor(sampleRate * duration)), sampleRate);
  const data = buffer.getChannelData(0);
  for (let index = 0; index < data.length; index += 1) {
    data[index] = (Math.random() * 2 - 1) * (1 - index / data.length);
  }

  const source = context.createBufferSource();
  const gain = envelopeGain(context, start, duration, peak);
  source.buffer = buffer;
  source.connect(gain);
  source.start(start);
  source.stop(start + duration + 0.02);
};

const soundEffects = {
  start(context, now) {
    tone(context, 392, now, 0.11, "triangle", 0.12);
    tone(context, 523.25, now + 0.11, 0.12, "triangle", 0.13);
    tone(context, 659.25, now + 0.23, 0.16, "triangle", 0.14);
    tone(context, 783.99, now + 0.36, 0.22, "triangle", 0.13);
    noiseBurst(context, now + 0.34, 0.18, 0.028);
  },
  question(context, now) {
    sweep(context, 220, 740, now, 0.22, "sawtooth", 0.07);
    tone(context, 880, now + 0.18, 0.08, "triangle", 0.08);
  },
  reveal(context, now) {
    tone(context, 740, now, 0.07, "triangle", 0.1);
    tone(context, 988, now + 0.055, 0.09, "triangle", 0.08);
  },
  correct(context, now) {
    tone(context, 523.25, now, 0.09, "triangle", 0.12);
    tone(context, 659.25, now + 0.09, 0.1, "triangle", 0.13);
    tone(context, 783.99, now + 0.19, 0.16, "triangle", 0.14);
  },
  wrong(context, now) {
    sweep(context, 260, 120, now, 0.25, "sawtooth", 0.09);
    tone(context, 98, now + 0.12, 0.18, "sine", 0.055);
  },
  tick(context, now) {
    tone(context, 880, now, 0.055, "square", 0.055);
  },
  result(context, now) {
    tone(context, 330, now, 0.11, "triangle", 0.1);
    tone(context, 392, now + 0.1, 0.11, "triangle", 0.1);
    tone(context, 523.25, now + 0.2, 0.18, "triangle", 0.11);
  },
  finish(context, now) {
    tone(context, 523.25, now, 0.12, "triangle", 0.1);
    tone(context, 659.25, now + 0.11, 0.12, "triangle", 0.1);
    tone(context, 783.99, now + 0.22, 0.12, "triangle", 0.1);
    tone(context, 1046.5, now + 0.36, 0.32, "triangle", 0.13);
    noiseBurst(context, now + 0.36, 0.24, 0.03);
  }
};

currentVolume = loadVolume();

window.wordGame.setVolume = (value) => {
  setMasterVolume(value);
};

window.wordGame.initializeSoundControls = () => {
  currentVolume = loadVolume();
  document.querySelectorAll("[data-volume-slider]").forEach(slider => {
    slider.value = String(Math.round(currentVolume * 100));
    slider.addEventListener("input", event => {
      setMasterVolume(clampVolume(event.target.value / 100));
    });
  });
};

window.wordGame.playSound = (name) => {
  if (currentVolume <= 0) {
    return;
  }

  const context = ensureAudio();
  const effect = soundEffects[name];
  if (!context || !effect) {
    return;
  }

  effect(context, context.currentTime + 0.01);
};

const getHexCells = (board) => Array
  .from(board.querySelectorAll("[data-word-cell]"))
  .sort((left, right) => Number(left.dataset.cellIndex) - Number(right.dataset.cellIndex));

const focusHexCell = (cell) => {
  cell.focus();
  cell.select();
};

const setHexCellValue = (cell, value) => {
  cell.value = value;
  syncingHexInput = true;
  cell.dispatchEvent(new Event("input", { bubbles: true }));
  syncingHexInput = false;
};

const normalizeHexInput = (value) => Array
  .from(value.toUpperCase())
  .filter(character => character.trim().length > 0);

window.wordGame.focusNextEmptyHex = (boardId, currentIndex) => {
  const board = document.querySelector(`[data-word-board="${boardId}"]`);
  if (!board) {
    return;
  }

  const cells = getHexCells(board);

  for (const cell of cells) {
    const index = Number(cell.dataset.cellIndex);
    if (index <= currentIndex || cell.disabled || cell.value.trim().length > 0) {
      continue;
    }

    focusHexCell(cell);
    return;
  }
};

window.wordGame.focusFirstEmptyHex = (boardId) => {
  const board = document.querySelector(`[data-word-board="${boardId}"]`);
  if (!board) {
    return;
  }

  const cell = getHexCells(board)
    .find(input => !input.disabled && input.value.trim().length === 0);

  if (cell) {
    focusHexCell(cell);
  }
};

window.wordGame.focusPreviousFilledHex = (boardId, currentIndex) => {
  const board = document.querySelector(`[data-word-board="${boardId}"]`);
  if (!board) {
    return;
  }

  const cells = getHexCells(board).reverse();

  const cell = cells.find(input =>
    Number(input.dataset.cellIndex) < currentIndex &&
    !input.disabled &&
    input.value.trim().length > 0);

  if (cell) {
    focusHexCell(cell);
  }
};

document.addEventListener("keydown", event => {
  if (event.key !== "Backspace" || !(event.target instanceof HTMLInputElement)) {
    return;
  }

  const current = event.target.matches("[data-word-cell]") ? event.target : null;
  const board = current?.closest("[data-word-board]");
  if (!current || !board || current.disabled) {
    return;
  }

  event.preventDefault();
  event.stopImmediatePropagation();

  const currentIndex = Number(current.dataset.cellIndex);
  const previousCells = getHexCells(board)
    .filter(input => Number(input.dataset.cellIndex) < currentIndex && !input.disabled)
    .reverse();

  if (current.value.trim().length > 0) {
    setHexCellValue(current, "");
    focusHexCell(previousCells[0] ?? current);
    return;
  }

  const previousFilled = previousCells.find(input => input.value.trim().length > 0);
  if (previousFilled) {
    setHexCellValue(previousFilled, "");
    focusHexCell(previousFilled);
    return;
  }

  focusHexCell(current);
}, true);

document.addEventListener("input", event => {
  if (syncingHexInput || !(event.target instanceof HTMLInputElement)) {
    return;
  }

  const current = event.target.matches("[data-word-cell]") ? event.target : null;
  const board = current?.closest("[data-word-board]");
  if (!current || !board || current.disabled) {
    return;
  }

  const characters = normalizeHexInput(current.value);
  if (characters.length === 0) {
    current.value = "";
    return;
  }

  const cells = getHexCells(board);
  const currentIndex = Number(current.dataset.cellIndex);
  const writableCells = cells.filter(input =>
    Number(input.dataset.cellIndex) >= currentIndex &&
    !input.disabled);

  for (const [offset, character] of characters.entries()) {
    const cell = writableCells[offset];
    if (!cell) {
      break;
    }

    if (cell === current) {
      cell.value = character;
      continue;
    }

    setHexCellValue(cell, character);
  }

  const lastFilledCell = writableCells[Math.min(characters.length - 1, writableCells.length - 1)] ?? current;
  const nextEmptyCell = cells.find(input =>
    Number(input.dataset.cellIndex) > Number(lastFilledCell.dataset.cellIndex) &&
    !input.disabled &&
    input.value.trim().length === 0);

  focusHexCell(nextEmptyCell ?? lastFilledCell);
}, true);
