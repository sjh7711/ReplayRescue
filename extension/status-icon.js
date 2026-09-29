// Two independent lights: left = NVIDIA Instant Replay, right = desktop app.
function makeStatusIcon(replayEnabled, connected, size) {
  const canvas = new OffscreenCanvas(size, size);
  const context = canvas.getContext('2d');
  const colors = [replayEnabled === true ? '#34d76a' : '#f05252', connected ? '#34d76a' : '#f05252'];
  colors.forEach((color, index) => {
    context.beginPath();
    context.arc(size * (index ? 0.75 : 0.25), size * 0.5, size * 0.205, 0, Math.PI * 2);
    context.fillStyle = color;
    context.fill();
    context.lineWidth = Math.max(0.75, size / 24);
    context.strokeStyle = '#1b2824';
    context.stroke();
  });
  return context.getImageData(0, 0, size, size);
}

function replayLabel(value, language) {
  return ReplayI18n.text(language, value === true ? 'active' : value === false ? 'inactive' : 'unknown');
}
