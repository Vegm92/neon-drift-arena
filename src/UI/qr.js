import QRCode from "qrcode";

export function qrToSvg(text) {
  const { size, data } = QRCode.create(text, { errorCorrectionLevel: "M" }).modules;
  let dots = "";
  for (let y = 0; y < size; y++)
    for (let x = 0; x < size; x++)
      if (data[y * size + x]) dots += `<circle cx="${x + 0.5}" cy="${y + 0.5}" r="0.42"/>`;
  return `<svg viewBox="0 0 ${size} ${size}" fill="currentColor">${dots}</svg>`;
}
