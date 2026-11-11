const MAX_SOURCE_BYTES = 10_000_000;
const MAX_EDGE_PX = 640;
const MAX_DATA_URL_LENGTH = 210_000;

/** Reads an image file, downsizes it and returns a JPEG data URL the API accepts. Throws a user-facing Error. */
export async function compressImage(file: File): Promise<string> {
  if (!file.type.startsWith('image/') || file.size > MAX_SOURCE_BYTES) throw new Error('Chọn ảnh dưới 10 MB.');
  const url = URL.createObjectURL(file);
  try {
    const photo = new Image();
    await new Promise<void>((resolve, reject) => {
      photo.onload = () => resolve();
      photo.onerror = () => reject(new Error('Không mở được ảnh này. Hãy chọn ảnh JPEG, PNG hoặc WebP.'));
      photo.src = url;
    });
    const scale = Math.min(1, MAX_EDGE_PX / Math.max(photo.naturalWidth, photo.naturalHeight));
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(photo.naturalWidth * scale));
    canvas.height = Math.max(1, Math.round(photo.naturalHeight * scale));
    const context = canvas.getContext('2d');
    if (!context) throw new Error('Không xử lý được ảnh.');
    context.fillStyle = '#fff';
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(photo, 0, 0, canvas.width, canvas.height);
    let image = canvas.toDataURL('image/jpeg', .78);
    if (image.length > MAX_DATA_URL_LENGTH) image = canvas.toDataURL('image/jpeg', .55);
    if (image.length > MAX_DATA_URL_LENGTH) throw new Error('Ảnh sau khi nén vẫn quá lớn. Hãy chọn ảnh đơn giản hơn.');
    return image;
  } finally {
    URL.revokeObjectURL(url);
  }
}
