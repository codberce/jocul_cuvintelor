window.wordGame = window.wordGame || {};

let syncingHexInput = false;

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
